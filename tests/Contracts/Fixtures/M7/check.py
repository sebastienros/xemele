#!/usr/bin/env python3
"""Compile contract declarations; execute only already implemented XML probes."""
import argparse, subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[3]
def run(args, expected=0):
 result=subprocess.run(args,cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 assert result.returncode==expected,result.stdout
 return result.stdout
assert run([a.dotnet,'--version']).strip()=='10.0.400'
assert 'Microsoft.NETCore.App 10.0.11 ' in run([a.dotnet,'--list-runtimes'])
for lane in ['Engine','Tokenizer','Selector']:
 project=here/lane/(lane+'.csproj')
 assert '<IsPackable>false</IsPackable>' in project.read_text()
 run([a.dotnet,'restore',str(project),'--source',str(here),'-p:NuGetAudit=false','--verbosity','quiet'])
 run([a.dotnet,'build',str(project),'-c','Release','--no-restore','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','--disable-build-servers','--verbosity','quiet'])
 output=run([a.dotnet,'--fx-version','10.0.11',str(project.parent/'bin/Release/net10.0'/ (lane+'.dll'))])
 assert 'runtime 10.0.11' in output
 print(output.strip(),flush=True)
owned=lambda s: s in ['docs/specs/html.md','docs/tasks/M7-contracts.json'] or any(s.startswith(x) for x in ['tests/Contracts/Fixtures/M7/','docs/decisions/M7/','docs/reports/M7-contracts/'])
for args in [['git','diff','--name-only','9e530764937150f85998d5db7807fd6581d9d425'],['git','ls-files','--others','--exclude-standard']]:
 assert all(owned(s) for s in run(args).splitlines())
print('PASS: owned changes only; no shared runtime/build changes; three independent nonpackaged declaration consumers')

checker_project=root/'docs/reports/M7-contracts/ApiCheck/ApiCheck.csproj'
run([a.dotnet,'restore',str(checker_project),'--source',str(here),'-p:NuGetAudit=false','--verbosity','quiet'])
run([a.dotnet,'build',str(checker_project),'-c','Release','--no-restore','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','--disable-build-servers','--verbosity','quiet'])
checker=checker_project.parent/'bin/Release/net10.0/ApiCheck.dll'
import tempfile
for lane in ['Engine','Tokenizer','Selector']:
 assembly=here/lane/'bin/Release/net10.0'/(lane+'.dll');baseline=here/(lane+'.golden.txt')
 output=run([a.dotnet,'--fx-version','10.0.11',str(checker),'compare',str(assembly),str(baseline)])
 print('PASS: '+lane+' '+output.strip(),flush=True)
 with tempfile.TemporaryDirectory(prefix='m7-api-control-') as temp:
  bad=Path(temp)/'bad.txt';bad.write_text('\n'.join(baseline.read_text().splitlines()[:-1])+'\n')
  output=run([a.dotnet,'--fx-version','10.0.11',str(checker),'compare',str(assembly),str(bad)],expected=1)
  assert 'API mismatch' in output
print('PASS: three missing-member API negative controls')
