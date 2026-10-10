#!/usr/bin/env python3
"""Owned syntax verification; never changes peer reports, models or API fragments."""
import json
import os
import platform
from pathlib import Path
import subprocess
import tempfile
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'docs/reports/M1-syntax'
RAW = ROOT / 'artifacts/M1-syntax'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
LOG = []
def run(*args):
    args = list(map(str, args))
    print('$ ' + ' '.join(args), flush=True)
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    LOG.extend(['$ ' + ' '.join(args), result.stdout, f'exit={result.returncode}'])
    if result.returncode:
        raise RuntimeError('\n'.join(LOG[-3:]))
    return result.stdout

def main():
    RAW.mkdir(parents=True, exist_ok=True)
    LOG.append(f'Host {platform.platform()} {platform.machine()}')
    LOG.append('Implementation revision ' + run('git', 'rev-parse', 'HEAD').strip())
    assert run('dotnet', '--version').strip() == json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    run('dotnet', '--info')
    run('dotnet', 'build', 'Xemele.slnx', *FLAGS)
    run('dotnet', 'test', '--project', 'tests/Xemele.Tests', '-c', 'Release', '--no-build', '--no-restore', '--filter-class', 'Xemele.Tests.Syntax.*')
    run('dotnet', 'test', '--solution', 'Xemele.slnx', '-c', 'Release', '--no-build', '--no-restore')
    run('dotnet', 'build', 'docs/reports/M0-contracts/Checks/Checks.csproj', *FLAGS)
    run('dotnet', 'test', '--project', 'docs/reports/M0-contracts/Checks/Checks.csproj', '-c', 'Release', '--no-build', '--no-restore')
    checker = ROOT / 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
    run('dotnet', 'build', 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', *FLAGS)
    for stage in ['M1', 'M2']:
        run('dotnet', checker, 'compare', ROOT / f'docs/reports/M0-contracts/Fixtures/{stage}/bin/Release/net10.0/Foundation{stage}.dll', ROOT / f'tests/Contracts/Fixtures/foundation/{stage}.golden.txt')
    run('dotnet', checker, 'compare', ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll', ROOT / 'tests/Contracts/Fixtures/M1/Common.golden.txt')
    for lane in ['Common', 'Tree', 'Input', 'Syntax', 'Serializer', 'Dom', 'Protocol']:
        run('dotnet', 'build', f'docs/reports/M1-contracts/Projects/{lane}/{lane}.csproj', *FLAGS)
        dll = ROOT / f'docs/reports/M1-contracts/Projects/{lane}/bin/Release/net10.0/M1{lane}.dll'
        if lane == 'Protocol':
            run('dotnet', dll)
        else:
            run('dotnet', checker, 'compare', dll, ROOT / f'tests/Contracts/Fixtures/M1/{lane}.golden.txt')
    run('python3', '-B', 'tools/ApiBaseline/discover.py')
    run('python3', '-B', 'tools/ApiBaseline/compare.py', '--report', REPORT / 'runtime-api.json')
    run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tests/Xemele.TestUtilities/Build', '-p', 'test_*.py', '-v')
    run('python3', '-B', 'tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py')
    run('dotnet', 'run', '--project', 'samples/M1-syntax/Sample.csproj', '-c', 'Release', '--disable-build-servers', '-p:UseSharedCompilation=false')
    run('dotnet', 'build', 'docs/reports/M1-syntax/AbiProbe/AbiProbe.csproj', *FLAGS)
    run('dotnet', ROOT / 'docs/reports/M1-syntax/AbiProbe/bin/Release/net10.0/AbiProbe.dll', ROOT / 'docs/reports/M1-contracts/Projects/Syntax/bin/Release/net10.0/M1Syntax.dll', ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll', REPORT / 'internal-abi.txt')
    run('dotnet', 'pack', 'src/Xemele/Xemele.csproj', *FLAGS, '--no-build', '--no-restore', '-o', 'artifacts/M1-syntax/packages')
    packages = ROOT / 'artifacts/M1-syntax/packages'
    package = next(packages.glob('Xemele.*.nupkg'))
    with zipfile.ZipFile(package) as archive:
        nuspec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        version = next(n.text for n in nuspec.iter() if n.tag.endswith('}version'))
        shipped = archive.namelist()
        assert 'lib/net10.0/Xemele.dll' in shipped
        assert not any('Foundation' in n or 'Contracts' in n for n in shipped)
    with tempfile.TemporaryDirectory(prefix='xemele-syntax-package-') as directory:
        directory = Path(directory)
        (directory / 'Consumer.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Xemele.SyntaxSample</AssemblyName><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="{version}" /></ItemGroup></Project>')
        (directory / 'Program.cs').write_text((ROOT / 'samples/M1-syntax/Program.cs').read_text())
        run('dotnet', 'run', '--project', directory / 'Consumer.csproj', '-c', 'Release', '--disable-build-servers', '-p:UseSharedCompilation=false', f'-p:RestoreSources={packages}', f'-p:RestorePackagesPath={directory / "packages"}')
    run('dotnet', 'build', 'docs/reports/M1-syntax/Xemele.SyntaxBenchmarks.csproj', *FLAGS)
    run('dotnet', 'run', '--project', 'docs/reports/M1-syntax/Xemele.SyntaxBenchmarks.csproj', '-c', 'Release', '--no-build', '--', '--allocation')
    run('python3', 'tools/planning/tasks.py', 'validate')
    run('git', 'diff', '--check')

if __name__ == '__main__':
    try:
        main()
    finally:
        RAW.mkdir(parents=True, exist_ok=True)
        revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()[:12]
        attempt = 1
        while (RAW / f'checks-{revision}-{attempt}.txt').exists():
            attempt += 1
        path = RAW / f'checks-{revision}-{attempt}.txt'
        path.write_text('\n'.join(LOG))
        print('Raw checks: ' + str(path), flush=True)
