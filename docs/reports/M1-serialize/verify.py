#!/usr/bin/env python3
"""Known-revision correctness/API/package checks; timings require root grant."""
from pathlib import Path
import json, platform, subprocess, tempfile, zipfile
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
REPORT=ROOT/'docs/reports/M1-serialize'
ARTIFACT=ROOT/'artifacts/M1-serialize'
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
LOG=[]
def run(*args):
    args=list(map(str,args)); print('$ '+' '.join(args),flush=True)
    r=subprocess.run(args,cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    LOG.extend(['$ '+' '.join(args),r.stdout,f'exit={r.returncode}'])
    if r.returncode:raise RuntimeError(r.stdout)
    return r.stdout

def main():
    ARTIFACT.mkdir(parents=True,exist_ok=True)
    LOG.append(f'Host {platform.platform()} {platform.machine()}')
    LOG.append('Source revision '+run('git','rev-parse','HEAD').strip())
    assert run('dotnet','--version').strip()=='10.0.400'
    run('dotnet','--info')
    run('dotnet','build','Xemele.slnx',*FLAGS)
    run('dotnet','test','--project','tests/Xemele.Tests','-c','Release','--no-build','--no-restore','--filter-class','Xemele.Tests.Serialization.SerializerTests')
    run('dotnet','test','--solution','Xemele.slnx','-c','Release','--no-build','--no-restore')
    run('dotnet','build','docs/reports/M0-contracts/Checks/Checks.csproj',*FLAGS)
    run('dotnet','test','--project','docs/reports/M0-contracts/Checks/Checks.csproj','-c','Release','--no-build','--no-restore')
    checker=ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
    run('dotnet','build','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj',*FLAGS)
    assembly=ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll'
    run('dotnet',checker,'compare',assembly,ROOT/'tests/Contracts/Fixtures/M1/Serializer.golden.txt')
    run('dotnet',checker,'emit',assembly,ARTIFACT/'actual-api.txt')
    prior={json.loads(s)['member'] for s in (ROOT/'tests/Contracts/Fixtures/M1/Tree.golden.txt').read_text().splitlines()[1:]}
    actual=(ARTIFACT/'actual-api.txt').read_text().splitlines()[1:]
    fragment='xemele-api-v1\n'+'\n'.join(s for s in actual if json.loads(s)['member'] not in prior)+'\n'
    assert fragment==(ROOT/'docs/api/M1-serialize.txt').read_text()
    LOG.append(f'Actual compiler extraction: {len(actual)} members; preserved {len(prior)} Common/Tree; new fragment 23')
    for stage in ['M1','M2']:
        run('dotnet','build',f'docs/reports/M0-contracts/Fixtures/{stage}/{stage}.csproj',*FLAGS)
        run('dotnet',checker,'compare',ROOT/f'docs/reports/M0-contracts/Fixtures/{stage}/bin/Release/net10.0/Foundation{stage}.dll',ROOT/f'tests/Contracts/Fixtures/foundation/{stage}.golden.txt')
    for lane in ['Common','Tree','Input','Syntax','Serializer','Dom','Protocol']:
        run('dotnet','build',f'docs/reports/M1-contracts/Projects/{lane}/{lane}.csproj',*FLAGS)
        dll=ROOT/f'docs/reports/M1-contracts/Projects/{lane}/bin/Release/net10.0/M1{lane}.dll'
        if lane=='Protocol':run('dotnet',dll)
        else:run('dotnet',checker,'compare',dll,ROOT/f'tests/Contracts/Fixtures/M1/{lane}.golden.txt')
    run('python3','-B','tools/ApiBaseline/discover.py')
    run('python3','-B','tools/ApiBaseline/compare.py','--report',REPORT/'runtime-api.json')
    run('dotnet','run','--project','samples/M1-serialize/Sample.csproj','-c','Release','--disable-build-servers','-p:UseSharedCompilation=false')
    run('dotnet','pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','--no-restore','-o',ARTIFACT/'packages')
    package=next((ARTIFACT/'packages').glob('Xemele.*.nupkg'))
    with zipfile.ZipFile(package) as archive:
        ns=ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        version=next(n.text for n in ns.iter() if n.tag.endswith('}version'))
        assert 'lib/net10.0/Xemele.dll' in archive.namelist()
        assert not any('Foundation' in n or 'Contracts' in n for n in archive.namelist())
    with tempfile.TemporaryDirectory(prefix='xemele-serialize-package-') as directory:
        directory=Path(directory)
        (directory/'Consumer.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="{version}" /></ItemGroup></Project>')
        (directory/'Program.cs').write_text((ROOT/'samples/M1-serialize/Program.cs').read_text())
        run('dotnet','run','--project',directory/'Consumer.csproj','-c','Release','--disable-build-servers','-p:UseSharedCompilation=false',f'-p:RestoreSources={ARTIFACT / "packages"}',f'-p:RestorePackagesPath={directory / "isolated-packages"}')
    run('dotnet','build','docs/reports/M1-serialize/SerializerBenchmarks.csproj',*FLAGS)
    run('dotnet','run','--project','docs/reports/M1-serialize/SerializerBenchmarks.csproj','-c','Release','--no-build','--','--allocation')
    run('python3','tools/planning/tasks.py','validate')
    run('git','diff','--check')
if __name__=='__main__':
    try:main()
    finally:
        ARTIFACT.mkdir(parents=True,exist_ok=True)
        (ARTIFACT/'verification-raw.txt').write_text('\n'.join(LOG))
        tokens=('Source revision','Host ','$ ','exit=','Build succeeded','Warning(s)','Error(s)','Matched ','total:','failed:','succeeded:','skipped:','Validated ','assertions','bytes','retained','Actual compiler','Independent construction','compatibility')
        (REPORT/'checks.txt').write_text('\n'.join(line for block in LOG for line in block.splitlines() if any(t in line for t in tokens))+'\n')
