"""Freeze already-built payload identities after accepted source; no counters/timing."""
import hashlib,json,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
paths={net,root/'src/Xemele/bin/Release/net10.0/Xemele.dll'}
for folder in ['benchmarks/Xemele.Benchmarks/M8-datatypes/bin/probe/net10.0','benchmarks/Xemele.Benchmarks/M8-datatypes/bin/Release/net10.0']:
 for p in (root/folder).rglob('*'):
  if p.is_file() and (p.suffix in {'.dll','.dylib','.so'} or p.name.endswith(('.deps.json','.runtimeconfig.json'))):paths.add(p)
for folder in [net.parent/'shared/Microsoft.NETCore.App/10.0.11',net.parent/'host/fxr']:
 for p in folder.rglob('*'):
  if p.is_file() and (folder.name!='fxr' or p.name=='libhostfxr.dylib'):paths.add(p)
for relative in subprocess.check_output(['git','ls-files','src/Xemele'],cwd=root,text=True).splitlines():
 if relative.endswith(('.cs','.csproj')):paths.add(root/relative)
for folder in ['tests/Xemele.Tests/M8-datatypes','tests/fixtures/M8-datatypes','benchmarks/Xemele.Benchmarks/M8-datatypes','docs/reports/M8-datatypes']:
 if not (root/folder).exists():continue
 for p in (root/folder).iterdir():
  if p.is_file() and p.name not in {'preparation.json','preparation-2.json','measurement-summary.json','allocations.md'}:paths.add(p)
paths.add(root/'docs/reports/M8-datatypes/XmlOnly/XmlOnly.csproj')
for p in (root/'docs/reports/M8-datatypes/IL').iterdir():
 if p.is_file():paths.add(p)
for p in (root/'samples/M8-datatypes').iterdir():
 if p.is_file():paths.add(p)
for relative in ['global.json','Directory.Build.props','Directory.Packages.props','src/Xemele/Xemele.csproj','docs/reports/M8-datatypes-core/contracts.md']:
 if (root/relative).is_file():paths.add(root/relative)
def digest(p):
 with p.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def key(p):return str(p.relative_to(root)) if p.is_relative_to(root) else str(p)
manifest={'sourceRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'preparationRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'maximumSeconds':600,'workSeconds':590,'cleanupReserveSeconds':10,
 'probeRoutes':18,'zeroAllocationRoutes':8,'benchmarkCases':6,'warmSamples':32,'measuredSamples':64,
 'identityScope':'Actual copied probe/BDN dependencies and configs, all tracked library build inputs, source/tests/fixtures/harness/consumers, host/fxr and complete NETCore.App10.0.11 runtime.',
 'commands':json.loads(subprocess.check_output(['python3',str(root/'docs/reports/M8-datatypes/measure.py'),'--commands'],text=True)),
 'identityCount':len(paths),'identities':{key(p):digest(p) for p in sorted(paths)}}
p=root/'docs/reports/M8-datatypes/preparation-2.json';p.write_text(json.dumps(manifest,indent=2)+'\n')
print(f'Prepared {len(paths)} identities; manifest {digest(p)}; counters/timing disabled.')
