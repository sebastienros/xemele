"""Correctness only: no allocation counters, timing or benchmark execution."""
import subprocess, xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(*args): subprocess.run(args,cwd=root,check=True)
def build(project,*extra):
 run(net,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags,*extra)
 run(net,'build',project,'-c','Release','--no-restore',*flags,*extra)
build('tests/Xemele.Tests/Xemele.Tests.csproj')
out=root/'artifacts/M8-datatypes/correctness/focused.xml';out.parent.mkdir(parents=True,exist_ok=True)
run(net,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-class','Xemele.Tests.M8Datatypes.CompositionTests','-class','Xemele.Tests.M8DatatypesTextBinary.FamilyTests','-class','Xemele.Tests.M8DatatypesNumeric.NumericTests','-class','Xemele.Tests.M8DatatypesTemporal.TemporalTests','-class','Xemele.Tests.M8DatatypesCore.CoreTests','-class','Xemele.Tests.M8Model.ModelTests','-result-xml',str(out))
r=ET.parse(out).getroot();a=r.find('assembly')
assert int(a.attrib['passed'])==353 and all(int(a.attrib[k])==0 for k in ['failed','errors','skipped','not-run']),a.attrib
for name,count in [('M8Datatypes.CompositionTests',76),('M8DatatypesNumeric.NumericTests',76),('M8DatatypesTemporal.TemporalTests',23),('M8DatatypesTextBinary.FamilyTests',148),('M8DatatypesCore.CoreTests',18),('M8Model.ModelTests',12)]:
 assert sum(t.attrib['type']=='Xemele.Tests.'+name for t in r.iter('test'))==count
assert '10.0.11' in a.attrib['environment']
print('ASSERTED:76 composition+277 verified child/model regressions=353 pass; no allocation tests.')
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj')
run('python3','docs/reports/M5-validation-model/check_api.py')
build('docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj')
run('python3','docs/reports/M8-datatypes/frozen.py')
consumer='docs/reports/M8-datatypes/Consumer.csproj'
build(consumer)
run(net,'--fx-version','10.0.11','docs/reports/M8-datatypes/bin/Release/net10.0/Xemele.Benchmarks.dll')
build(consumer,'-p:DatatypesXmlOnly=true')
run(net,'--fx-version','10.0.11','docs/reports/M8-datatypes/bin/Release/net10.0/Xemele.Benchmarks.dll','--xml-only')
run('python3','tools/planning/tasks.py','validate')
run('git','diff','--check')
