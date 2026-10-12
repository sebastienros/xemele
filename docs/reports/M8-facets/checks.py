"""Correctness only: no allocation assertions/counters or timing."""
import subprocess, xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(*args):subprocess.run(args,cwd=root,check=True)
def build(project,*extra):
 run(net,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags,*extra)
 run(net,'build',project,'-c','Release','--no-restore',*flags,*extra)
build('tests/Xemele.Tests/Xemele.Tests.csproj')
classes=['M8Facets.FacetTests','M8Facets.DefinitionTests','M8Facets.CompositeTests','M8Facets.OperationTests','M8Facets.PrimitiveTests','M8Facets.ApplicabilityTests','M8Facets.CallerEngineTests','M8Datatypes.CompositionTests','M8DatatypesTextBinary.FamilyTests','M8DatatypesNumeric.NumericTests','M8DatatypesTemporal.TemporalTests','M8DatatypesCore.CoreTests','M8Model.ModelTests','M8Regex.RegexTests']
counts=[56,48,61,15,7,83,2,77,148,76,23,18,12,147] # exact owned counts refreshed only after actual results
out=root/'artifacts/M8-facets/correctness/focused.xml';out.parent.mkdir(parents=True,exist_ok=True)
arguments=[]
for name in classes:arguments+=['-class','Xemele.Tests.'+name]
run(net,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll',*arguments,'-result-xml',str(out))
r=ET.parse(out).getroot();a=r.find('assembly')
assert all(int(a.attrib[k])==0 for k in ['failed','errors','skipped','not-run']),a.attrib
actual={name:sum(t.attrib['type']=='Xemele.Tests.'+name for t in r.iter('test')) for name in classes}
assert list(actual.values())==counts,actual
assert int(a.attrib['passed'])==sum(actual.values()) and '10.0.11' in a.attrib['environment'],a.attrib
print('ASSERTED actual class counts:',actual)
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj');run('python3','docs/reports/M5-validation-model/check_api.py')
build('docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj');run('python3','docs/reports/M8-facets/frozen.py')
consumer='docs/reports/M8-facets/Consumer.csproj'
build(consumer);run(net,'--fx-version','10.0.11','docs/reports/M8-facets/bin/Release/net10.0/Xemele.Benchmarks.dll')
build(consumer,'-p:FacetsXmlOnly=true');run(net,'--fx-version','10.0.11','docs/reports/M8-facets/bin/Release/net10.0/Xemele.Benchmarks.dll','--xml-only')
run('python3','tools/planning/tasks.py','validate');run('git','diff','--check')
