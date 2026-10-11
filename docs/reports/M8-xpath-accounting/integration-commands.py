import json,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
dotnet='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
projects=['tests/Xemele.Tests/Xemele.Tests.csproj','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj','docs/reports/M8-xpath-accounting/ApiCheck/ApiCheck.csproj']
common=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
for project in projects:
 subprocess.run([dotnet,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*common],check=True)
 subprocess.run([dotnet,'build',project,'-c','Release','--no-restore',*common],check=True)
out=Path('artifacts/M8-xpath-accounting/integration');out.mkdir(parents=True,exist_ok=True)
args=[dotnet,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
for namespace in ['M4Evaluate','M4Model','M4Compile','M4ModelContext','M4ModelConstruction','M4Navigation','M4Functions','M8XPathAccounting']:
 args+=['-namespace','Xemele.Tests.'+namespace]
args+=['-result-xml',str(out/'focused.xml')]
subprocess.run(args,check=True)
r=ET.parse(out/'focused.xml').getroot();a=r.find('assembly')
assert int(a.attrib['passed'])==368 and all(int(a.attrib[x])==0 for x in ['errors','failed','skipped','not-run'])
assert '10.0.11' in a.attrib['environment']
assert all(t.attrib['result']=='Pass' for t in r.iter('test'))
print('Validated368 actual ARM .NET10.0.11 tests; zero errors/failures/skips/not-run',flush=True)
subprocess.run(['python3','docs/reports/M5-validation-model/check_api.py'],check=True)
subprocess.run([dotnet,'--fx-version','10.0.11','docs/reports/M8-xpath-accounting/ApiCheck/bin/Release/net10.0/ApiCheck.dll','accounting','src/Xemele/bin/Release/net10.0/Xemele.dll',str(out/'internal.txt')],check=True)
assert (out/'internal.txt').read_bytes()==Path('docs/reports/M8-xpath-accounting/internal.golden.txt').read_bytes()
print('Matched all5 frozen internal accounting declarations',flush=True)
subprocess.run(['python3','tools/planning/tasks.py','validate'],check=True)
subprocess.run(['git','diff','--check'],check=True)
