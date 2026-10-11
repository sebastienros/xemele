from pathlib import Path
import subprocess,xml.etree.ElementTree as ET
D='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
for p in ['tests/Xemele.Tests/Xemele.Tests.csproj','samples/M8-regex/Regex.Consumer.csproj','docs/reports/M8-model/ApiCheck/ApiCheck.csproj','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj']:
 subprocess.run([D,'restore',p,'--source','/Users/sebastienros/.nuget/packages',*flags],check=True)
 subprocess.run([D,'build',p,'-c','Release','--no-restore',*flags],check=True)
out=Path('artifacts/M8-regex');out.mkdir(parents=True,exist_ok=True)
for selection,expected in [('Xemele.Tests.M8Regex.RegexTests',None),('Xemele.Tests.M8Model.ModelTests',None)]:
 result=out/(selection+'.xml')
 subprocess.run([D,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-class',selection,'-result-xml',str(result)],check=True)
 a=ET.parse(result).getroot().find('assembly');assert int(a.attrib['passed'])>0 and all(int(a.attrib[k])==0 for k in ['errors','failed','skipped','not-run']),a.attrib
 print(selection,a.attrib,flush=True)
subprocess.run([D,'--fx-version','10.0.11','docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll','tests/Contracts/Fixtures/M8/Regex.golden.txt'],check=True)
subprocess.run(['python3','docs/reports/M5-validation-model/check_api.py'],check=True)
p='samples/M8-regex/Regex.Consumer.csproj'
for args in [['restore',p,'--source','/Users/sebastienros/.nuget/packages'],['build',p,'-c','Release','--no-restore']]:
 subprocess.run([D,*args,*flags,'-p:RegexXmlOnly=true'],check=True)
subprocess.run([D,'--fx-version','10.0.11','samples/M8-regex/bin/Release/net10.0/Xemele.Benchmarks.dll'],check=True)
subprocess.run(['python3','tools/planning/tasks.py','validate'],check=True)
subprocess.run(['git','diff','--check'],check=True)
