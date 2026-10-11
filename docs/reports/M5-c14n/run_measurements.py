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
manifest = root / 'artifacts/M5-c14n/prepared.json'
plan = json.loads((root / 'docs/reports/M5-c14n/measurement-plan.json').read_text())
if sys.argv[1:] != ['--exclusive-grant']:
    raise SystemExit('Requires the coordinator prepared exclusive grant; this flag is not itself authorization.')
deadline = time.monotonic() + plan['total_max_seconds']
freeze = ['python3', str(root / 'docs/reports/M5-c14n/freeze.py'), 'check', str(manifest)]
status = {'phase': 'initial-freeze', 'exit_code': 0, 'initial_freeze': None, 'final_freeze': None, 'no_child': True, 'runs': []}
process = None
process_groups = []
def bounded(seconds, reserve=0):
    remaining = deadline - time.monotonic() - reserve
    if remaining <= 0:
        raise TimeoutError('Total prepared sequence deadline exceeded.')
    return min(seconds, remaining)
try:
    initial = subprocess.run(freeze, cwd=root, timeout=bounded(plan['initial_identity_max_seconds'], 50), check=False)
    status['initial_freeze'] = initial.returncode
    initial.check_returncode()
    environment = dict(os.environ)
    environment.update(plan['environment'])
    environment['XEMELE_C14N_REVISION'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True, timeout=bounded(5, 50)).strip()
    for run in plan['runs']:
        status['phase'] = run['name']
        result = {'phase': run['name'], 'exit_code': None}
        status['runs'].append(result)
        with (root / run['log']).open('w') as output:
            # NoEmit runs in this process. A separate group also makes timeout
            # cleanup cover any unexpected descendants without touching peers.
            process = subprocess.Popen(run['command'], cwd=root, env=environment, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
            process_groups.append(process.pid)
            result['exit_code'] = process.wait(timeout=bounded(run['max_seconds'], 50))
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
        for group in process_groups:
            try:
                os.killpg(group, signal.SIGKILL)
            except ProcessLookupError:
                pass
        if process is not None:
            process.wait(timeout=bounded(5, 35))
            status['runs'][-1]['exit_code'] = process.returncode
        status['no_child'] = True
        for group in process_groups:
            scoped = subprocess.run(['/bin/ps', '-g', str(group), '-o', 'pid='], text=True, capture_output=True, timeout=bounded(5, 35), check=False)
            if scoped.returncode not in (0, 1) or scoped.stdout.strip():
                status['no_child'] = False
    except BaseException as error:
        status['no_child'] = False
        status['cleanup_error'] = type(error).__name__
    try:
        final = subprocess.run(freeze, cwd=root, timeout=bounded(plan['final_identity_max_seconds']), check=False)
        status['final_freeze'] = final.returncode
    except BaseException as error:
        status['final_freeze'] = type(error).__name__
    if status['final_freeze'] != 0 or not status['no_child']:
        status['exit_code'] = status['exit_code'] or 1
    # Report lifecycle evidence before anyone inspects ignored raw results.
    print(json.dumps(status, sort_keys=True), flush=True)
raise SystemExit(status['exit_code'])
