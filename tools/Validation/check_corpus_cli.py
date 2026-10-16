"""Build/run the outside-solution MTP app and check the actual root CLI offline."""
import hashlib
import json
from pathlib import Path
import platform
import shlex
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
ARTIFACTS = ROOT / 'artifacts/corpus-cli'
TEST_PROJECT = 'tests/CorpusManager/Xemele.CorpusInfrastructure.Tests.csproj'
PROFILE = 'xml-well-formedness-bootstrap-v1'
LOG = []
CLI_CHECKS = 0


def run(args, expected=0):
    result = subprocess.run(args, cwd=ROOT, text=True, capture_output=True)
    entry = '$ ' + shlex.join(args) + '\n' + result.stdout + result.stderr + f'Exit: {result.returncode}\n'
    LOG.append(entry)
    print(entry, flush=True)
    assert result.returncode == expected, f'Expected {expected}, got {result.returncode}: {args}'
    return result


def cli(arguments, expected=0):
    global CLI_CHECKS
    result = run(['dotnet', 'run', '--project', 'tools/CorpusManager', '-c', 'Release',
                  '--no-build', '--no-restore', '--', *map(str, arguments)], expected)
    CLI_CHECKS += 1
    return result


def report(command, repository, cache, outcome, profile=PROFILE, expected=1):
    value = json.loads(cli([command, repository, profile, cache], expected).stdout)
    assert value['schemaVersion'] == 1 and value['corpora'] == 1 and value['total'] == 2
    counts = ['provisioned', 'missingCache', 'corruptCache', 'unsupportedProfile', 'excluded', 'infrastructureFailure']
    assert sum(value[key] for key in counts) == value['total']
    assert value[outcome] == 2 and all(value[key] == 0 for key in counts if key != outcome)
    assert value['parserCasesExecuted'] == value['standardsPassed'] == 0
    details = value['reports'][0]
    manifest = json.loads((repository / 'tests/corpora/bootstrap/manifest.json').read_text())
    for name, field in [('CorpusId', 'id'), ('Profile', 'profile'), ('SourceUrl', 'sourceUrl'),
                        ('Revision', 'revision'), ('SourceSha256', 'sha256'), ('License', 'license'),
                        ('Notice', 'notice'), ('AdapterVersion', 'adapterVersion')]:
        assert details[name] == manifest[field]
    assert details['Total'] == 2 and details['ParserCasesExecuted'] == details['StandardsPassed'] == 0
    assert details['Successful'] == (expected == 0)
    assert len(details['Cases']) == 2
    for case, fixture in zip(details['Cases'], manifest['fixtures']):
        assert case['Path'] == fixture['path'] and case['Classification'] == fixture['classification']
        assert case['Outcome'] == outcome[0].upper() + outcome[1:] and case['Detail']
        if expected == 0:
            assert hashlib.sha256(Path(case['CachePath']).read_bytes()).hexdigest() == fixture['sha256']
        else:
            assert case['CachePath'] is None
    return value


def infrastructure_error(arguments):
    result = cli(arguments, 1)
    assert result.stdout == '' and 'Corpus infrastructure error:' in result.stderr


