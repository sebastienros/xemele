"""Finite supervisor regression: timeout, SIGTERM -> exit0, clean but failed.
Imports only the real supervisor definitions; no CLI/measurement path executes.
"""
import json, os, subprocess, sys, tempfile, time
from pathlib import Path
path = Path(__file__).with_name('measure.py')
source = path.read_text().split("if sys.argv[1:] == ['--commands']:", 1)[0]
module = {'__file__': str(path.resolve())}
exec(compile(source, str(path), 'exec'), module)
with tempfile.TemporaryDirectory(prefix='relations-lifecycle-', dir='/private/tmp') as tmp:
    directory = Path(tmp); module['ARTIFACTS'] = directory
    ready = directory / 'ready'; next_child = directory / 'next-child'
    first = [sys.executable, '-c', 'import signal,sys,time; from pathlib import Path; signal.signal(signal.SIGTERM,lambda *_:sys.exit(0)); Path(sys.argv[1]).write_text("ready"); time.sleep(5)', str(ready)]
    second = [sys.executable, '-c', 'import sys; from pathlib import Path; Path(sys.argv[1]).write_text("unexpected")', str(next_child)]
    start = time.monotonic()
    sequence = module['run_commands']([first, second], os.environ.copy(), start + .5, start + 3)
    assert ready.is_file(), 'Timeout fixture must enter its SIGTERM handler before deadline'
    assert len(sequence) == 1 and not next_child.exists(), 'No next child may launch after timeout'
    record = sequence[0]
    assert record['reason'] == 'TimeoutExpired' and record['exit'] == 0, record
    assert record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'], record
    assert not module['workload_succeeded'](record), 'Clean SIGTERM exit0 is not completed workload'
    # A normal child independently verifies that completed remains success.
    normal = module['supervise_command']([sys.executable, '-c', 'pass'], 2, os.environ.copy(), start + 2, start + 3)
    assert module['workload_succeeded'](normal), normal
    assert time.monotonic() - start < 3
    result = {'status': 'passed', 'counterEnabled': False, 'timeoutSequence': sequence, 'nextChildStarted': False, 'normal': normal}
    Path(__file__).with_name('lifecycle-check.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, indent=2))
