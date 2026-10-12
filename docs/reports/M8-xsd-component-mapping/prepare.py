"""After source acceptance and host RELEASE: build/control/freeze; no measurements."""
import hashlib,json,os,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
task=json.loads((root/'docs/tasks/M8-xsd-component-mapping.json').read_text())
accepted=next(e['revision'] for e in reversed(task['evidence']) if e['id']=='branch-correctness' and e['result']=='passed')
subprocess.run(['git','diff','--exit-code',accepted,'--','src/Xemele','tests/Xemele.Tests/M8-xsd-component-mapping'],cwd=root,check=True)
revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
project='benchmarks/Xemele.Benchmarks/M8-xsd-component-mapping/Mapping.Probe.csproj'
for verb in ['restore','build']:
 args=[str(net),verb,project,*( ['--source','/Users/sebastienros/.nuget/packages'] if verb=='restore' else ['-c','Release','--no-restore']),*flags]
 subprocess.run(args,cwd=root,check=True)
out=root/'artifacts/M8-xsd-component-mapping/preparation';out.mkdir(parents=True,exist_ok=True)
env=os.environ.copy();env.update(DOTNET_TieredCompilation='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0',XEMELE_MAPPING_REVISION=revision)
subprocess.run([str(net),'--fx-version','10.0.11','benchmarks/Xemele.Benchmarks/M8-xsd-component-mapping/bin/probe/net10.0/Xemele.Benchmarks.dll','--control',str(out/'control.json')],cwd=root,env=env,check=True,timeout=40)
control=json.loads((out/'control.json').read_text())
assert control['mode']=='--control' and control['status']=='passed' and control['runtime']=='.NET 10.0.11' and len(control['rows'])==19
assert all(row['bytes']==0 and row['firstRunBytes']==0 and row['operations']==64 for row in control['rows'])
assert control['coldSetupBytes']==control['positiveBytes']==control['retainedBefore']==control['retainedAfterResults']==0
assert control['retainedResults']==16
paths={net,root/'src/Xemele/bin/Release/net10.0/Xemele.dll',out/'control.json'}
for folder in [root/'benchmarks/Xemele.Benchmarks/M8-xsd-component-mapping/bin/probe/net10.0',net.parent/'shared/Microsoft.NETCore.App/10.0.11',net.parent/'host/fxr']:
 for p in folder.rglob('*'):
  if p.is_file() and (p.suffix in {'.dll','.dylib','.so'} or p.name.endswith(('.deps.json','.runtimeconfig.json'))):paths.add(p)
for relative in subprocess.check_output(['git','ls-files','src/Xemele'],cwd=root,text=True).splitlines():
 if relative.endswith(('.cs','.csproj')):paths.add(root/relative)
for folder in ['tests/Xemele.Tests/M8-xsd-component-mapping','benchmarks/Xemele.Benchmarks/M8-xsd-component-mapping','tests/fixtures/M8-xsd-component-mapping']:
 for p in (root/folder).iterdir():
  if p.is_file():paths.add(p)
for relative in ['global.json','Directory.Build.props','Directory.Packages.props','docs/reports/M8-xsd-component-mapping/prepare.py','docs/reports/M8-xsd-component-mapping/measure.py','docs/reports/M8-xsd-component-mapping/check.py','docs/reports/M8-xsd-component-mapping/assert_results.py','tests/Xemele.Tests/M8-xsd-effective-content/MappingTests.cs','docs/reports/M8-xsd-effective-content/Probe/Program.cs','docs/reports/M8-xsd-effective-content/README.md']:
 paths.add(root/relative)
def digest(p):
 with p.open('rb') as s:return hashlib.file_digest(s,'sha256').hexdigest()
def key(p):return str(p.relative_to(root)) if p.is_relative_to(root) else str(p)
manifest={'sourceRevision':revision,'acceptedRuntimeRevision':accepted,'maximumSeconds':30,'workSeconds':20,'cleanupReserveSeconds':10,'probeRoutes':19,'zeroAllocationRoutes':6,'warmups':16,'aggregateOperations':64,'retainedCount':16,'environment':{'DOTNET_TieredCompilation':'0','DOTNET_gcServer':'0','DOTNET_GCConcurrent':'0'},'commands':json.loads(subprocess.check_output(['python3','docs/reports/M8-xsd-component-mapping/measure.py','--commands'],cwd=root,text=True)),'firstRuns':1,'identityCount':len(paths),'identities':{key(p):digest(p) for p in sorted(paths)}}
p=root/'docs/reports/M8-xsd-component-mapping/preparation.json';p.write_text(json.dumps(manifest,indent=2)+'\n')
print(f'Prepared {len(paths)} identities; manifest {digest(p)}; counters and timing disabled.')
