"""Run only after a durable grant; supervise identities/cleanup without inspecting results."""
import hashlib, json, os, signal, subprocess, sys, time
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
out = root / 'artifacts/M8-xsd-content/measurement-2'
manifest_path = root / 'docs/reports/M8-xsd-content/preparation.json'
commands = [
 [net, '--fx-version', '10.0.11', str(root / 'benchmarks/Xemele.Benchmarks/M8-xsd-content/bin/probe/net10.0/Xemele.Benchmarks.dll'), '--measure', str(out / 'allocations.json')],
 [net, '--fx-version', '10.0.11', str(root / 'benchmarks/Xemele.Benchmarks/M8-xsd-content/bin/Release/net10.0/Xemele.Benchmarks.dll'), '--benchmark', str(out / 'bdn')]
]
def digest(path):
 with Path(path).open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def identities(manifest):
 mismatches = []
 for relative, expected in manifest['identities'].items():
  path = Path(relative); path = path if path.is_absolute() else root / path
  try:
   if digest(path) != expected: mismatches.append(relative)
  except OSError: mismatches.append(relative)
 return mismatches
def processes():
 text = subprocess.check_output(['ps', '-axo', 'pid=,ppid=,pgid=,stat='], text=True, timeout=2)
 return [line.split() for line in text.splitlines() if line.strip()]
def group_rows(pid): return [row for row in processes() if int(row[2]) == pid]
def kill_group(pid, sig):
 try: os.killpg(pid, sig)
 except ProcessLookupError: pass

