#!/usr/bin/env python3
"""Execute only inside an explicitly granted exclusive coordinator lease."""
from pathlib import Path
import subprocess
import time

root = Path(__file__).resolve().parents[3]
report = root / 'docs/reports/M3-sax'
dotnet = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
commands = [
    ([dotnet, 'docs/reports/M3-sax/Probe/bin/Release/net10.0/Xemele.Benchmarks.dll', '--allocation', '8388608'], 'allocation.txt'),
    ([dotnet, 'benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll', '--filter', '*Xemele.Benchmarks.Sax.SaxBenchmarks*', '--job', 'Dry', '--inProcess', '--artifacts', 'docs/reports/M3-sax/bdn'], 'benchmark.txt'),
]
deadline = time.monotonic() + 90
for command, filename in commands:
    with (report / filename).open('w') as output:
        result = subprocess.run(command, cwd=root, stdout=output, stderr=subprocess.STDOUT,
                                timeout=max(1, min(30 if filename == 'allocation.txt' else 90, deadline - time.monotonic())))
    print(f'{filename}: exit={result.returncode}', flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)
