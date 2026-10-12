"""Build and freeze only after source acceptance and passing focused checks. No allocation measurements."""
import hashlib, json, os, subprocess
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
flags = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
accepted = json.loads((root / 'docs/reports/M8-xsd-attribute-fixed/checks.json').read_text())['revision']
subprocess.run(['git', 'diff', '--exit-code', accepted, '--', 'src/Xemele', 'tests/Xemele.Tests/M8-xsd-attribute-fixed'], cwd=root, check=True)
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
project = 'benchmarks/Xemele.Benchmarks/M8-xsd-attribute-fixed/AttributeFixed.Probe.csproj'
for verb in ['restore', 'build']:
    subprocess.run([str(net), verb, project, *(['--source', '/Users/sebastienros/.nuget/packages'] if verb == 'restore' else ['-c', 'Release', '--no-restore']), *flags], cwd=root, check=True)
out = root / 'artifacts/M8-xsd-attribute-fixed/preparation'; out.mkdir(parents=True, exist_ok=False)
env = dict(os.environ, DOTNET_TieredCompilation='0', DOTNET_gcServer='0', DOTNET_GCConcurrent='0', XEMELE_ATTRIBUTE_REVISION=revision)
subprocess.run([str(net), '--fx-version', '10.0.11', 'benchmarks/Xemele.Benchmarks/M8-xsd-attribute-fixed/bin/probe/net10.0/Xemele.Benchmarks.dll', '--control', str(out / 'control.json')], cwd=root, env=env, check=True, timeout=20)
control = json.loads((out / 'control.json').read_text())
assert control['mode'] == '--control' and control['status'] == 'passed' and control['runtime'] == '.NET 10.0.11'
assert len(control['rows']) == 12 and all(row['bytes'] == 0 and row['operations'] == 64 for row in control['rows'])
assert control['coldSetupBytes'] == control['positiveBytes'] == control['retainedBefore'] == control['retainedAfterResults'] == 0
assert control['retainedResults'] == 16 and control['consumed'] == 1456
paths = {net, root / 'src/Xemele/bin/Release/net10.0/Xemele.dll', out / 'control.json'}
for folder in [root / 'benchmarks/Xemele.Benchmarks/M8-xsd-attribute-fixed/bin/probe/net10.0', net.parent / 'shared/Microsoft.NETCore.App/10.0.11', net.parent / 'host/fxr']:
    for path in folder.rglob('*'):
        if path.is_file() and (path.suffix in {'.dll', '.dylib', '.so'} or path.name.endswith(('.deps.json', '.runtimeconfig.json'))): paths.add(path)
for relative in subprocess.check_output(['git', 'ls-files', 'src/Xemele'], cwd=root, text=True).splitlines():
    if relative.endswith(('.cs', '.csproj')): paths.add(root / relative)
for folder in ['tests/Xemele.Tests/M8-xsd-attribute-fixed', 'tests/fixtures/M8-xsd-attribute-fixed', 'benchmarks/Xemele.Benchmarks/M8-xsd-attribute-fixed', 'docs/reports/M8-xsd-attribute-fixed']:
    for path in (root / folder).rglob('*'):
        if path.is_file() and 'bin' not in path.parts and 'obj' not in path.parts and path.name not in ['preparation.json', 'proposal.md', 'allocations.md', 'preflight-summary.md']: paths.add(path)
for relative in ['global.json', 'Directory.Build.props', 'Directory.Packages.props', 'docs/reports/M8-xsd-facade/check.py', 'docs/reports/M8-xsd-facade/measure.py', 'docs/reports/M8-xsd-facade/XmlOnly/XmlOnly.csproj', 'samples/M8-xsd-facade/Facade.Consumer.csproj', 'samples/M8-xsd-facade/Program.cs']:
    paths.add(root / relative)
def digest(path):
    with path.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def key(path): return str(path.relative_to(root)) if path.is_relative_to(root) else str(path)
manifest = {'sourceRevision': revision, 'acceptedRuntimeRevision': accepted, 'maximumSeconds': 30, 'workSeconds': 20, 'cleanupReserveSeconds': 10,
    'probeRoutes': 12, 'warmups': 16, 'aggregateOperations': 64, 'retainedCount': 16,
    'environment': {'DOTNET_TieredCompilation': '0', 'DOTNET_gcServer': '0', 'DOTNET_GCConcurrent': '0'},
    'commands': json.loads(subprocess.check_output(['python3', 'docs/reports/M8-xsd-attribute-fixed/measure.py', '--commands'], cwd=root, text=True)),
    'identityCount': len(paths), 'identities': {key(path): digest(path) for path in sorted(paths)}}
path = root / 'docs/reports/M8-xsd-attribute-fixed/preparation.json'; path.write_text(json.dumps(manifest, indent=2) + '\n')
print(f'Prepared {len(paths)} identities; manifest {digest(path)}; all counters/GC/timing disabled.')
