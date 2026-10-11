#!/usr/bin/env python3
"""Verify ordered PI declaration extension and exact owned compatibility records."""
import argparse,json,os,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);p.add_argument('--ownership-base',required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[3]
def run(args,expected=0,env=None):
 r=subprocess.run(args,cwd=root,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 assert r.returncode==expected,r.stdout
 return r.stdout
rows=lambda s:{r['id']:r for r in map(json.loads,s.splitlines()[1:])}
added={'F:Xemele.Html.HtmlNodeKind.ProcessingInstruction','F:Xemele.Html.HtmlTokenKind.ProcessingInstruction','M:Xemele.Html.HtmlDocument.CreateProcessingInstruction(System.String,System.String)'}
for lane in ['Engine','Tokenizer','Selector']:
 path='tests/Contracts/Fixtures/M7/'+lane+'.golden.txt';before=rows(run(['git','show',a.ownership_base+':'+path]));after=rows((root/path).read_text())
 assert set(after)-set(before)==added,(lane,set(after)-set(before))
 assert all(after[k]==v for k,v in before.items()),lane
print('PASS: exactly three additive members per declaration family, all prior members/numeric enum metadata unchanged')
# Canonical checker owns the original task only: use current source HEAD there,
# then independently audit the entire current-task claim-to-handoff diff below.
env=os.environ.copy();env['NoWarn']='CS0436'
print(run(['python3','tests/Contracts/Fixtures/M7/check.py','--dotnet',a.dotnet,'--ownership-base',run(['git','rev-parse','HEAD']).strip()],env=env).strip())
checker=root/'docs/reports/M7-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
for lane in ['Engine','Tokenizer','Selector']:
 assembly=root/'tests/Contracts/Fixtures/M7'/lane/'bin/Release/net10.0'/(lane+'.dll')
 baseline=root/'tests/Contracts/Fixtures/M7'/(lane+'.golden.txt')
 with tempfile.TemporaryDirectory(prefix='m7-pi-control-') as d:
  for member in added:
   bad=Path(d)/'missing-pi.txt';bad.write_text('\n'.join(line for line in baseline.read_text().splitlines() if not (line.startswith('{') and json.loads(line)['id']==member))+'\n')
   assert 'API mismatch' in run([a.dotnet,'--fx-version','10.0.11',str(checker),'compare',str(assembly),str(bad)],expected=1)
print('PASS: nine PI-specific missing-member baseline negative controls')
project=root/'docs/reports/M7-html-pi-contract/ActualModels/ActualModels.csproj'
assert 'Model.cs.fixture' not in project.read_text()
run([a.dotnet,'restore',str(project),'--source',str(root/'tests/Contracts/Fixtures/M7'),'-p:NuGetAudit=false','--verbosity','quiet'])
print(run([a.dotnet,'build',str(project),'-c','Release','--no-restore','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','--disable-build-servers','--verbosity','quiet']).strip())
output=run([a.dotnet,'--fx-version','10.0.11',str(project.parent/'bin/Release/net10.0/ActualModels.dll')]);assert 'actual HTML and XML model assembly identity' in output and 'runtime 10.0.11' in output;print(output.strip())
task=json.loads((root/'docs/tasks/M7-html-pi-contract.json').read_text())
def owned(path):return path=='docs/tasks/M7-html-pi-contract.json' or any(path.startswith(v) if v.endswith('/') else path==v for v in task['owns'])
for command in [['git','diff','--name-only',a.ownership_base],['git','ls-files','--others','--exclude-standard']]:
 paths=run(command).splitlines();assert all(owned(v) for v in paths),paths
run(['git','diff','--check',a.ownership_base]);print('PASS: strict complete owned-path audit from claim including untracked files; diff whitespace')
compat=json.loads((root/'docs/reports/M7-html-pi-contract/legacy-vectors.json').read_text())
for vector in compat['vectors']:
 if 'managedTree' in vector:
  from copy import deepcopy
  expected=deepcopy(vector['tree']);expected[0]['children'].extend(expected[1]['children']);expected.pop(1)
  assert expected==vector['managedTree'] and vector['adaptation']=='legacy-root-envelope-v1'
pi=json.loads((root/'docs/reports/M7-html-pi-contract/pi-corpus.json').read_text());assert pi['discrepancies']==[] and pi['exclusions']==[]
print('PASS: exact named native-to-managed envelope vectors; PI corpus has no discrepancies/exclusions (record validation, no runtime parser claim)')
print(run(['python3','tools/planning/tasks.py','validate']).strip())
