#!/usr/bin/env python3
"""Sequential correctness checks; no timings, baseline rewrites or peer reports."""
import argparse
import json
import os
from pathlib import Path
import platform
import subprocess
import tempfile
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'docs/reports/M1-tree-dom-persistence'
ARTIFACT = ROOT / 'artifacts/M1-tree-dom-persistence'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
LOG = []

def run(*args):
    args = list(map(str, args))
    print('$ ' + ' '.join(args), flush=True)
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=180)
    LOG.extend(['$ ' + ' '.join(args), result.stdout, f'exit={result.returncode}'])
    if result.returncode:
        raise RuntimeError('\n'.join(LOG[-3:]))
    return result.stdout

def audit_paths(audit_base=None):
    task = json.loads((ROOT / 'docs/tasks/M1-tree-dom-persistence.json').read_text())
    audit_base = audit_base or task['base_revision']
    changed = run('git', 'diff', '--name-only', audit_base, 'HEAD').splitlines()
    changed += run('git', 'diff', '--name-only', 'HEAD').splitlines()
    changed += run('git', 'ls-files', '--others', '--exclude-standard').splitlines()
    allowed = task['owns'] + ['docs/tasks/M1-tree-dom-persistence.json']
    assert all(any(path.startswith(owner) if owner.endswith('/') else path == owner for owner in allowed) for path in changed), changed
    LOG.append('Owned-path audit passed at base ' + audit_base)
    run('git', 'diff', '--check', audit_base, 'HEAD')
    run('git', 'diff', '--check')

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--public-golden', default='tests/Contracts/Fixtures/M1/Serializer.golden.txt')
    parser.add_argument('--audit-base', help='Pre-merge coordination revision for the integration delta; defaults to the original task base')
    args = parser.parse_args()
    ARTIFACT.mkdir(parents=True, exist_ok=True)
    LOG.append(f'Host {platform.platform()} {platform.machine()}')
    LOG.append('Source revision ' + run('git', 'rev-parse', 'HEAD').strip())
    assert run('dotnet', '--version').strip() == json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    run('dotnet', '--info')
    source = (ROOT / 'src/Xemele/Tree/DomPersistence.cs').read_text()
    assert 'TreeValidation.' not in source and 'for (' not in source and 'while (' not in source
    LOG.append('Trusted boundary source audit: no validation calls or custom scan loops')
    run('dotnet', 'build', 'Xemele.slnx', *FLAGS)
    for option, name in [('--filter-class', 'DomPersistence.DomPersistenceTests'), ('--filter-class', 'DomConstruction.DomBuilderTests'), ('--filter-class', 'Tree.TreeTests'), ('--filter-class', 'Input.InputTests'), ('--filter-namespace', 'Syntax')]:
        output = run('dotnet', 'test', '--project', 'tests/Xemele.Tests', '-c', 'Release', '--no-build', '--no-restore', option, 'Xemele.Tests.' + name)
        assert 'total: 0' not in output, name
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
    run('dotnet', checker, 'compare', ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll', ROOT / args.public_golden)
    run('python3', '-B', 'tools/ApiBaseline/discover.py')
    run('python3', '-B', 'tools/ApiBaseline/compare.py', '--report', REPORT / 'runtime-api.json')
    run('dotnet', 'build', 'samples/M1-tree-dom-persistence/Sample.csproj', *FLAGS)
    run('dotnet', 'run', '--project', 'samples/M1-tree-dom-persistence/Sample.csproj', '-c', 'Release', '--no-build', '--no-restore')
    run('dotnet', 'run', '--project', 'samples/M1-tree-dom-persistence/Sample.csproj', '-c', 'Release', '--no-build', '--no-restore', '--', '--allocation')
    run('dotnet', 'pack', 'src/Xemele/Xemele.csproj', *FLAGS, '--no-build', '--no-restore', '-o', ARTIFACT / 'packages')
    package = next((ARTIFACT / 'packages').glob('Xemele.*.nupkg'))
    with zipfile.ZipFile(package) as archive:
        nuspec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        version = next(n.text for n in nuspec.iter() if n.tag.endswith('}version'))
        assert 'lib/net10.0/Xemele.dll' in archive.namelist()
    with tempfile.TemporaryDirectory(prefix='xemele-builder-consumer-') as directory:
        directory = Path(directory)
        (directory / 'Consumer.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><AssemblyName>Xemele.DomPersistenceSample</AssemblyName><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="{version}" /></ItemGroup></Project>')
        (directory / 'Program.cs').write_text((ROOT / 'samples/M1-tree-dom-persistence/Program.cs').read_text())
        (directory / 'AllocationProbe.cs').write_text((ROOT / 'samples/M1-tree-dom-persistence/AllocationProbe.cs').read_text())
        run('dotnet', 'run', '--project', directory / 'Consumer.csproj', '-c', 'Release', '--disable-build-servers', '-p:UseSharedCompilation=false', f'-p:RestoreSources={ARTIFACT / "packages"}', f'-p:RestorePackagesPath={directory / "isolated-packages"}')
    run('dotnet', 'build', 'benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj', *FLAGS)
    run('dotnet', 'build', 'docs/reports/M1-tree-dom-persistence/Oracle/Oracle.csproj', *FLAGS)
    run('dotnet', 'run', '--project', 'docs/reports/M1-tree-dom-persistence/Oracle/Oracle.csproj', '-c', 'Release', '--no-build', '--no-restore')
    audit_paths(args.audit_base)
    run('python3', 'tools/planning/tasks.py', 'validate')

if __name__ == '__main__':
    try: main()
    finally:
        ARTIFACT.mkdir(parents=True, exist_ok=True)
        (ARTIFACT / 'verification-raw.txt').write_text('\n'.join(LOG))
        tokens = ('Trusted boundary', 'Owned-path', 'Complete checked', 'Source revision', 'Host ', '$ ', 'exit=', 'Build succeeded', 'Warning(s)', 'Error(s)', 'Matched ', 'total:', 'failed:', 'succeeded:', 'skipped:', 'Validated ', 'assertions', 'compatibility', ' B;', 'Independent validated', 'catalog:')
        (REPORT / 'checks.txt').write_text('\n'.join(line for block in LOG for line in block.splitlines() if any(token in line for token in tokens)) + '\n')
