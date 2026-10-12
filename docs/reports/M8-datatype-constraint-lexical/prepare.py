"""After source acceptance and host RELEASE: build/control/freeze; no measurements."""
import hashlib, json, os, subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
project='benchmarks/Xemele.Benchmarks/M8-datatype-constraint-lexical/Probe.csproj'
for verb in ['restore','build']:
 args=[str(net),verb,project,*( ['--source','/Users/sebastienros/.nuget/packages'] if verb=='restore' else ['-c','Release','--no-restore']),*flags]
 subprocess.run(args,cwd=root,check=True)
out=root/'artifacts/M8-datatype-constraint-lexical/preparation';out.mkdir(parents=True,exist_ok=True)
env=os.environ.copy();env.update(DOTNET_TieredCompilation='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0')
subprocess.run([str(net),'--fx-version','10.0.11','benchmarks/Xemele.Benchmarks/M8-datatype-constraint-lexical/bin/Release/net10.0/Xemele.Benchmarks.dll','--control',str(out/'control.json')],cwd=root,env=env,check=True,timeout=40)
control=json.loads((out/'control.json').read_text())
assert control['mode']=='--control' and control['runtime']=='.NET 10.0.11' and len(control['rows'])==9
assert all(row['bytes']==0 and row['operations']==64 for row in control['rows'])
assert control['coldBytes']==control['positiveBytes']==control['retainedBefore']==control['retainedAfter']==0
paths={net,root/'src/Xemele/bin/Release/net10.0/Xemele.dll',out/'control.json'}
for folder in [root/'benchmarks/Xemele.Benchmarks/M8-datatype-constraint-lexical/bin/Release/net10.0',net.parent/'shared/Microsoft.NETCore.App/10.0.11',net.parent/'host/fxr']:
 for p in folder.rglob('*'):
  if p.is_file() and (p.suffix in {'.dll','.dylib','.so'} or p.name.endswith(('.deps.json','.runtimeconfig.json'))):paths.add(p)
for relative in subprocess.check_output(['git','ls-files','src/Xemele'],cwd=root,text=True).splitlines():
 if relative.endswith(('.cs','.csproj')):paths.add(root/relative)
for folder in ['tests/Xemele.Tests/M8-datatype-constraint-lexical','benchmarks/Xemele.Benchmarks/M8-datatype-constraint-lexical','samples/M8-datatype-constraint-lexical']:
 for p in (root/folder).iterdir():
  if p.is_file():paths.add(p)
for relative in ['global.json','Directory.Build.props','Directory.Packages.props','docs/reports/M8-datatype-constraint-lexical/prepare.py','docs/reports/M8-datatype-constraint-lexical/measure.py']:
 paths.add(root/relative)
def digest(p):
 with p.open('rb') as s:return hashlib.file_digest(s,'sha256').hexdigest()
def key(p):return str(p.relative_to(root)) if p.is_relative_to(root) else str(p)
manifest={'sourceRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'maximumSeconds':120,'workSeconds':110,'cleanupReserveSeconds':10,'probeRoutes':10,'zeroAllocationRoutes':1,'warmups':16,'aggregateOperations':64,'retainedCount':16,'commands':json.loads(subprocess.check_output(['python3','docs/reports/M8-datatype-constraint-lexical/measure.py','--commands'],cwd=root,text=True)),'identityCount':len(paths),'identities':{key(p):digest(p) for p in sorted(paths)}}
p=root/'docs/reports/M8-datatype-constraint-lexical/preparation.json';p.write_text(json.dumps(manifest,indent=2)+'\n')
print(f'Prepared {len(paths)} identities; manifest {digest(p)}; counters and timing disabled.')
