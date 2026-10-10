#!/usr/bin/env python3
"""Sequential checks at a committed revision; no timing or peer-report writes."""
import json
import argparse
import platform
from pathlib import Path
import subprocess
import tempfile
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'docs/reports/M1-pi-targets'
RAW = ROOT / 'artifacts/M1-pi-targets'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
LOG = []

def run(*args, cwd=ROOT):
    args = list(map(str, args))
    print('$ ' + ' '.join(args), flush=True)
    result = subprocess.run(args, cwd=cwd, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=240)
    LOG.extend(['$ ' + ' '.join(args), result.stdout, f'exit={result.returncode}'])
    if result.returncode:
        raise RuntimeError('\n'.join(LOG[-3:]))
    return result.stdout

def rows(path):
    lines = path.read_text().splitlines()
    assert lines[0] == 'xemele-api-v1'
    result = {}
    for line in lines[1:]:
        row = json.loads(line)
        assert row['member'] not in result
        result[row['member']] = row
    return result

def audit(base):
    task = json.loads((ROOT / 'docs/tasks/M1-pi-targets.json').read_text())
    paths = set(run('git', 'diff', '--name-only', base, 'HEAD').splitlines())
    paths.update(run('git', 'diff', '--name-only', 'HEAD').splitlines())
    paths.update(run('git', 'diff', '--cached', '--name-only').splitlines())
    paths.update(run('git', 'ls-files', '--others', '--exclude-standard').splitlines())
    for path in paths:
        assert path == 'docs/tasks/M1-pi-targets.json' or any(path.startswith(p) if p.endswith('/') else path == p for p in task['owns']), path
    run('git', 'diff', '--check', base, 'HEAD')
    run('git', 'diff', '--check')
    run('git', 'diff', '--cached', '--check')
    (REPORT / 'owned-path-audit.txt').write_text('Audit base ' + base + '\nSource revision ' + run('git', 'rev-parse', 'HEAD').strip() + '\nCommitted delta plus working/untracked paths all owned:\n' + '\n'.join(sorted(paths)) + '\n')