def main():
    try:
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        LOG.append(f'Host: {platform.platform()} {platform.machine()}\n')
        run(['git', 'rev-parse', 'HEAD'])
        assert run(['dotnet', '--version']).stdout.strip() == '10.0.400'
        for project in ['tools/CorpusManager', TEST_PROJECT]:
            run(['dotnet', 'restore', project, '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'])
            run(['dotnet', 'build', project, '-c', 'Release', '--no-restore',
                 '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'])
        results = ARTIFACTS / 'test-results'
        if results.exists():
            shutil.rmtree(results)
        run(['dotnet', 'test', '--project', TEST_PROJECT, '-c', 'Release', '--no-build', '--no-restore',
             '--no-ansi', '--no-progress', '--filter-class', 'Xemele.CorpusInfrastructure.Tests.ProvisioningTests',
             '--minimum-expected-tests', '24', '--report-xunit-xml',
             '--results-directory', str(results)])
        tests = [test for path in results.glob('*.xunit.xml') for test in ET.parse(path).iter('test')]
        skipped = [test for test in tests if test.get('result') == 'Skip']
        passed = [test for test in tests if test.get('result') == 'Pass']
        assert len(tests) == 24 and len(passed) + len(skipped) == 24
        assert len(skipped) <= 1 and all(test.get('name', '').endswith('.SymlinkOutsideRepositoryFailsBeforeReadOrWrite') for test in skipped)
        LOG.append(f'Focused MTP tests: {len(passed)} passed, {len(skipped)} capability-skipped.\n')
        for test in skipped:
            LOG.append('Explicit capability skip: ' + ''.join(test.itertext()) + '\n')
        for args in [[], ['--help'], ['-h'], ['help']]:
            result = cli(args)
            assert 'import|provision' in result.stdout and 'No parser execution.' in result.stdout
        for args in [['unknown'], ['import'], ['provision', ROOT], ['import', ROOT, PROFILE, ROOT, 'extra']]:
            result = cli(args, 2)
            assert result.stdout == '' and 'Unknown command or invalid arguments.' in result.stderr
        # All mutations use a disposable repository, including malformed manifest tests.
        with tempfile.TemporaryDirectory(prefix='cli-', dir=ARTIFACTS) as temporary:
            repository = Path(temporary)
            module = repository / 'tests/corpora/bootstrap'
            shutil.copytree(ROOT / 'tests/corpora/bootstrap', module)
            cache = repository / 'artifacts/corpora'
            manifest_path = module / 'manifest.json'
            original = manifest_path.read_bytes()
            manifest = json.loads(original)
            report('provision', repository, cache, 'missingCache')
            report('import', repository, cache, 'provisioned', expected=0)
            report('provision', repository, cache, 'provisioned', expected=0)
            report('provision', repository, cache, 'unsupportedProfile', profile='unsupported-profile')
            source_cache = cache / 'sources' / (manifest['sha256'] + '.json')
            source_cache.write_bytes(b'corrupt')
            report('provision', repository, cache, 'corruptCache')
            report('import', repository, cache, 'provisioned', expected=0)
            source_cache.unlink()
            source_cache.mkdir()
            report('provision', repository, cache, 'infrastructureFailure')
            source_cache.rmdir()
            manifest['fixtures'][0]['exclusion'] = 'Explicit test exclusion'
            manifest_path.write_text(json.dumps(manifest))
            excluded = json.loads(cli(['provision', repository, PROFILE, cache], 1).stdout)
            assert excluded['excluded'] == 1 and excluded['missingCache'] == 1 and excluded['provisioned'] == 0
            assert sum(excluded[key] for key in ['provisioned', 'missingCache', 'corruptCache', 'unsupportedProfile', 'excluded', 'infrastructureFailure']) == 2
            assert excluded['parserCasesExecuted'] == excluded['standardsPassed'] == 0
            manifest_path.write_text('{malformed')
            infrastructure_error(['provision', repository, PROFILE, cache])
            manifest_path.write_bytes(original)
            (module / 'source.bundle.json').write_bytes(b'corrupt source')
            infrastructure_error(['import', repository, PROFILE, cache])
            # No manifest inventory cannot become a vacuous successful import/provision.
            shutil.rmtree(repository / 'tests')
            for command in ['import', 'provision']:
                empty = json.loads(cli([command, repository, PROFILE, cache], 1).stdout)
                assert empty['corpora'] == empty['total'] == empty['provisioned'] == 0
                assert empty['parserCasesExecuted'] == empty['standardsPassed'] == 0
        LOG.append(f'Root CLI checks: {CLI_CHECKS} passed. Offline provisioning only; zero parser/standards cases. No hosted-platform claim.\n')
    finally:
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        (ARTIFACTS / 'verification.txt').write_text('\n'.join(LOG))


if __name__ == '__main__':
    main()
