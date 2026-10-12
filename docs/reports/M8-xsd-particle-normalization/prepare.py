"""Only after allocation SOURCE acceptance: offline build, disabled control, freeze.
No actual counters, workload timers, isolation or coordination with other projects.
"""
import hashlib, json, os, subprocess, time
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
flags = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
source = json.loads((root/'docs/reports/M8-xsd-particle-normalization/allocation-source.json').read_text())
review = json.loads((root/'docs/reports/M8-xsd-particle-normalization/allocation-source-review.json').read_text())
assert review['result'] == 'SOURCE PASS', 'Allocation source must be independently accepted first'
accepted = source['acceptedRuntimeRevision']
subprocess.run(['git', 'diff', '--exit-code', accepted, '--', 'src/Xemele', 'tests/Xemele.Tests/M8-xsd-particle-normalization', 'tests/fixtures/M8-xsd-particle-normalization'], cwd=root, check=True)
subprocess.run(['git', 'diff', '--exit-code', review['reviewedRevision'], '--', 'benchmarks/Xemele.Benchmarks/M8-xsd-particle-normalization', 'docs/reports/M8-xsd-particle-normalization/prepare.py', 'docs/reports/M8-xsd-particle-normalization/measure.py', 'docs/reports/M8-xsd-particle-normalization/lifecycle-check.py', 'docs/reports/M8-xsd-particle-normalization/allocation-source.json', 'docs/reports/M8-xsd-particle-normalization/allocation-design.md'], cwd=root, check=True)
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
project = 'benchmarks/Xemele.Benchmarks/M8-xsd-particle-normalization/Particles.Probe.csproj'
for verb in ['restore', 'build']:
 args = [str(net), verb, project, *(['--source', '/Users/sebastienros/.nuget/packages'] if verb == 'restore' else ['-c', 'Release', '--no-restore']), *flags]
 subprocess.run(args, cwd=root, check=True, timeout=180)
out = root/'artifacts/M8-xsd-particle-normalization/preparation'; out.mkdir(parents=True, exist_ok=False)
env = os.environ.copy(); env.update(DOTNET_TieredCompilation='0', DOTNET_gcServer='0', DOTNET_GCConcurrent='0', XEMELE_PARTICLES_REVISION=revision)
# Import definitions only; the CLI, preflight and counter paths are not executed.
script = (root/'docs/reports/M8-xsd-particle-normalization/measure.py').read_text().split("if sys.argv[1:] == ['--commands']:", 1)[0]
module = {'__file__': str(root/'docs/reports/M8-xsd-particle-normalization/measure.py')}
exec(compile(script, 'measure.py', 'exec'), module); module['ARTIFACTS'] = out
start = time.monotonic()
record = module['supervise_command']([str(net), '--fx-version', '10.0.11', str(root/'benchmarks/Xemele.Benchmarks/M8-xsd-particle-normalization/bin/probe/net10.0/Xemele.Benchmarks.dll'), '--control', str(out/'control.json')], 0, env, start+30, start+40)
(out/'lifecycle.json').write_text(json.dumps(record, indent=2)+'\n')
assert module['workload_succeeded'](record), record
control = json.loads((out/'control.json').read_text())
assert control['mode'] == '--control' and control['status'] == 'passed' and control['runtime'] == '.NET 10.0.11' and len(control['rows']) == 24
assert all(row['bytes'] == row['firstRunBytes'] == 0 and row['operations'] == 64 for row in control['rows'])
assert control['coldSetupBytes'] == control['positiveBytes'] == 0 and control['retainedResults'] == 40
assert len(control['heap']) == 5 and all(row['heapBefore'] == row['heapAfter'] == row['delta'] == 0 and row['count'] == 8 for row in control['heap'])
assert all(all(sample == 0 for sample in row['distribution']) and len(row['distribution']) == 64 for row in control['rows'])
paths = {net, root/'src/Xemele/bin/Release/net10.0/Xemele.dll', out/'control.json', out/'lifecycle.json'}
for folder in [root/'benchmarks/Xemele.Benchmarks/M8-xsd-particle-normalization/bin/probe/net10.0', net.parent/'shared/Microsoft.NETCore.App/10.0.11', net.parent/'host/fxr']:
 for p in folder.rglob('*'):
  if p.is_file() and (p.suffix in {'.dll', '.dylib', '.so'} or p.name.endswith(('.deps.json', '.runtimeconfig.json'))): paths.add(p)
for relative in subprocess.check_output(['git', 'ls-files', 'src/Xemele'], cwd=root, text=True).splitlines():
 if relative.endswith(('.cs', '.csproj')): paths.add(root/relative)
for folder in ['tests/Xemele.Tests/M8-xsd-particle-normalization', 'benchmarks/Xemele.Benchmarks/M8-xsd-particle-normalization', 'tests/fixtures/M8-xsd-particle-normalization']:
 for p in (root/folder).rglob('*'):
  if p.is_file() and 'bin' not in p.parts and 'obj' not in p.parts: paths.add(p)
for relative in ['global.json', 'Directory.Build.props', 'Directory.Packages.props', 'docs/reports/M8-xsd-particle-normalization/prepare.py', 'docs/reports/M8-xsd-particle-normalization/measure.py', 'docs/reports/M8-xsd-particle-normalization/check.py', 'docs/reports/M8-xsd-particle-normalization/allocation-source-review.json', 'docs/reports/M8-xsd-particle-normalization/allocation-source.json', 'docs/reports/M8-xsd-particle-normalization/allocation-design.md', 'docs/reports/M8-xsd-particle-normalization/lifecycle-check.py', 'docs/reports/M8-xsd-particle-normalization/source-identities.json', 'docs/reports/M8-xsd-particle-normalization/batch-internal-additions.txt']:
 paths.add(root/relative)
def digest(path):
 with path.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def key(path): return str(path.relative_to(root)) if path.is_relative_to(root) else str(path)
manifest = {'sourceRevision': revision, 'acceptedRuntimeRevision': accepted, 'acceptedAllocationSourceRevision': review['reviewedRevision'], 'maximumSeconds': 120, 'workSeconds': 110, 'cleanupReserveSeconds': 10, 'probeRoutes': 24, 'zeroAllocationRoutes': 0, 'warmups': 16, 'aggregateOperations': 64, 'retainedCount': 40, 'environment': {'DOTNET_TieredCompilation': '0', 'DOTNET_gcServer': '0', 'DOTNET_GCConcurrent': '0'}, 'commands': module['commands'], 'firstRuns': 1, 'identityCount': len(paths), 'identities': {key(p): digest(p) for p in sorted(paths)}}
p = root/'docs/reports/M8-xsd-particle-normalization/preparation.json'; p.write_text(json.dumps(manifest, indent=2)+'\n')
print(f'Prepared {len(paths)} identities; manifest {digest(p)}; counters and workload timing disabled.')
