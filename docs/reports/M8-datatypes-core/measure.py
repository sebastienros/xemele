"""Run only after coordinator's durable grant. No result inspection in this driver."""
import json, os, signal, subprocess, sys, time
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
out = root / 'artifacts/M8-datatypes-core/measurement'; out.mkdir(parents=True, exist_ok=True)
commands = [
 [net, '--fx-version', '10.0.11', str(root / 'benchmarks/Xemele.Benchmarks/M8-datatypes-core/bin/probe/net10.0/Xemele.Benchmarks.dll'), '--measure', str(out / 'allocations.json')],
 [net, '--fx-version', '10.0.11', str(root / 'benchmarks/Xemele.Benchmarks/M8-datatypes-core/bin/Release/net10.0/Xemele.Benchmarks.dll'), '--benchmark', str(out / 'bdn')]
]
if sys.argv[1:] == ['--commands']:
 print(json.dumps(commands, indent=2)); sys.exit(0)
if sys.argv[1:] != ['--granted']:
 raise SystemExit('Requires explicit coordinator grant before --granted.')
env = os.environ.copy()
env['PATH'] = str(Path(net).parent) + os.pathsep + env.get('PATH', '')
env['XEMELE_DATATYPE_CORE_REVISION'] = json.loads((root / 'docs/reports/M8-datatypes-core/preparation.json').read_text())['sourceRevision']
env['NuGetAudit'] = 'false'
env['RuntimeFrameworkVersion'] = '10.0.11'
env['UseSharedCompilation'] = 'false'
preflight = subprocess.check_output(['ps', '-axo', 'pid=,ppid=,pgid=,stat='], text=True).splitlines()
assert any(int(row.split()[0]) == os.getpid() for row in preflight if row.strip()), 'Process permission preflight failed.'
end = time.monotonic() + 600
lifecycle = []
status = 0
for index, argv in enumerate(commands):
 with (out / f'command-{index}.log').open('wb') as log:
  process = subprocess.Popen(argv, cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
  reason = 'completed'
  try:
   code = process.wait(timeout=max(1, end-time.monotonic()))
   if code: reason = 'failed'
  except BaseException as error:
   reason = type(error).__name__; code = -1
  # Kill the whole group on every exit: reap the direct child and drain any
  # surviving grandchildren before declaring RELEASE, including failed builds.
  try: os.killpg(process.pid, signal.SIGTERM)
  except ProcessLookupError: pass
  try: process.wait(timeout=5)
  except subprocess.TimeoutExpired:
   try: os.killpg(process.pid, signal.SIGKILL)
   except ProcessLookupError: pass
   process.wait()
  try: os.killpg(process.pid, signal.SIGKILL)
  except ProcessLookupError: pass
  rows = subprocess.check_output(['ps', '-axo', 'pid=,ppid=,pgid=,stat='], text=True).splitlines()
  owned = [row.split() for row in rows if row.strip() and int(row.split()[2]) == process.pid]
  if owned: raise RuntimeError('Owned process group remains after kill/reap.')
  lifecycle.append({'ownedGroupAbsent': True, 'command': index, 'pid': process.pid, 'exit': code, 'reason': reason, 'directChildReaped': process.returncode is not None})
  if code: status = 1; break
(out / 'lifecycle.json').write_text(json.dumps({'status': status, 'lifecycle': lifecycle}, indent=2))
print('LIFECYCLE RELEASE: approved sequence stopped; process groups terminated; direct children reaped; results not inspected.', flush=True)
sys.exit(status)