def supervise(argv, index, env, work_deadline, hard_deadline):
 record = {'command': index, 'pid': None, 'exit': None, 'reason': 'not-started', 'directChildReaped': False, 'ownedGroupAbsent': False, 'logClosed': False}
 process = None
 try:
  with (out / f'command-{index}.log').open('wb') as log:
   try:
    if time.monotonic() >= work_deadline: raise TimeoutError('Work deadline reached.')
    process = subprocess.Popen(argv, cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    record['pid'] = process.pid
    record['exit'] = process.wait(timeout=max(.001, work_deadline-time.monotonic()))
    record['reason'] = 'completed' if record['exit'] == 0 else 'failed'
   except BaseException as error:
    record['reason'] = type(error).__name__
   finally:
    if process is not None:
     kill_group(process.pid, signal.SIGTERM)
     try: process.wait(timeout=max(.001, min(2, hard_deadline-time.monotonic())))
     except subprocess.TimeoutExpired:
      kill_group(process.pid, signal.SIGKILL)
      process.wait(timeout=max(.001, min(2, hard_deadline-time.monotonic())))
     kill_group(process.pid, signal.SIGKILL)
     record['directChildReaped'] = process.returncode is not None
     while True:
      remaining = group_rows(process.pid)
      if not remaining: record['ownedGroupAbsent'] = True; break
      if time.monotonic() >= hard_deadline-2: raise TimeoutError('Owned group did not drain.')
      kill_group(process.pid, signal.SIGKILL); time.sleep(.05)
  record['logClosed'] = True
 except BaseException as error:
  record['cleanupError'] = type(error).__name__
 return record

if sys.argv[1:] == ['--commands']:
 print(json.dumps(commands, indent=2)); sys.exit(0)
if sys.argv[1:] == ['--verify-only']:
 manifest = json.loads(manifest_path.read_text()); mismatches = identities(manifest)
 assert not mismatches, mismatches
 assert any(int(row[0]) == os.getpid() for row in processes())
 print(f"PASS: {len(manifest['identities'])} prepared identities and ps permission; no child/counter/timing started; manifest {digest(manifest_path)}")
 sys.exit(0)
if len(sys.argv) != 5 or sys.argv[1] != '--granted':
 raise SystemExit('Requires --granted <durably approved manifest SHA256> <latest-start UTC> <absolute UTC expiry>.')
from datetime import datetime, timezone
latest_start = datetime.fromisoformat(sys.argv[3].replace('Z','+00:00'))
expires = datetime.fromisoformat(sys.argv[4].replace('Z','+00:00'))
if any(value.utcoffset() is None or value.utcoffset().total_seconds() != 0 for value in (latest_start, expires)):
 raise SystemExit('Explicit UTC latest-start and expiry required.')
now = datetime.now(timezone.utc)
if now > latest_start: raise SystemExit('Latest start passed; no directory or child created.')
if expires <= latest_start: raise SystemExit('Expiry must follow latest start; no directory or child created.')
remaining = (expires - now).total_seconds()
if remaining <= 10: raise SystemExit('Grant expired or no cleanup reserve remains; no child started.')
if out.exists(): raise SystemExit('Measurement directory already exists; refusing to overwrite prior output.')
started = time.monotonic(); hard_deadline = started + min(600, remaining); work_deadline = hard_deadline - 10
out.mkdir(parents=True, exist_ok=False)
lifecycle = {'status': 1, 'beforeIdentities': False, 'afterIdentities': False, 'sequence': [], 'rawResultsInspected': False,
 'latestStartUtc': latest_start.isoformat(), 'grantExpiryUtc': expires.isoformat(), 'maximumSeconds': min(600, remaining)}
manifest = None
try:
 if digest(manifest_path) != sys.argv[2]: raise ValueError('Manifest differs from durable grant.')
 manifest = json.loads(manifest_path.read_text())
 if manifest['commands'] != commands or manifest['maximumSeconds'] != 600: raise ValueError('Command/bound mismatch.')
 mismatches = identities(manifest)
 lifecycle['beforeMismatches'] = mismatches
 if mismatches: raise ValueError('Prepared identities changed before measurement.')
 lifecycle['beforeIdentities'] = True; lifecycle['identityCount'] = len(manifest['identities'])
 if not any(int(row[0]) == os.getpid() for row in processes()): raise ValueError('Process permission preflight failed.')
 env = os.environ.copy(); env['PATH'] = str(Path(net).parent) + os.pathsep + env.get('PATH', '')
 env.update(manifest['environment'])
 env['XEMELE_CONTENT_REVISION'] = manifest['sourceRevision']
 env['NuGetAudit'] = 'false'; env['RuntimeFrameworkVersion'] = '10.0.11'; env['UseSharedCompilation'] = 'false'
 for index, argv in enumerate(commands):
  record = supervise(argv, index, env, work_deadline, hard_deadline); lifecycle['sequence'].append(record)
  if record['exit'] != 0 or not record['directChildReaped'] or not record['ownedGroupAbsent'] or not record['logClosed']: break
 else: lifecycle['status'] = 0
except BaseException as error:
 lifecycle['error'] = type(error).__name__
finally:
 try:
  if manifest is not None:
   mismatches = identities(manifest); lifecycle['afterMismatches'] = mismatches
   lifecycle['afterIdentities'] = not mismatches and digest(manifest_path) == sys.argv[2]
  if not lifecycle['afterIdentities']: lifecycle['status'] = 1
 except BaseException as error:
  lifecycle['status'] = 1; lifecycle['afterError'] = type(error).__name__
 lifecycle['elapsedSeconds'] = time.monotonic()-started
 if time.monotonic() > hard_deadline: lifecycle['status'] = 1; lifecycle['deadlineExceeded'] = True
 (out / 'lifecycle.json').write_text(json.dumps(lifecycle, indent=2))
 clean = all(row['directChildReaped'] and row['ownedGroupAbsent'] and row['logClosed'] for row in lifecycle['sequence'])
 print(f"LIFECYCLE {'RELEASE' if clean else 'CLEANUP FAILURE'}: status={lifecycle['status']}; identityCount={lifecycle.get('identityCount',0)}; children reaped/groups absent/logs closed={clean}; raw results unopened.", flush=True)
sys.exit(lifecycle['status'])
