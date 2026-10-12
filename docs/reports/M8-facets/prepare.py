"""Build controls/discovery and freeze identities; never run measurements here."""
import hashlib,json,subprocess,os
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(*args):subprocess.run(args,cwd=root,check=True)
for name,extra in [('Facets.Probe.csproj',['-p:BaseIntermediateOutputPath=obj/probe/','-p:OutputPath=bin/probe/']),('Facets.Bdn.csproj',[])]:
 project='benchmarks/Xemele.Benchmarks/M8-facets/'+name
 run(str(net),'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags,*extra)
 run(str(net),'build',project,'-c','Release','--no-restore',*flags,*extra)
out=root/'artifacts/M8-facets/preparation';out.mkdir(parents=True,exist_ok=True)
env=os.environ.copy();env.update(DOTNET_TieredCompilation='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0')
subprocess.run([str(net),'--fx-version','10.0.11','benchmarks/Xemele.Benchmarks/M8-facets/bin/probe/net10.0/Xemele.Benchmarks.dll','--control',str(out/'control.json')],cwd=root,env=env,check=True)
run(str(net),'--fx-version','10.0.11','benchmarks/Xemele.Benchmarks/M8-facets/bin/Release/net10.0/Xemele.Benchmarks.dll','--control')
run(str(net),'--fx-version','10.0.11','benchmarks/Xemele.Benchmarks/M8-facets/bin/Release/net10.0/Xemele.Benchmarks.dll','--discover',str(out/'discovery'))
paths={net,root/'src/Xemele/bin/Release/net10.0/Xemele.dll'}
for folder in ['benchmarks/Xemele.Benchmarks/M8-facets/bin/probe/net10.0','benchmarks/Xemele.Benchmarks/M8-facets/bin/Release/net10.0']:
 for p in (root/folder).rglob('*'):
  if p.is_file() and (p.suffix in {'.dll','.dylib','.so'} or p.name.endswith(('.deps.json','.runtimeconfig.json'))):paths.add(p)
for folder in [net.parent/'shared/Microsoft.NETCore.App/10.0.11',net.parent/'host/fxr']:
 for p in folder.rglob('*'):
  if p.is_file() and (folder.name!='fxr' or p.name=='libhostfxr.dylib'):paths.add(p)
for relative in subprocess.check_output(['git','ls-files','src/Xemele'],cwd=root,text=True).splitlines():
 if relative.endswith(('.cs','.csproj')):paths.add(root/relative)
for folder in ['tests/Xemele.Tests/M8-facets','benchmarks/Xemele.Benchmarks/M8-facets','samples/M8-facets']:
 for p in (root/folder).iterdir():
  if p.is_file():paths.add(p)
for relative in ['global.json','Directory.Build.props','Directory.Packages.props','docs/reports/M8-facets/prepare.py','docs/reports/M8-facets/measure.py']:
 paths.add(root/relative)
def digest(p):
 with p.open('rb') as s:return hashlib.file_digest(s,'sha256').hexdigest()
def key(p):return str(p.relative_to(root)) if p.is_relative_to(root) else str(p)
manifest={'sourceRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'maximumSeconds':600,'workSeconds':590,'cleanupReserveSeconds':10,'probeRoutes':17,'zeroAllocationRoutes':11,'benchmarkCases':6,'warmSamples':32,'measuredSamples':64,'commands':json.loads(subprocess.check_output(['python3','docs/reports/M8-facets/measure.py','--commands'],cwd=root,text=True)),'identityCount':len(paths),'identities':{key(p):digest(p) for p in sorted(paths)}}
p=root/'docs/reports/M8-facets/preparation.json';p.write_text(json.dumps(manifest,indent=2)+'\n')
print(f'Prepared {len(paths)} identities; manifest {digest(p)}; controls/discovery counters and timing disabled.')
