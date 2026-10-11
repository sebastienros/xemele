"""Focused integrated correctness checks; no allocation test or counters selected."""
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
D='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
common=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
for project in ['tests/Xemele.Tests/Xemele.Tests.csproj','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj','docs/reports/M8-model/ApiCheck/ApiCheck.csproj']:
 subprocess.run([D,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*common],check=True)
 subprocess.run([D,'build',project,'-c','Release','--no-restore',*common],check=True)
out=Path('artifacts/M8-source-locations/integration.xml');out.parent.mkdir(parents=True,exist_ok=True)
args=[D,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
classes=['Xemele.Tests.AsyncInput.AsyncInputTests', 'Xemele.Tests.Dom.DomTests', 'Xemele.Tests.Dom.PersistenceTests', 'Xemele.Tests.DtdInstanceProvenance.CompletionHookTests', 'Xemele.Tests.DtdInstanceProvenance.InstanceProvenanceTests', 'Xemele.Tests.M5ValidationSourceProvenance.SourceProvenanceTests', 'Xemele.Tests.M8Model.ModelTests', 'Xemele.Tests.M8Model.ResourceTests', 'Xemele.Tests.M8SourceLocations.SourceLocationTests', 'Xemele.Tests.ValidationMetadata.ValidationMetadataTests']
for name in classes: args+=['-class',name]
args+=['-result-xml',str(out)]
subprocess.run(args,check=True)
r=ET.parse(out).getroot();a=r.find('assembly')
assert int(a.attrib['passed'])==177 and all(int(a.attrib[k])==0 for k in ['errors','failed','skipped','not-run']),a.attrib
assert '10.0.11' in a.attrib['environment']
assert all(t.attrib['result']=='Pass' for t in r.iter('test'))
assert len(list(r.iter('test')))==177
print('Verified 177 actual Arm64 .NET 10.0.11 correctness cases; no allocation test selected.',flush=True)
subprocess.run(['python3','docs/reports/M5-validation-model/check_api.py'],check=True)
subprocess.run([D,'--fx-version','10.0.11','docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll','tests/Contracts/Fixtures/M8/Model.golden.txt'],check=True)
subprocess.run(['python3','tools/planning/tasks.py','validate'],check=True)
subprocess.run(['git','diff','--check'],check=True)
