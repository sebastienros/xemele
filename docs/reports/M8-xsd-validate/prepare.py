"""Freeze already-built identities; never builds, times, or measures."""
import hashlib,json,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[3]
def digest(path):
 with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
prepared_revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
task=json.loads((root/'docs/tasks/M8-xsd-validate.json').read_text())
revision=prepared_revision
inputs=['src/Xemele','tests/Xemele.Tests/M8-xsd-validate','benchmarks/Xemele.Benchmarks/M8-xsd-validate','samples/M8-xsd-validate','docs/reports/M8-xsd-validate/check.py','docs/reports/M8-xsd-validate/XmlOnly']
subprocess.run(['git','diff','--exit-code',revision,'--',*inputs],cwd=root,check=True,stdout=subprocess.DEVNULL)
out=root/'artifacts/M8-xsd-validate/measurement-1'
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
identities={}
directories=['src/Xemele/Diagnostics','src/Xemele/Internal','src/Xemele/Options','src/Xemele/Parsing','src/Xemele/Resolution/Core','src/Xemele/Serialization','src/Xemele/Streaming','src/Xemele/Tree','src/Xemele/Validation/Common','src/Xemele/Validation/Dtd','src/Xemele/XPath','src/Xemele/Validation/SchemaCommon','src/Xemele/Validation/Datatypes','src/Xemele/Validation/Regex','src/Xemele/Validation/Xsd/Compilation','src/Xemele/Validation/Xsd/Identity','src/Xemele/Validation/Xsd/ContentModels','src/Xemele/Validation/Xsd/Evaluation','tests/Xemele.Tests/M8-xsd-validate','benchmarks/Xemele.Benchmarks/M8-xsd-validate','samples/M8-xsd-validate','docs/reports/M8-xsd-validate']
for directory in directories:
 for path in (root/directory).rglob('*'):
  if path.is_file() and not any(part in ['bin','obj','__pycache__'] for part in path.relative_to(root).parts) and path.name!='preparation.json':
   identities[str(path.relative_to(root))]=digest(path)
for relative in ['Directory.Build.props','global.json','src/Xemele/Xemele.csproj','docs/api/M8-xsd-validate.txt','tests/Contracts/Fixtures/M8/Xsd.cs.fixture','tests/Contracts/Fixtures/M8/Xsd.golden.txt']:
 path=root/relative
 if path.is_file():identities[relative]=digest(path)
for relative in ['benchmarks/Xemele.Benchmarks/M8-xsd-validate/bin/probe/net10.0','src/Xemele/bin/Release/net10.0','docs/reports/M8-xsd-validate/XmlOnly/bin/Release/net10.0']:
 for path in (root/relative).iterdir():
  if path.is_file() and path.suffix in ['.dll','.pdb','.json']:identities[str(path.relative_to(root))]=digest(path)
identities[net]=digest(Path(net))
commands=[[net,'--fx-version','10.0.11',str(root/'benchmarks/Xemele.Benchmarks/M8-xsd-validate/bin/probe/net10.0/Xemele.Benchmarks.dll'),'--measure',str(out/'allocations.json')]]
manifest={'sourceRevision':revision,'preparedRevision':prepared_revision,'maximumSeconds':30,'cleanupReserveSeconds':10,'commands':commands,'environment':{'DOTNET_TieredCompilation':'0','DOTNET_gcServer':'0','DOTNET_GCConcurrent':'0','DOTNET_ROLL_FORWARD':'Disable'},'identities':dict(sorted(identities.items()))}
p=root/'docs/reports/M8-xsd-validate/preparation.json';p.write_text(json.dumps(manifest,indent=2)+'\n')
print('Prepared',len(identities),'identities; source',revision,'manifest',digest(p))
