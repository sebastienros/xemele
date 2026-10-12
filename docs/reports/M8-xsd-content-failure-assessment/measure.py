"""Prebuilt allocation-only lifecycle; copied SCH supervision, no raw inspection."""
import hashlib, json, os, signal, subprocess, sys, time
from datetime import datetime, timezone
from pathlib import Path
root = Path(__file__).resolve().parents[3]
ROOT = root
net = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
out = root / 'artifacts/M8-xsd-content-failure-assessment/measurement-1'
ARTIFACTS = out
manifest_path = root / 'docs/reports/M8-xsd-content-failure-assessment/preparation.json'
commands = [
 [net, '--fx-version', '10.0.11', str(root / 'benchmarks/Xemele.Benchmarks/M8-xsd-content-failure-assessment/bin/probe/net10.0/Xemele.Benchmarks.dll'), '--control', str(out / 'control.json')],
 [net, '--fx-version', '10.0.11', str(root / 'benchmarks/Xemele.Benchmarks/M8-xsd-content-failure-assessment/bin/probe/net10.0/Xemele.Benchmarks.dll'), '--measure', str(out / 'allocations.json')]
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

def members(group):
    listing = subprocess.check_output(['ps', '-axo', 'pid,pgid'], text=True, timeout=2)
    return [int(pid) for pid, pgid in (line.split() for line in listing.splitlines()[1:]) if int(pgid) == group]


def kill_group(pid, sig):
    try:
        os.killpg(pid, sig)
    except ProcessLookupError:
        pass


def supervise(argv, environment, work_deadline, hard_deadline, record, log_name="allocation.log"):
    child = None
    log = None
    try:
        log = (ARTIFACTS / log_name).open('xb')
        if time.monotonic() >= work_deadline:
            raise TimeoutError('Work deadline reached')
        child = subprocess.Popen(argv, cwd=ROOT, env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        record['pid'] = child.pid
        record['exit'] = child.wait(timeout=max(.001, work_deadline - time.monotonic()))
        record['reason'] = 'completed' if record['exit'] == 0 else 'failed'
    except BaseException as error:
        record['reason'] = type(error).__name__
    finally:
        try:
            if child is not None:
                kill_group(child.pid, signal.SIGTERM)
                try:
                    child.wait(timeout=max(.001, min(1, hard_deadline - time.monotonic())))
                except subprocess.TimeoutExpired:
                    kill_group(child.pid, signal.SIGKILL)
                    child.wait(timeout=max(.001, min(1, hard_deadline - time.monotonic())))
                kill_group(child.pid, signal.SIGKILL)
                record['exit'] = child.returncode
                record['directChildReaped'] = child.returncode is not None
                while True:
                    remaining = members(child.pid)
                    record['groupMembers'] = remaining
                    if not remaining:
                        record['ownedGroupAbsent'] = True
                        break
                    if time.monotonic() >= hard_deadline - 2:
                        raise TimeoutError('Owned group did not drain')
                    kill_group(child.pid, signal.SIGKILL)
                    time.sleep(.05)
        except BaseException as error:
            record['cleanupError'] = type(error).__name__
        finally:
            if log is not None:
                log.close()
                record['logClosed'] = log.closed
            else:
                record['logClosed'] = True
            if child is None:
                record['directChildReaped'] = True
                record['ownedGroupAbsent'] = True


def supervise_command(argv, index, environment, work_deadline, hard_deadline):
 record = {'command': index, 'pid': None, 'exit': None, 'reason': 'not-started', 'directChildReaped': False,
           'ownedGroupAbsent': False, 'logClosed': False, 'groupMembers': []}
 supervise(argv, environment, work_deadline, hard_deadline, record, f'command-{index}.log')
 return record

if sys.argv[1:] == ['--commands']:
 print(json.dumps(commands, indent=2)); sys.exit(0)
if sys.argv[1:] == ['--preflight']:
 manifest = json.loads(manifest_path.read_text())
 assert not identities(manifest), 'Frozen identity mismatch before preflight.'
 out = root / 'artifacts/M8-xsd-content-failure-assessment/preflight'
 out.mkdir(parents=True, exist_ok=False)
 ARTIFACTS = out
 env = os.environ.copy(); env.update(DOTNET_TieredCompilation='0', DOTNET_gcServer='0', DOTNET_GCConcurrent='0')
 start = time.monotonic()
 argv = [net, '--fx-version', '10.0.11', str(root / 'benchmarks/Xemele.Benchmarks/M8-xsd-content-failure-assessment/bin/probe/net10.0/Xemele.Benchmarks.dll'), '--control', str(out / 'control.json')]
 record = supervise_command(argv, 0, env, start + 30, start + 40)
 assert not identities(manifest), 'Frozen identity mismatch after preflight.'
 (out / 'lifecycle.json').write_text(json.dumps(record, indent=2))
 clean = record['exit'] == 0 and record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed']
 print(f"PREFLIGHT: {record}; frozen identities {len(manifest['identities'])} matched; counters and benchmark timing disabled.")
 sys.exit(0 if clean else 1)
if sys.argv[1:] == ['--verify-only']:
 manifest = json.loads(manifest_path.read_text()); mismatches = identities(manifest)
 assert not mismatches, mismatches
 assert os.getpid() in members(os.getpgid(os.getpid()))
 print(f"PASS: {len(manifest['identities'])} prepared identities and ps permission, counters/timing disabled; manifest {digest(manifest_path)}")
 sys.exit(0)
if len(sys.argv) != 5 or sys.argv[1] != '--granted':
 raise SystemExit('Requires --granted <durably approved manifest SHA256> <latest-start UTC ISO> <absolute-expiry UTC ISO>.')
# The root supplies fresh durable lease bounds. A late start never extends them.
now = datetime.now(timezone.utc)
latest_start = datetime.fromisoformat(sys.argv[3].replace('Z','+00:00'))
expiry = datetime.fromisoformat(sys.argv[4].replace('Z','+00:00'))
if expiry.tzinfo is None or latest_start.tzinfo is None or now > latest_start or expiry <= now:
 raise SystemExit('Lease start deadline/expiry refused; no child or measurement started.')
remaining = min(120, (expiry-now).total_seconds())
if remaining <= 10: raise SystemExit('Lease cleanup reserve exhausted; no child started.')
started = time.monotonic(); hard_deadline = started + remaining; work_deadline = hard_deadline - 10
# No retry or accidental replacement of preserved raw results.
out.mkdir(parents=True, exist_ok=False)
lifecycle = {'status': 1, 'beforeIdentities': False, 'afterIdentities': False, 'sequence': [], 'rawResultsInspected': False, 'latestStart': sys.argv[3], 'absoluteExpiry': sys.argv[4]}
manifest = None
try:
 if digest(manifest_path) != sys.argv[2]: raise ValueError('Manifest differs from durable grant.')
 manifest = json.loads(manifest_path.read_text())
 if manifest['commands'] != commands or manifest['maximumSeconds'] != 120 or manifest['workSeconds'] != 110 or manifest['cleanupReserveSeconds'] != 10: raise ValueError('Command/bound mismatch.')
 mismatches = identities(manifest)
 lifecycle['beforeMismatches'] = mismatches
 if mismatches: raise ValueError('Prepared identities changed before measurement.')
 lifecycle['beforeIdentities'] = True; lifecycle['identityCount'] = len(manifest['identities'])
 if os.getpid() not in members(os.getpgid(os.getpid())): raise ValueError('Process permission preflight failed.')
 env = os.environ.copy(); env['PATH'] = str(Path(net).parent) + os.pathsep + env.get('PATH', '')
 env['XEMELE_CONTINUATION_REVISION'] = manifest['sourceRevision']
 env['DOTNET_TieredCompilation'] = '0'; env['DOTNET_gcServer'] = '0'; env['DOTNET_GCConcurrent'] = '0'
 env['NuGetAudit'] = 'false'; env['RuntimeFrameworkVersion'] = '10.0.11'; env['UseSharedCompilation'] = 'false'
 for index, argv in enumerate(commands):
  if datetime.now(timezone.utc) > latest_start: raise TimeoutError('Latest start elapsed; no child started.')
  record = supervise_command(argv, index, env, work_deadline, hard_deadline); lifecycle['sequence'].append(record)
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
 if lifecycle['elapsedSeconds'] > remaining: lifecycle['status'] = 1; lifecycle['deadlineExceeded'] = True
 for name, data in [('lifecycle.json', lifecycle), ('release.json', {'status': lifecycle['status'], 'releasedAt': datetime.now(timezone.utc).isoformat(), 'rawResultsInspected': False, 'sequence': lifecycle['sequence'], 'beforeIdentities': lifecycle['beforeIdentities'], 'afterIdentities': lifecycle['afterIdentities']})]:
  with (out / name).open('x') as stream:
   stream.write(json.dumps(data, indent=2) + '\n'); stream.flush(); os.fsync(stream.fileno())
 clean = all(row['directChildReaped'] and row['ownedGroupAbsent'] and row['logClosed'] for row in lifecycle['sequence'])
 print(f"LIFECYCLE {'RELEASE' if clean else 'CLEANUP FAILURE'}: status={lifecycle['status']}; identityCount={lifecycle.get('identityCount',0)}; children reaped/groups absent/logs closed={clean}; raw results unopened.", flush=True)
sys.exit(lifecycle['status'])
