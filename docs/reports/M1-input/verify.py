#!/usr/bin/env python3
"""Checks known input revision; never writes shared/peer reports."""
import subprocess, pathlib, json, platform
ROOT=pathlib.Path(__file__).resolve().parents[3]
REPORT=ROOT/'docs/reports/M1-input'
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
LOG=[]
def run(*args):
    args=list(map(str,args)); print('$ '+' '.join(args),flush=True)
    p=subprocess.run(args,cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    LOG.extend(['$ '+' '.join(args),p.stdout,f'exit={p.returncode}'])
    if p.returncode: raise RuntimeError(p.stdout)
    return p.stdout

def main():
    LOG.append(f'Host {platform.platform()} {platform.machine()}')
    LOG.append('Source revision '+run('git','rev-parse','HEAD').strip())
    assert run('dotnet','--version').strip()==json.loads((ROOT/'global.json').read_text())['sdk']['version']
    run('dotnet','--info')
    run('dotnet','build','Xemele.slnx',*FLAGS)
    run('dotnet','test','--project','tests/Xemele.Tests','-c','Release','--no-build','--no-restore','--filter-class','Xemele.Tests.Input.InputTests')
    run('dotnet','test','--solution','Xemele.slnx','-c','Release','--no-build','--no-restore')
    run('dotnet','build','docs/reports/M0-contracts/Checks/Checks.csproj',*FLAGS)
    run('dotnet','test','--project','docs/reports/M0-contracts/Checks/Checks.csproj','-c','Release','--no-build','--no-restore')
    checker=ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
    run('dotnet','build','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj',*FLAGS)
    run('dotnet',checker,'compare',ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll',ROOT/'tests/Contracts/Fixtures/M1/Common.golden.txt')
    for lane in ['Common','Tree','Input','Syntax','Serializer','Dom','Protocol']:
        run('dotnet','build',f'docs/reports/M1-contracts/Projects/{lane}/{lane}.csproj',*FLAGS)
        dll=ROOT/f'docs/reports/M1-contracts/Projects/{lane}/bin/Release/net10.0/M1{lane}.dll'
        if lane=='Protocol':run('dotnet',dll)
        else:run('dotnet',checker,'compare',dll,ROOT/f'tests/Contracts/Fixtures/M1/{lane}.golden.txt')
    run('python3','-B','tools/ApiBaseline/discover.py')
    run('python3','-B','tools/ApiBaseline/compare.py','--report',REPORT/'runtime-api.json')
    run('python3','-B','-m','unittest','discover','-s','tests/Xemele.TestUtilities/Build','-p','test_*.py','-v')
    run('python3','-B','tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py')
    run('dotnet','run','--project','samples/M1-input/Sample.csproj',*FLAGS)
    run('dotnet','pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','--no-restore','-o','artifacts/M1-input/packages')
    run('dotnet','build','docs/reports/M1-input/Xemele.InputBenchmarks.csproj',*FLAGS)
    run('dotnet','run','--project','docs/reports/M1-input/Xemele.InputBenchmarks.csproj','-c','Release','--no-build','--','--contract',ROOT/'docs/reports/M1-contracts/Projects/Input/bin/Release/net10.0/M1Input.dll')
    run('dotnet','run','--project','docs/reports/M1-input/Xemele.InputBenchmarks.csproj','-c','Release','--no-build','--','--allocation')
    run('python3','tools/planning/tasks.py','validate')
    run('git','diff','--check')
if __name__=='__main__':
    try:main()
    finally:(REPORT/'checks.txt').write_text('\n'.join(LOG))
