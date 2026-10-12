"""Build and execute only counter-disabled controls, then freeze exact identities."""
import hashlib, json, os, subprocess
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
flags = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
project = 'benchmarks/Xemele.Benchmarks/M8-rng-validate/Rng.Probe.csproj'
for command in [[str(net), 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *flags], [str(net), 'build', project, '-c', 'Release', '--no-restore', *flags]]:
 subprocess.run(command, cwd=root, check=True)
out = root / 'artifacts/M8-rng-validate/preparation'
out.mkdir(parents=True, exist_ok=True)
env = os.environ.copy()
env.update(DOTNET_TieredCompilation='0', DOTNET_gcServer='0', DOTNET_GCConcurrent='0')
subprocess.run([str(net), '--fx-version', '10.0.11', 'benchmarks/Xemele.Benchmarks/M8-rng-validate/bin/Release/net10.0/Xemele.Benchmarks.dll', '--control', str(out / 'control.json')], cwd=root, env=env, check=True)
control = json.loads((out / 'control.json').read_text())
assert not control['CountersEnabled'] and control['RouteCount'] == 10
assert control['RouteNames'] == ['compile-owned-schema', 'dom-valid', 'dom-invalid-diagnostic', 'coalesced-multilingual-text', 'datatype-regex', 'recursive-dom', 'list-values', 'ambiguous-interleave', 'string-input-dom-and-result', 'byte-stream-input-dom-and-result']
paths = {net}
for folder in ['benchmarks/Xemele.Benchmarks/M8-rng-validate/bin/Release/net10.0', str(net.parent / 'shared/Microsoft.NETCore.App/10.0.11'), str(net.parent / 'host/fxr')]:
 for p in (root / folder).rglob('*'):
  if p.is_file() and (p.suffix in {'.dll', '.dylib', '.so'} or p.name.endswith(('.deps.json', '.runtimeconfig.json'))): paths.add(p)
for relative in subprocess.check_output(['git', 'ls-files', 'src/Xemele'], cwd=root, text=True).splitlines():
 if relative.endswith(('.cs', '.csproj')): paths.add(root / relative)
for folder in ['tests/Xemele.Tests/M8-rng-validate', 'benchmarks/Xemele.Benchmarks/M8-rng-validate', 'samples/M8-rng-validate', 'docs/reports/M8-rng-validate']:
 for p in (root / folder).iterdir():
  if p.is_file() and p.suffix in {'.cs', '.csproj', '.py'}: paths.add(p)
for relative in ['global.json', 'Directory.Build.props', 'Directory.Packages.props', 'docs/api/M8-rng-validate.txt']:
 paths.add(root / relative)
def digest(p):
 with p.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def key(p): return str(p.relative_to(root)) if p.is_relative_to(root) else str(p)
manifest = {'sourceRevision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(), 'maximumSeconds': 45, 'workSeconds': 35, 'cleanupReserveSeconds': 10, 'probeRoutes': 10, 'warmSamples': 32, 'measuredSamples': 64, 'operationsPerSample': 16, 'commands': json.loads(subprocess.check_output(['python3', 'docs/reports/M8-rng-validate/measure.py', '--commands'], cwd=root, text=True)), 'identityCount': len(paths), 'identities': {key(p): digest(p) for p in sorted(paths)}}
# Full identity lists stay transient. The compact summary and hashes are committed.
p = out / 'preparation.json'; p.write_text(json.dumps(manifest, indent=2) + '\n')
print(f'PASS: disabled controls; {len(paths)} frozen identities; manifest SHA256 {digest(p)}')
