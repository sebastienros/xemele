"""Correctness/API/consumer checks only; excludes all allocation test methods."""
import json, subprocess, sys, xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(*args):subprocess.run(args,cwd=root,check=True)
def build(project,*extra):
 run(net,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags,*extra)
 run(net,'build',project,'-c','Release','--no-restore',*flags,*extra)
if '--reuse-tests' not in sys.argv: build('tests/Xemele.Tests/Xemele.Tests.csproj')
classes=['Xemele.Tests.M8ConstraintLexical.*','Xemele.Tests.M8Facets.*','Xemele.Tests.M8Datatypes.*','Xemele.Tests.M8DatatypesCore.*','Xemele.Tests.M8DatatypesNumeric.*','Xemele.Tests.M8DatatypesTextBinary.*','Xemele.Tests.M8DatatypesTemporal.*']
args=[]
for name in classes:args+=['-class',name]
out=root/'artifacts/M8-datatype-constraint-lexical/correctness.xml';out.parent.mkdir(parents=True,exist_ok=True)
if '--reuse-tests' not in sys.argv: run(net,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll',*args,'-method-','*WarmBorrowed*','-result-xml',str(out))
r=ET.parse(out).getroot();a=r.find('assembly')
assert all(int(a.attrib[k])==0 for k in ['failed','errors','skipped','not-run']),a.attrib
assert int(a.attrib['passed'])==731 and '10.0.11' in a.attrib['environment'],a.attrib
own=sum(t.attrib['type'].startswith('Xemele.Tests.M8ConstraintLexical.') for t in r.iter('test'))
assert own==86,own
print('ASSERTED own',own,'affected total',a.attrib['passed'])
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj');run('python3','docs/reports/M5-validation-model/check_api.py')
build('docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj')
actual=root/'artifacts/M8-datatype-constraint-lexical/runtime-internal.txt'
run(net,'--fx-version','10.0.11','docs/reports/M8-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll','emit','src/Xemele/bin/Release/net10.0/Xemele.dll',str(actual))
def rows(path):return {r['id']:r for r in (json.loads(line) for line in path.read_text().splitlines()[1:])}
runtime=rows(actual)
for name in ['Datatypes','Facets']:
 baseline=rows(root/('tests/Contracts/Fixtures/M8/'+name+'.golden.txt'))
 assert all(key in runtime and row==runtime[key] for key,row in baseline.items()),name
 print('PASS frozen',name,len(baseline))
consumer='docs/reports/M8-datatype-constraint-lexical/Consumer.csproj'
for extra in [[],['-p:ConstraintXmlOnly=true']]:
 build(consumer,*extra)
 run(net,'--fx-version','10.0.11','docs/reports/M8-datatype-constraint-lexical/bin/Release/net10.0/Xemele.Benchmarks.dll')
run('python3','tools/planning/tasks.py','validate');run('git','diff','--check')
