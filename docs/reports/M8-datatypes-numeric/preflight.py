"""Counters-disabled process permission and direct-child cleanup preflight."""
import os, subprocess
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
def process_rows():
 text = subprocess.check_output(['ps', '-axo', 'pid=,ppid=,pgid=,stat='], text=True)
 return [line.split() for line in text.splitlines()]
assert any(int(row[0]) == os.getpid() for row in process_rows())
out = root / 'artifacts/M8-datatypes-numeric/preflight-control.json'
child = subprocess.Popen([net, '--fx-version', '10.0.11', str(root / 'benchmarks/Xemele.Benchmarks/M8-datatypes-numeric/bin/probe/net10.0/Xemele.Benchmarks.dll'), '--control', str(out)], cwd=root, start_new_session=True)
assert child.wait(timeout=30) == 0
assert not any(int(row[2]) == child.pid for row in process_rows()), 'Owned process group still exists.'
print('PASS:ps permission; counters-disabled probe consumed; child reaped; owned group absent. Measurement requires same elevated execution permission.')
