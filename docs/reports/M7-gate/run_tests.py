#!/usr/bin/env python3
"""Run only under a prepared exclusive coordinator grant; never builds."""
import json
import os
import pathlib
import signal
import subprocess
import sys
import time
root = pathlib.Path(__file__).resolve().parents[3]
manifest = root / 'artifacts/M7-gate/prepared.json'
plan = json.loads((root / 'docs/reports/M7-gate/test-plan.json').read_text())
if sys.argv[1:] not in (['--exclusive-grant'], ['--identity-control']):
    raise SystemExit('Requires the coordinator prepared exclusive grant; this flag is not itself authorization.')
deadline = time.monotonic() + plan['total_max_seconds']
freeze = ['python3', str(root / 'docs/reports/M7-gate/freeze.py'), 'check', str(manifest)]
status = {'phase': 'initial-freeze', 'exit_code': 0, 'initial_freeze': None, 'final_freeze': None, 'no_child': True, 'runs': []}
process = None
process_groups = []
process_handles = []
def bounded(seconds, reserve=0):
    remaining = deadline - time.monotonic() - reserve
    if remaining <= 0:
        raise TimeoutError('Total prepared sequence deadline exceeded.')
    return min(seconds, remaining)
def identity(seconds, reserve=0):
    child = subprocess.Popen(freeze, cwd=root, start_new_session=True)
    process_groups.append(child.pid)
    process_handles.append(child)
    return child.wait(timeout=bounded(seconds, reserve))
def cleanup():
    for group in process_groups:
        try: os.killpg(group, signal.SIGKILL)
        except ProcessLookupError: pass
    for child in process_handles:
        child.wait(timeout=5)
    for group in process_groups:
        result = subprocess.run(['ps', '-o', 'pid=,pgid=', '-g', str(group)], capture_output=True, text=True, timeout=5)
        if result.returncode not in (0, 1): raise RuntimeError('Scoped process-group inspection failed')
        if any(int(row.split()[1]) == group for row in result.stdout.splitlines()): status['no_child'] = False
try:
    status['initial_freeze'] = identity(plan['initial_identity_max_seconds'], 130)
    if status['initial_freeze'] != 0: raise subprocess.CalledProcessError(status['initial_freeze'], freeze)
    environment = dict(os.environ)
    environment.update(plan['environment'])
    # Stable effective environment is frozen by freeze.py.
    for run in ([] if sys.argv[1:] == ['--identity-control'] else plan['runs']):
        status['phase'] = run['name']
        result = {'phase': run['name'], 'exit_code': None}
        status['runs'].append(result)
        with (root / run['log']).open('w') as output:
            # NoEmit runs in this process. A separate group also makes timeout
            # cleanup cover any unexpected descendants without touching peers.
            process = subprocess.Popen(run['command'], cwd=root, env=environment, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
            process_groups.append(process.pid)
            process_handles.append(process)
            result['exit_code'] = process.wait(timeout=bounded(run['max_seconds'], 130))
        if result['exit_code'] != 0:
            raise subprocess.CalledProcessError(result['exit_code'], run['command'])
        process = None
        print(run['name'] + ' completed', flush=True)
    status['phase'] = 'complete'
except (subprocess.TimeoutExpired, TimeoutError) as error:
    status['exit_code'] = 124
    status['error'] = type(error).__name__
except BaseException as error:
    status['exit_code'] = getattr(error, 'returncode', 1) or 1
    status['error'] = type(error).__name__
finally:
    # Always attempt cleanup and the complete final identity check, including
    # initial identity failures, product assertions, interrupts and timeouts.
    try:
        cleanup()
    except BaseException as error:
        status['no_child'] = False
        status['cleanup_error'] = type(error).__name__
    try:
        status['final_freeze'] = identity(plan['final_identity_max_seconds'])
    except BaseException as error:
        status['final_freeze'] = type(error).__name__
    finally:
        try:
            cleanup()
        except BaseException as error:
            status['no_child'] = False
            status['final_cleanup_error'] = type(error).__name__
    if status['final_freeze'] != 0 or not status['no_child']:
        status['exit_code'] = status['exit_code'] or 1
    # Report lifecycle evidence before anyone inspects ignored raw results.
    print(json.dumps(status, sort_keys=True), flush=True)
raise SystemExit(status['exit_code'])
