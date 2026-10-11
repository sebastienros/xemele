"""Extend identities without rebuilding or invoking any counters/timing."""
import hashlib, json, subprocess
from pathlib import Path
root = Path(__file__).resolve().parents[3]
manifest_path = root / 'docs/reports/M8-datatypes-core/preparation.json'
manifest = json.loads(manifest_path.read_text())
net = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
paths = {root / 'src/Xemele/bin/Release/net10.0/Xemele.dll', net}
for folder in ['benchmarks/Xemele.Benchmarks/M8-datatypes-core/bin/probe/net10.0', 'benchmarks/Xemele.Benchmarks/M8-datatypes-core/bin/Release/net10.0']:
 for path in (root / folder).rglob('*'):
  if path.is_file() and (path.suffix in {'.dll','.dylib','.so'} or path.name.endswith(('.deps.json','.runtimeconfig.json'))): paths.add(path)
# Prepared payloads may load managed runtime facades/natives lazily. Hash the
# complete pinned runtime instead of guessing which facades a benchmark uses.
for folder in [net.parent / 'shared/Microsoft.NETCore.App/10.0.11', net.parent / 'host/fxr']:
 for path in folder.rglob('*'):
  if path.is_file() and (folder.name != 'fxr' or path.name == 'libhostfxr.dylib'): paths.add(path)
for folder in ['src/Xemele/Validation/Datatypes/Values/Core', 'tests/Xemele.Tests/M8-datatypes-core', 'benchmarks/Xemele.Benchmarks/M8-datatypes-core']:
 for path in (root / folder).iterdir():
  if path.is_file(): paths.add(path)
for name in ['measure.py','prepare.py','preflight.py','Consumer.cs.fixture','Consumer.csproj','contracts.md']:
 paths.add(root / 'docs/reports/M8-datatypes-core' / name)
paths.add(root / 'tests/fixtures/M8-datatypes-core/README.md')
def key(path): return str(path.relative_to(root)) if path.is_relative_to(root) else str(path)
def digest(path):
 with path.open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
manifest['identities'] = {key(p):digest(p) for p in sorted(paths)}
manifest['identityCount'] = len(paths)
manifest['identityScope'] = 'Both actual copied payloads including dependency DLLs and runtimeconfig/deps; pinned dotnet host, installed hostfxr candidates, complete NETCore.App10.0.11 runtime/CoreLib; core/probe/BDN fixture/source and harness/config.'
manifest['workSeconds'] = 590; manifest['cleanupReserveSeconds'] = 10
manifest['commands'] = json.loads(subprocess.check_output(['python3', str(root / 'docs/reports/M8-datatypes-core/measure.py'), '--commands'], text=True))
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
print(f"Prepared {len(paths)} file identities; manifest {digest(manifest_path)}; no binaries changed.")
