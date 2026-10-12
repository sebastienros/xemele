"""Finite owned-process cleanup reused from verified particle/type supervision.

Source: docs/reports/M8-xsd-particle-normalization/measure.py at verified
b2395ffa3f7cf17da2a9d39d35f1486b536cab6b. No allocation environment or commands.
The supervisor body below is unchanged; the caller sets its owned artifact root
and supplies monotonic work/hard deadlines. Tests exercise real owned children.
"""
import os, signal, subprocess, time
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
ARTIFACTS = None

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

def workload_succeeded(record):
 return (record['reason'] == 'completed' and record['exit'] == 0 and
         record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'])

def run_commands(argvs, environment, work_deadline, hard_deadline):
 sequence = []
 for index, argv in enumerate(argvs):
  record = supervise_command(argv, index, environment, work_deadline, hard_deadline)
  sequence.append(record)
  if not workload_succeeded(record): break
 return sequence