def main(args):
    RAW.mkdir(parents=True, exist_ok=True)
    LOG.append(f'Host {platform.platform()} {platform.machine()}')
    LOG.append('Source revision ' + run('git', 'rev-parse', 'HEAD').strip())
    audit(args.audit_base or json.loads((ROOT / 'docs/tasks/M1-pi-targets.json').read_text())['base_revision'])
    assert run('dotnet', '--version').strip() == json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    run('dotnet', '--info')
    if not args.contracts_only:
        run('dotnet', 'build', 'Xemele.slnx', *FLAGS)
        for name in ['PiTargets', 'Dom', 'Input', 'Syntax', 'Tree', 'DomConstruction', 'DomPersistence', 'NamespaceNames', 'Serialization']:
            output = run('dotnet', 'test', '--project', 'tests/Xemele.Tests', '-c', 'Release', '--no-build', '--no-restore', '--filter-namespace', 'Xemele.Tests.' + name)
            assert 'total: 0' not in output
        run('dotnet', 'test', '--solution', 'Xemele.slnx', '-c', 'Release', '--no-build', '--no-restore')
    run('dotnet', 'build', 'docs/reports/M0-contracts/Checks/Checks.csproj', *FLAGS)
    run('dotnet', 'test', '--project', 'docs/reports/M0-contracts/Checks/Checks.csproj', '-c', 'Release', '--no-build', '--no-restore')
    run('dotnet', 'build', 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', *FLAGS)
    checker = ROOT / 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
    for stage in ['M1', 'M2', 'ApiProbe']:
        run('dotnet', 'build', f'docs/reports/M0-contracts/Fixtures/{stage}/{stage}.csproj', *FLAGS)
        name = stage if stage == 'ApiProbe' else 'Foundation' + stage
        run('dotnet', checker, 'compare', ROOT / f'docs/reports/M0-contracts/Fixtures/{stage}/bin/Release/net10.0/{name}.dll', ROOT / f'tests/Contracts/Fixtures/foundation/{stage}.golden.txt')
    for lane in ['Common', 'Tree', 'Input', 'Syntax', 'Serializer', 'Dom', 'Protocol']:
        run('dotnet', 'build', f'docs/reports/M1-contracts/Projects/{lane}/{lane}.csproj', *FLAGS)
        dll = ROOT / f'docs/reports/M1-contracts/Projects/{lane}/bin/Release/net10.0/M1{lane}.dll'
        if lane == 'Protocol': run('dotnet', dll)
        else: run('dotnet', checker, 'compare', dll, ROOT / f'tests/Contracts/Fixtures/M1/{lane}.golden.txt')
    # Expected declarations derive only from independently approved models.
    dom = rows(ROOT / 'tests/Contracts/Fixtures/M1/Dom.golden.txt')
    serializer = rows(ROOT / 'tests/Contracts/Fixtures/M1/Serializer.golden.txt')
    expected = dict(dom)
    for member, row in serializer.items():
        if member in expected: assert expected[member] == row, member
        expected[member] = row
    assert len(dom) == 199 and len(serializer) == 215 and len(expected) == 222
    canonical = 'xemele-api-v1\n' + ''.join(json.dumps(expected[k], ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n' for k in sorted(expected))
    (REPORT / 'approved-union.txt').write_text(canonical)
    run('dotnet', checker, 'compare', ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll', REPORT / 'approved-union.txt')
    run('dotnet', checker, 'emit', ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll', REPORT / 'runtime-actual.txt')
    actual = rows(REPORT / 'runtime-actual.txt')
    factories = {k: v for k, v in actual.items() if k not in serializer}
    assert len(factories) == 7 and factories == rows(ROOT / 'docs/api/M1-dom.txt')
    assert all(dom[k] == v for k, v in factories.items())
    run('python3', '-B', 'tools/ApiBaseline/discover.py')
    run('python3', '-B', 'tools/ApiBaseline/compare.py', '--report', REPORT / 'runtime-api.json')
    run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tests/Xemele.TestUtilities/Build', '-p', 'test_*.py', '-v')
    run('python3', '-B', 'tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py')
    run('dotnet', 'build', 'docs/reports/M1-input/Xemele.InputBenchmarks.csproj', *FLAGS)
    with tempfile.TemporaryDirectory(prefix='xemele-dom-input-probe-') as directory:
        directory = Path(directory)
        destination = directory / 'docs/reports/M1-input/internal-contract.txt'
        destination.parent.mkdir(parents=True)
        run('dotnet', ROOT / 'docs/reports/M1-input/bin/Release/net10.0/Xemele.InputBenchmarks.dll', '--contract', ROOT / 'docs/reports/M1-contracts/Projects/Input/bin/Release/net10.0/M1Input.dll', cwd=directory)
        assert destination.read_bytes() == (ROOT / 'docs/reports/M1-input/internal-contract.txt').read_bytes()
        (REPORT / 'internal-input-abi.txt').write_bytes(destination.read_bytes())
    run('dotnet', 'build', 'docs/reports/M1-syntax/AbiProbe/AbiProbe.csproj', *FLAGS)
    run('dotnet', ROOT / 'docs/reports/M1-syntax/AbiProbe/bin/Release/net10.0/AbiProbe.dll', ROOT / 'docs/reports/M1-contracts/Projects/Syntax/bin/Release/net10.0/M1Syntax.dll', ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll', REPORT / 'internal-syntax-abi.txt')
    run('dotnet', 'build', 'samples/M1-dom/Sample.csproj', *FLAGS)
    run('dotnet', 'run', '--project', 'samples/M1-dom/Sample.csproj', '-c', 'Release', '--no-build', '--no-restore')
    run('dotnet', 'run', '--project', 'samples/M1-dom/Sample.csproj', '-c', 'Release', '--no-build', '--no-restore', '--', '--allocation')
    run('dotnet', 'pack', 'src/Xemele/Xemele.csproj', *FLAGS, '--no-build', '--no-restore', '-o', RAW / 'packages')
    package = next((RAW / 'packages').glob('Xemele.*.nupkg'))
    with zipfile.ZipFile(package) as archive:
        nuspec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        version = next(n.text for n in nuspec.iter() if n.tag.endswith('}version'))
        assert 'lib/net10.0/Xemele.dll' in archive.namelist()
        assert not any('Contracts' in name or 'Foundation' in name for name in archive.namelist())
    with tempfile.TemporaryDirectory(prefix='xemele-dom-consumer-') as directory:
        directory = Path(directory)
        (directory / 'Consumer.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="{version}" /></ItemGroup></Project>')
        (directory / 'Program.cs').write_text((ROOT / 'samples/M1-pi-targets/Program.cs').read_text())
        run('dotnet', 'run', '--project', directory / 'Consumer.csproj', '-c', 'Release', '--disable-build-servers', '-p:UseSharedCompilation=false', f'-p:RestoreSources={RAW / "packages"}', f'-p:RestorePackagesPath={directory / "isolated-packages"}')
    run('dotnet', 'build', 'docs/reports/M1-dom/Benchmarks.csproj', *FLAGS)
    run('dotnet', 'run', '--project', 'docs/reports/M1-dom/Benchmarks.csproj', '-c', 'Release', '--no-build', '--no-restore', '--', '--oracle')
    run('dotnet', 'build', 'docs/reports/M1-serialize/SerializerBenchmarks.csproj', *FLAGS)
    run('dotnet', 'run', '--project', 'docs/reports/M1-serialize/SerializerBenchmarks.csproj', '-c', 'Release', '--no-build', '--no-restore', '--', '--allocation')
    run('dotnet', 'build', 'samples/M1-pi-targets/Sample.csproj', *FLAGS)
    run('dotnet', 'run', '--project', 'samples/M1-pi-targets/Sample.csproj', '-c', 'Release', '--no-build', '--no-restore')
    run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tools/planning', '-p', 'test_tasks.py')
    run('python3', 'tools/planning/tasks.py', 'validate')
    audit(args.audit_base or json.loads((ROOT / 'docs/tasks/M1-pi-targets.json').read_text())['base_revision'])

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--contracts-only', action='store_true', help='Continue independent contract/sample/allocation/planning checks without repeating runtime suite.')
    parser.add_argument('--audit-base', help='Coordination revision before integration; default is current task base.')
    parser.add_argument('--report-prefix', help='New owned report/artifact subdirectory; preserves earlier report files.')
    args = parser.parse_args()
    if args.report_prefix:
        assert args.report_prefix.replace('-', '').isalnum(), 'Invalid report prefix'
        REPORT = REPORT / args.report_prefix
        RAW = RAW / args.report_prefix
        REPORT.mkdir(parents=True, exist_ok=True)
    try: main(args)
    finally:
        RAW.mkdir(parents=True, exist_ok=True)
        revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()[:12]
        attempt = 1
        while (RAW / f'checks-{revision}-{attempt}.txt').exists(): attempt += 1
        (RAW / f'checks-{revision}-{attempt}.txt').write_text('\n'.join(LOG))
        tokens = ('Source revision', 'Host ', '$ ', 'exit=', 'Build succeeded', 'Warning(s)', 'Error(s)', 'Matched ', 'total:', 'failed:', 'succeeded:', 'skipped:', 'Validated ', 'assertions', 'compatibility', ' B;', 'B/document', 'Independent DOM', 'Exact internal', 'matched:')
        (REPORT / 'checks.txt').write_text('\n'.join(line for block in LOG for line in block.splitlines() if any(token in line for token in tokens)) + '\n')
