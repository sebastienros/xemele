"""Prepare identities of prebuilt payloads. Never builds or measures."""
import hashlib, json, subprocess
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
manifest_path = root / 'docs/reports/M8-datatypes-temporal/preparation.json'
paths = {net, root / 'src/Xemele/bin/Release/net10.0/Xemele.dll'}
for folder in ['benchmarks/Xemele.Benchmarks/M8-datatypes-temporal/bin/probe/net10.0', 'benchmarks/Xemele.Benchmarks/M8-datatypes-temporal/bin/Release/net10.0']:
    for path in (root / folder).rglob('*'):
        if path.is_file() and (path.suffix in {'.dll', '.dylib', '.so'} or path.name.endswith(('.deps.json', '.runtimeconfig.json'))): paths.add(path)
for folder in [net.parent / 'shared/Microsoft.NETCore.App/10.0.11', net.parent / 'host/fxr']:
    for path in folder.rglob('*'):
        if path.is_file() and (folder.name != 'fxr' or path.name == 'libhostfxr.dylib'): paths.add(path)
# Hash actual compiled source/config without loading unrelated source into chat.
for path in (root / 'src/Xemele').rglob('*'):
    if path.is_file() and path.suffix in {'.cs', '.csproj'} and not {'bin', 'obj'}.intersection(path.parts): paths.add(path)
for folder in ['benchmarks/Xemele.Benchmarks/M8-datatypes-temporal', 'tests/fixtures/M8-datatypes-temporal',
               'tests/Xemele.Tests/M8-datatypes-temporal', 'tests/Xemele.Tests/M8-datatypes-core', 'tests/Xemele.Tests/M8-model']:
    for path in (root / folder).iterdir():
        if path.is_file(): paths.add(path)
for path in root.glob('Directory.*'): paths.add(path)
for name in ['global.json', 'NuGet.Config']:
    if (root / name).exists(): paths.add(root / name)
for name in ['measure.py', 'prepare.py', 'preflight.py', 'Consumer.cs.fixture', 'Consumer.csproj', 'checks.py', 'accounting.md', 'normative.md']:
    paths.add(root / 'docs/reports/M8-datatypes-temporal' / name)
paths.add(root / 'docs/reports/M8-datatypes-core/contracts.md')
def digest(path):
    with path.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def key(path): return str(path.relative_to(root)) if path.is_relative_to(root) else str(path)
manifest = {
    'sequence': 'temporal-family-prepared-v1',
    'sourceRevision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
    'runtime': '.NET10.0.11 Arm64', 'sdk': '10.0.400',
    'maximumSeconds': 600, 'workSeconds': 590, 'cleanupReserveSeconds': 10,
    'identityScope': 'Actual complete library source/config; both copied payloads and dependency binaries/runtimeconfigs/deps; pinned dotnet host, hostfxr candidates and whole10.0.11 runtime; probe/control/benchmark/input/supervisor source.',
    'identities': {key(p): digest(p) for p in sorted(paths)},
    'identityCount': len(paths),
    'commands': json.loads(subprocess.check_output(['python3', str(root / 'docs/reports/M8-datatypes-temporal/measure.py'), '--commands'], text=True)),
    'routes': '12 allocation distributions:6borrowed compare/hash+6owned parse/invalid/setup;32warm,64samples; positive1024byte control; cold setup;128retained long calendars.',
    'benchmarks': '8ShortRunMemoryDiagnoserInProcessNoEmit: fourcanonicaltimefraction cases x scalar oracle/provider compare; provisional microbenchmark, no speedup claim or generated builds.',
    'controls': 'Counter-disabled12route probe and4baseline/provider controls; exact8NoEmit discovery; require_escalated ps permission preflight with direct child reaped and owned process group absent.',
    'rawInspection': 'Forbidden until durable root RELEASE ACK; lifecycle summary only immediately after supervisor exits.'
}
manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
print(f'Prepared {len(paths)} identities; manifest {digest(manifest_path)}; no binaries changed or measurements invoked.')
