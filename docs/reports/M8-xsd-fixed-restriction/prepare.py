"""Build and exercise counters-disabled control; freeze identities, never measure."""
import hashlib
import json
import os
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[3]
net = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
flags = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
project = 'benchmarks/Xemele.Benchmarks/M8-xsd-fixed-restriction/Probe.csproj'
subprocess.run([str(net), 'build', project, '-c', 'Release', '--no-restore', *flags], cwd=root, check=True)
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
out = root / 'artifacts/M8-xsd-fixed-restriction/preparation'
out.mkdir(parents=True, exist_ok=True)
environment = {'DOTNET_TieredCompilation': '0', 'DOTNET_gcServer': '0', 'DOTNET_GCConcurrent': '0', 'XEMELE_FIXED_REVISION': revision}
env = os.environ.copy(); env.update(environment)
probe = root / 'benchmarks/Xemele.Benchmarks/M8-xsd-fixed-restriction/bin/probe/net10.0/Xemele.Benchmarks.dll'
subprocess.run([str(net), '--fx-version', '10.0.11', str(probe), '--control', str(out / 'control.json')], cwd=root, env=env, check=True, timeout=40)
control = json.loads((out / 'control.json').read_text())
assert control['mode'] == '--control' and len(control['rows']) == 6 and control['runtime'] == '.NET 10.0.11'
assert control['coldSetupBytes'] == control['positiveBytes'] == control['retainedBefore'] == control['retainedAfter'] == 0
assert all(row['firstBytes'] == 0 and row['samples'] == [0, 0, 0] and row['operationsPerSample'] == 64 for row in control['rows'])
paths = {net, out / 'control.json'}
for directory in [probe.parent, net.parent / 'shared/Microsoft.NETCore.App/10.0.11', net.parent / 'host/fxr']:
    for path in directory.rglob('*'):
        if path.is_file() and (path.suffix in {'.dll', '.dylib', '.so'} or path.name.endswith(('.deps.json', '.runtimeconfig.json'))): paths.add(path)
for relative in subprocess.check_output(['git', 'ls-files', 'src/Xemele'], cwd=root, text=True).splitlines():
    if relative.endswith(('.cs', '.csproj')): paths.add(root / relative)
for directory in ['benchmarks/Xemele.Benchmarks/M8-xsd-fixed-restriction', 'tests/Xemele.Tests/M8-xsd-fixed-restriction', 'samples/M8-xsd-fixed-restriction']:
    for path in (root / directory).iterdir():
        if path.is_file(): paths.add(path)
for relative in ['global.json', 'Directory.Build.props', 'Directory.Packages.props',
                 'docs/reports/M8-xsd-fixed-restriction/prepare.py', 'docs/reports/M8-xsd-fixed-restriction/measure.py']:
    paths.add(root / relative)
def digest(path):
    with path.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def key(path): return str(path.relative_to(root)) if path.is_relative_to(root) else str(path)
manifest = {'sourceRevision': revision, 'maximumSeconds': 60, 'workSeconds': 50, 'cleanupReserveSeconds': 10,
    'routes': 6, 'warmups': 16, 'samples': 3, 'operationsPerSample': 64, 'retainedCount': 16, 'environment': environment,
    'commands': json.loads(subprocess.check_output(['python3', 'docs/reports/M8-xsd-fixed-restriction/measure.py', '--commands'], cwd=root, text=True)),
    'identities': {key(path): digest(path) for path in sorted(paths)}}
target = root / 'docs/reports/M8-xsd-fixed-restriction/preparation.json'
target.write_text(json.dumps(manifest, indent=2) + '\n')
print(f'Prepared {len(paths)} identities; manifest SHA256 {digest(target)}; counters/timing disabled.')
