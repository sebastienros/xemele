"""Known-revision full inventory/provisioning verification; no product XML execution."""
import json
import os
from pathlib import Path
import platform
import shlex
import subprocess
import sys
import xml.etree.ElementTree as ET
import import_suite as suite

ROOT = suite.ROOT
REPORT = ROOT / 'artifacts/M1-corpus-import/verification.txt'
LOG = []


def run(args, expected=0):
    result = subprocess.run(args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    entry = '$ ' + shlex.join(args) + '\n' + result.stdout + f'Exit: {result.returncode}\n'
    LOG.append(entry)
    print(entry, flush=True)
    if result.returncode != expected:
        raise RuntimeError('Check exit did not match expected: ' + str(args))
    return result.stdout


def main():
    try:
        LOG.append(f'Host: {platform.platform()} {platform.machine()}\n')
        run(['git', 'rev-parse', 'HEAD'])
        assert run(['dotnet', '--version']).strip() == '10.0.400'
        run([sys.executable, '-B', 'docs/reports/M1-corpus-import/import_suite.py', '--check'])
        run([sys.executable, '-B', '-m', 'unittest', 'discover', '-s', 'docs/reports/M1-corpus-import', '-p', 'test_import.py', '-v'])
        projects = ['docs/reports/M1-corpus-import/Runner/Runner.csproj', 'docs/reports/M1-corpus-import/Tests/Tests.csproj']
        for project in projects:
            run(['dotnet', 'build', project, '-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'])
        results = ROOT / 'artifacts/M1-corpus-import/test-results'
        results.mkdir(parents=True, exist_ok=True)
        for path in results.glob('*.xunit.xml'): path.unlink()
        run(['dotnet', 'test', '--project', projects[1], '-c', 'Release', '--no-build', '--no-restore',
             '--no-ansi', '--no-progress', '--minimum-expected-tests', '14', '--report-xunit-xml', '--results-directory', str(results)])
        executed = [t for p in results.glob('*.xunit.xml') for t in ET.parse(p).iter('test')]
        assert len(executed) == 14 and all(t.get('result') == 'Pass' for t in executed)
        run([sys.executable, '-B', 'docs/reports/M1-corpus-import/provision.py', '--preserve'])
        run([sys.executable, '-B', 'tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py'])
        discovery = json.loads(run([sys.executable, '-B', 'tools/ApiBaseline/discover.py']))
        assert discovery['apiDeclarations'] == 215
        assert discovery['fixtures'] == 3388
        assert discovery['corpusManifests'] == ['tests/corpora/bootstrap/manifest.json', 'tests/corpora/xml/manifest.json']
        # Preserved bootstrap discovery is checked independently with original frozen fragments.
        run(['dotnet', 'build', 'docs/reports/M0-corpora/Tests/Xemele.CorpusInfrastructure.Tests.csproj', '-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'])
        run(['dotnet', 'test', '--project', 'docs/reports/M0-corpora/Tests/Xemele.CorpusInfrastructure.Tests.csproj', '-c', 'Release', '--no-build', '--no-restore', '--no-ansi', '--no-progress', '--minimum-expected-tests', '24'])
        run([sys.executable, 'tools/planning/tasks.py', 'validate'])
        run(['git', 'diff', '--check'])
        owned = ('tests/corpora/xml/', 'docs/reports/M1-corpus-import/', 'docs/tasks/M1-corpus-import.json')
        paths = run(['git', 'diff', '--name-only', '7c44e76d1ba839b5c0c58dfad9791e3279bf793e']).splitlines()
        work = run(['git', 'diff', '--name-only']).splitlines()
        staged = run(['git', 'diff', '--cached', '--name-only']).splitlines()
        untracked = run(['git', 'ls-files', '--others', '--exclude-standard']).splitlines()
        for path in paths + work + staged + untracked:
            assert path.startswith(owned), 'Outside owned paths: ' + path
        LOG.append('Committed/working/staged/untracked path audit passed. No runtime/package changes. Public baseline: 215 rows.\n')
        LOG.append('Full suite files provisioned and resources preserved offline; 15 Python + 14 MTP full-inventory tests passed; bootstrap checks passed. Product cases executed: 0; standards passed: 0.\n')
    finally:
        REPORT.parent.mkdir(parents=True, exist_ok=True)
        REPORT.write_text('\n'.join(LOG))
        print('Transcript: ' + str(REPORT), flush=True)

if __name__ == '__main__': main()
