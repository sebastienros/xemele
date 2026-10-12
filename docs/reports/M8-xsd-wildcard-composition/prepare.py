"""Freeze already built source/probe/runtime identities; no counters or timing."""
import hashlib,json,os,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
report=root/'docs/reports/M8-xsd-wildcard-composition'
paths={net}
for folder in [report/'Probe/bin/Release/net10.0',net.parent/'shared/Microsoft.NETCore.App/10.0.11',net.parent/'host/fxr']:
 for p in folder.rglob('*'):
  if p.is_file():paths.add(p)
for relative in subprocess.check_output(['git','ls-files','src/Xemele'],cwd=root,text=True).splitlines():
 if relative.endswith(('.cs','.csproj')):paths.add(root/relative)
for folder in [report,report/'Probe',root/'tests/Xemele.Tests/M8-xsd-wildcard-composition']:
 for p in folder.iterdir():
  if p.is_file() and p.name not in {'preparation.json','README.md','allocations.md','measurement-summary.json'}:paths.add(p)
for relative in ['global.json','Directory.Build.props','Directory.Packages.props']:
 if (root/relative).exists():paths.add(root/relative)
def digest(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def key(p):return str(p.relative_to(root)) if p.is_relative_to(root) else str(p)
manifest={'sourceRevision':'42a93288b8a3b4c2def3be7c5a1183738f9137c6','preparationRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'maximumSeconds':30,'workSeconds':20,'cleanupReserveSeconds':10,'probeRoutes':5,'warmSamples':32,'measuredSamples':64,'environment':{k:v for k,v in os.environ.items() if k.startswith(('DOTNET_','COMPlus_','COREHOST_'))},'commands':json.loads(subprocess.check_output(['python3',str(report/'measure.py'),'--commands'],text=True)),'identities':{key(p):digest(p) for p in sorted(paths)}}
p=report/'preparation.json';p.write_text(json.dumps(manifest,indent=2)+'\n');print(f'Prepared {len(paths)} identities; manifest {digest(p)}; counters/timing disabled')
