"""Extract comparable medians from BDN full JSON; keep raw results beside summary."""
from pathlib import Path
import gzip
import json
import statistics
import sys

folder = Path(sys.argv[1])
manifest_path = folder.parent / "suite-revisions.json"
manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
rows = {}
for source in sorted(folder.glob('*-report-full*.json*')):
    payload = json.loads(gzip.decompress(source.read_bytes()) if source.suffix == ".gz" else source.read_text())
    for bench in payload['Benchmarks']:
        group = bench['Type'].split('.')[-1]
        rows.setdefault(group, []).append(bench)

if not rows:
    raise SystemExit('No full JSON reports found.')

def parameters(bench):
    return bench.get('Parameters', '')

def method(bench):
    return bench['Method']

def mean(bench):
    return bench['Statistics']['Mean']

def alloc(bench):
    return bench.get('Memory', {}).get('BytesAllocatedPerOperation', 0)

print('# Measured matrix summary\n')
print('All means are nanoseconds per whole operation. Three short iterations; provisional single-host data. Raw full JSON retains workload samples, error and standard deviation. Single-delimiter controls have different semantics.\n')
for group, data in rows.items():
    print(f'## {group} ({len(data)} measured cases)\n')
    if group in manifest.get('active', {}):
        print(f"Tested revision: `{manifest['active'][group]['revision']}`.\n")
    print('| Method | Mean range (ns/op) | Allocation range (B/op) |')
    print('|---|---:|---:|')
    for name in sorted({method(b) for b in data}):
        subset = [b for b in data if method(b) == name]
        times = [mean(b) for b in subset]
        allocations = [alloc(b) for b in subset]
        print(f'| {name} | {min(times):.3f}–{max(times):.3f} | {min(allocations):g}–{max(allocations):g} |')
    print()
    pairs = [('ByteCached','ByteSimple'), ('CharCached','CharSimple')] if group == 'ScanBenchmarks' else [('ByteHash','ByteLength'),('CharHash','CharLength')] if group in ('MatchBenchmarks','ChunkMatchBenchmarks') else []
    for numerator, denominator in pairs:
        lookup = {(method(b), parameters(b)): b for b in data}
        paired = [b for b in data if method(b) == numerator]
        usable = [b for b in paired if mean(lookup[(denominator, parameters(b))]) >= 1.0]
        ratios = [mean(b)/mean(lookup[(denominator, parameters(b))]) for b in usable]
        print(f'{numerator}/{denominator} ratio across identical parameter sets: min {min(ratios):.3f}, median {statistics.median(ratios):.3f}, max {max(ratios):.3f}; below 1 is faster; {len(paired)-len(usable)} subnanosecond denominator(s) omitted (empty-harness resolution); all raw cases retained.\n')

print(f'Total: {sum(map(len,rows.values()))} benchmark cases. No parser throughput claim.\n')
