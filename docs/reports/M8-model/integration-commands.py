import json,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
dotnet='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
projects=['tests/Xemele.Tests/Xemele.Tests.csproj','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj','docs/reports/M8-model/ApiCheck/ApiCheck.csproj','samples/M8-model/Model.Consumer.csproj']
common=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
for project in projects:
 subprocess.run([dotnet,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*common],check=True)
 subprocess.run([dotnet,'build',project,'-c','Release','--no-restore',*common],check=True)
out=Path('artifacts/M8-model/integration');out.mkdir(parents=True,exist_ok=True)
resolver=next(x['args'] for x in json.loads(Path('docs/reports/M8-resource-kind/test-commands.json').read_text()) if x['label']=='resolver')
resolver_args=resolver[:resolver.index('-result-xml')]+['-result-xml',str(out/'resolver.xml')]
assert not any('WarmBorrowed' in x for x in resolver_args)
subprocess.run(resolver_args,check=True)
rr=ET.parse(out/'resolver.xml').getroot();ra=rr.find('assembly')
assert int(ra.attrib['passed'])==42 and all(int(ra.attrib[x])==0 for x in ['errors','failed','skipped','not-run']),ra.attrib
assert '10.0.11' in ra.attrib['environment']
assert all(t.attrib['result']=='Pass' for t in rr.iter('test'))
args=[dotnet,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
assert not any('WarmBorrowed' in x for x in args)
for name in ['Xemele.Tests.M8XPathAccounting.AccountingTests','Xemele.Tests.M5ValidationModel.ValidationModelTests','Xemele.Tests.M5ValidationSourceProvenance.SourceProvenanceTests','Xemele.Tests.ValidationMetadata.ValidationMetadataTests','Xemele.Tests.M4Evaluate.EvaluationTests','Xemele.Tests.M8Model.ModelTests','Xemele.Tests.M8Model.ResourceTests']:
 args+=['-class',name]
args+=['-result-xml',str(out/'focused.xml')]
subprocess.run(args,check=True)
r=ET.parse(out/'focused.xml').getroot();a=r.find('assembly')
assert int(a.attrib['passed'])==167 and all(int(a.attrib[x])==0 for x in ['errors','failed','skipped','not-run']),a.attrib
assert '10.0.11' in a.attrib['environment']
assert all(t.attrib['result']=='Pass' for t in r.iter('test'))
assert not any('WarmBorrowedReadsAllocateNothing' in t.attrib['name'] for t in r.iter('test'))
print('Validated209 actual ARM .NET10.0.11 correctness tests; zero errors/failures/skips/not-run; no resolver allocation child selected',flush=True)
subprocess.run(['python3','docs/reports/M5-validation-model/check_api.py'],check=True)
subprocess.run([dotnet,'--fx-version','10.0.11','docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll','tests/Contracts/Fixtures/M8/Model.golden.txt'],check=True)
subprocess.run([dotnet,'--fx-version','10.0.11','samples/M8-model/bin/Release/net10.0/Xemele.Benchmarks.dll'],check=True)
subprocess.run(['python3','tools/planning/tasks.py','validate'],check=True)
subprocess.run(['git','diff','--check'],check=True)

# XML-only dependency verification, run separately after the checks above.
project='samples/M8-model/Model.Consumer.csproj'
xml_flags=[*common,'-p:ModelXmlOnly=true']
subprocess.run([dotnet,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*xml_flags],check=True)
subprocess.run([dotnet,'build',project,'-c','Release','--no-restore',*xml_flags],check=True)
subprocess.run([dotnet,'--fx-version','10.0.11','samples/M8-model/bin/Release/net10.0/Xemele.Benchmarks.dll'],check=True)
