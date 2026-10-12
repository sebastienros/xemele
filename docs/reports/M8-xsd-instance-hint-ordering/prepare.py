"""Offline build and freeze only. Counter-disabled preflight is a separate command."""
import hashlib
import json
import subprocess
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
flags = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
project = 'benchmarks/Xemele.Benchmarks/M8-xsd-instance-hint-ordering/Hint.Probe.csproj'
for verb in ['restore', 'build']:
 args = [str(net), verb, project, *(['--source', '/Users/sebastienros/.nuget/packages'] if verb == 'restore' else ['-c', 'Release', '--no-restore']), *flags]
 subprocess.run(args, cwd=root, check=True, timeout=120)
paths = {net, root / 'src/Xemele/bin/Release/net10.0/Xemele.dll'}
for folder in [root / 'benchmarks/Xemele.Benchmarks/M8-xsd-instance-hint-ordering/bin/probe/net10.0', net.parent / 'shared/Microsoft.NETCore.App/10.0.11', net.parent / 'host/fxr']:
 for p in folder.rglob('*'):
  if p.is_file() and (p.suffix in {'.dll', '.dylib', '.so'} or p.name.endswith(('.deps.json', '.runtimeconfig.json'))): paths.add(p)
for relative in subprocess.check_output(['git', 'ls-files', 'src/Xemele'], cwd=root, text=True).splitlines():
 if relative.endswith(('.cs', '.csproj')): paths.add(root / relative)
for folder in ['tests/Xemele.Tests/M8-xsd-instance-hint-ordering', 'tests/fixtures/M8-xsd-instance-hint-ordering', 'benchmarks/Xemele.Benchmarks/M8-xsd-instance-hint-ordering', 'docs/reports/M8-xsd-instance-hint-ordering']:
 for p in (root / folder).iterdir():
  if p.is_file() and p.name not in {'preparation.json', 'preparation-summary.json', 'allocations.md'}: paths.add(p)
for relative in ['global.json', 'Directory.Build.props', 'Directory.Packages.props', 'docs/api/M8-xsd-instance-hint-ordering.txt', 'docs/decisions/M8/instance-hint-ordering.md']:
 paths.add(root / relative)
def digest(path):
 with path.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def key(path): return str(path.relative_to(root)) if path.is_relative_to(root) else str(path)
manifest = {
 'sourceRevision': revision, 'testedRuntimeRevision': subprocess.check_output(['git', 'rev-parse', '7cb790009'], cwd=root, text=True).strip(),
 'maximumSeconds': 30, 'workSeconds': 20, 'cleanupReserveSeconds': 10,
 'probeRoutes': 13, 'zeroAllocationRoutes': 6, 'ownedRoutes': 7,
 'warmups': 16, 'aggregateOperations': 64, 'retainedCount': 16,
 'environment': {'DOTNET_TieredCompilation': '0', 'DOTNET_gcServer': '0', 'DOTNET_GCConcurrent': '0'},
 'commands': json.loads(subprocess.check_output(['python3', 'docs/reports/M8-xsd-instance-hint-ordering/measure.py', '--commands'], cwd=root, text=True)),
 'identityCount': len(paths), 'identities': {key(p): digest(p) for p in sorted(paths)}
}
p = root / 'docs/reports/M8-xsd-instance-hint-ordering/preparation.json'
p.write_text(json.dumps(manifest, indent=2) + '\n')
print(f'Prepared {len(paths)} identities; manifest {digest(p)}; no counters or workload executed.')
