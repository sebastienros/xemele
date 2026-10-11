"""Extend identities without rebuilding or invoking any counters/timing."""
import hashlib, json, subprocess
from pathlib import Path
root = Path(__file__).resolve().parents[3]
manifest_path = root / 'docs/reports/M8-datatypes-numeric/preparation.json'
manifest = json.loads(manifest_path.read_text())
net = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
paths = {root / 'src/Xemele/bin/Release/net10.0/Xemele.dll', net}
for folder in ['benchmarks/Xemele.Benchmarks/M8-datatypes-numeric/bin/probe/net10.0', 'benchmarks/Xemele.Benchmarks/M8-datatypes-numeric/bin/Release/net10.0']:
 for path in (root / folder).rglob('*'):
  if path.is_file() and (path.suffix in {'.dll','.dylib','.so'} or path.name.endswith(('.deps.json','.runtimeconfig.json'))): paths.add(path)
# Prepared payloads may load managed runtime facades/natives lazily. Hash the
# complete pinned runtime instead of guessing which facades a benchmark uses.
for folder in [net.parent / 'shared/Microsoft.NETCore.App/10.0.11', net.parent / 'host/fxr']:
 for path in folder.rglob('*'):
  if path.is_file() and (folder.name != 'fxr' or path.name == 'libhostfxr.dylib'): paths.add(path)
for folder in ['src/Xemele/Validation/Datatypes/Values/Core', 'src/Xemele/Validation/Datatypes/Values/Numeric', 'tests/Xemele.Tests/M8-datatypes-numeric', 'benchmarks/Xemele.Benchmarks/M8-datatypes-numeric']:
 for path in (root / folder).iterdir():
  if path.is_file(): paths.add(path)
for name in ['measure.py','prepare.py','preflight.py','Consumer.cs.fixture','Consumer.csproj','checks.py','correctness.md','joins.md']:
 paths.add(root / 'docs/reports/M8-datatypes-numeric' / name)
paths.add(root / 'docs/reports/M8-datatypes-core/contracts.md')
paths.add(root / 'docs/reports/M8-datatypes-numeric/XmlOnly/XmlOnly.csproj')
# Hash all tracked library build inputs without loading them into chat context.
for relative in subprocess.check_output(['git','ls-files','src/Xemele'],cwd=root,text=True).splitlines():
 if relative.endswith(('.cs','.csproj')): paths.add(root / relative)
for relative in ['global.json','Directory.Build.props','Directory.Packages.props','src/Xemele/Xemele.csproj']:
 if (root / relative).is_file(): paths.add(root / relative)
def key(path): return str(path.relative_to(root)) if path.is_relative_to(root) else str(path)
def digest(path):
 with path.open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
manifest['identities'] = {key(p):digest(p) for p in sorted(paths)}
manifest['identityCount'] = len(paths)
manifest['identityScope'] = 'Both actual copied payloads including dependency DLLs and runtimeconfig/deps; pinned dotnet host, installed hostfxr candidates, complete NETCore.App10.0.11 runtime/CoreLib; core/numeric/probe/BDN fixture/source and harness/config.'
manifest['workSeconds'] = 590; manifest['cleanupReserveSeconds'] = 10
manifest['commands'] = json.loads(subprocess.check_output(['python3', str(root / 'docs/reports/M8-datatypes-numeric/measure.py'), '--commands'], text=True))
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
print(f"Prepared {len(paths)} file identities; manifest {digest(manifest_path)}; no binaries changed.")
