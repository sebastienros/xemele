"""Verify actual corpus infrastructure, runner, and preserved integrated bootstrap."""
import hashlib
import json
from pathlib import Path
import platform
import shlex
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'artifacts/M0-corpora/verification.txt'
TEST_PROJECT = 'docs/reports/M0-corpora/Tests/Xemele.CorpusInfrastructure.Tests.csproj'
RUNNER_PROJECT = 'docs/reports/M0-corpora/Runner/Xemele.CorpusRunner.csproj'
PROFILE = 'xml-well-formedness-bootstrap-v1'
LOG = []


def run(args, expected=0):
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    entry = '$ ' + shlex.join(args) + '\n' + result.stdout + f'Exit: {result.returncode}\n'
    LOG.append(entry)
    print(entry, flush=True)
    if result.returncode != expected:
        raise RuntimeError(f'Expected {expected}, got {result.returncode}: {args}')
    return result.stdout


def runner(command, profile=PROFILE, expected=0):
    return json.loads(run(['dotnet', 'run', '--project', RUNNER_PROJECT, '-c', 'Release',
                           '--no-build', '--no-restore', '--', command, str(ROOT), profile,
                           str(ROOT / 'artifacts/M0-corpora/cache')], expected))


def main():
    try:
        LOG.append(f'Host: {platform.platform()} {platform.machine()}\n')
        run(['git', 'rev-parse', 'HEAD'])
        assert run(['dotnet', '--version']).strip() == '10.0.400'
        for project in ['Xemele.slnx', TEST_PROJECT, RUNNER_PROJECT]:
            run(['dotnet', 'restore', project, '--disable-parallel', '-p:UseSharedCompilation=false', '-nodeReuse:false'])
            run(['dotnet', 'build', project, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false', '-nodeReuse:false'])
        results = ROOT / 'artifacts/M0-corpora/test-results'
        if results.exists(): shutil.rmtree(results)
        run(['dotnet', 'test', '--project', TEST_PROJECT, '-c', 'Release', '--no-build', '--no-restore',
             '--no-ansi', '--no-progress', '--minimum-expected-tests', '24', '--report-xunit-xml',
             '--results-directory', str(results)])
        executed = [test for path in results.glob('*.xunit.xml') for test in ET.parse(path).iter('test')]
        skipped = [test for test in executed if test.get('result') == 'Skip']
        passed = [test for test in executed if test.get('result') == 'Pass']
        assert len(executed) == 24 and len(passed) + len(skipped) == len(executed)
        assert len(skipped) <= 1 and all(test.get('name', '').endswith('.SymlinkOutsideRepositoryFailsBeforeReadOrWrite') for test in skipped)
        LOG.append(f'Focused infrastructure tests: {len(passed)} passed, {len(skipped)} capability-skipped; parser cases executed: 0.\n')
        for test in skipped:
            LOG.append('Explicit symlink capability skip: ' + ''.join(test.itertext()) + '\n')
        assets = json.loads((ROOT / 'docs/reports/M0-corpora/Tests/obj/project.assets.json').read_text())['libraries']
        assert 'xunit.v3.mtp-v2/4.0.1' in assets
        assert any(key.startswith('Microsoft.Testing.Platform/') for key in assets)
        assert not any(key.startswith(('xunit/2', 'Microsoft.NET.Test.Sdk/')) for key in assets)
        LOG.append('Focused actual package inventory: ' + json.dumps(sorted(assets)) + '\n')
        for option in [None, '--help', '-h', 'help']:
            run(['dotnet', 'run', '--project', RUNNER_PROJECT, '-c', 'Release', '--no-build', '--no-restore', '--'] +
                ([] if option is None else [option]))
        run(['dotnet', 'run', '--project', RUNNER_PROJECT, '-c', 'Release', '--no-build', '--no-restore', '--', 'unknown'], 2)
        cache = ROOT / 'artifacts/M0-corpora/cache'
        if cache.exists(): shutil.rmtree(cache)
        missing = runner('provision', expected=1)
        assert missing['missingCache'] == 2 and missing['provisioned'] == 0
        imported = runner('import')
        assert imported['provisioned'] == 2 and imported['standardsPassed'] == imported['parserCasesExecuted'] == 0
        manifest = json.loads((ROOT / 'tests/corpora/bootstrap/manifest.json').read_text())
        for fixture in manifest['fixtures']:
            output = cache / 'fixtures' / manifest['sha256'] / fixture['sha256']
            assert hashlib.sha256(output.read_bytes()).hexdigest() == fixture['sha256']
        assert runner('provision')['provisioned'] == 2
        unsupported = runner('provision', profile='unsupported-profile', expected=1)
        assert unsupported['unsupportedProfile'] == 2 and unsupported['provisioned'] == 0
        (cache / 'sources' / (manifest['sha256'] + '.json')).write_bytes(b'corrupt')
        corrupt = runner('provision', expected=1)
        assert corrupt['corruptCache'] == 2 and corrupt['provisioned'] == 0
        runner('import')
        run([sys.executable, '-B', 'tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py'])
        discovery = json.loads(run([sys.executable, '-B', 'tools/ApiBaseline/discover.py', '--bootstrap-empty-api']))
        assert discovery['fixtures'] == 2 and discovery['corpusManifests'] == ['tests/corpora/bootstrap/manifest.json']
        run([sys.executable, '-B', '-m', 'unittest', 'discover', '-s', 'tests/Xemele.TestUtilities/Build', '-p', 'test_*.py', '-v'])
        run([sys.executable, '-B', 'docs/reports/M0-tests-mtp/check_runner.py'])
        for project in ['src/Xemele.Tool', 'tests/Xemele.Fuzz', 'benchmarks/Xemele.Benchmarks', 'tools/CorpusManager']:
            for arg in [None, '--help', '-h', 'help']:
                command = ['dotnet', 'run', '-c', 'Release', '--no-build', '--no-restore', '--project', project, '--']
                run(command + ([] if arg is None else [arg]))
        for project in ['src/Xemele.Tool', 'tests/Xemele.Fuzz', 'tools/CorpusManager']:
            run(['dotnet', 'run', '-c', 'Release', '--no-build', '--no-restore', '--project', project, '--', 'unknown'], 2)
        run(['dotnet', 'pack', 'src/Xemele/Xemele.csproj', '-c', 'Release', '--no-build', '--no-restore', '-o', 'artifacts/M0-corpora/packages'])
        with zipfile.ZipFile(ROOT / 'artifacts/M0-corpora/packages/Xemele.1.0.0.nupkg') as package:
            assert [name for name in package.namelist() if name.startswith('lib/')] == ['lib/net10.0/Xemele.dll']
            assert not any(name.startswith('runtimes/') for name in package.namelist())
        run([sys.executable, 'tools/planning/tasks.py', 'validate'])
        LOG.append('All checks passed. Offline provision/checksum evidence only; no Xemele conformance or hot-path performance claim.\n')
    finally:
        REPORT.parent.mkdir(parents=True, exist_ok=True)
        REPORT.write_text('\n'.join(LOG))
        print(f'Transcript: {REPORT}', flush=True)


if __name__ == '__main__':
    main()
