#!/usr/bin/env python3
"""Prepared single-case caller for reviewed guard controls; UNRUN.

Use a fresh case plan. The parent-death control kills this caller after hazard.json
appears, while the guard survives to own cleanup. Root verifies final receipt and
process absence independently. No command is launched on import.
"""
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

plan = Path(sys.argv[1]).resolve()
checker = Path(__file__).with_name('check.py')
child = subprocess.Popen([sys.executable, str(checker), '--guard', str(plan),
                          '--caller-pid', str(os.getpid())], start_new_session=True)

def stop(sig, frame):
    try:
        os.kill(child.pid, signal.SIGTERM)
    except ProcessLookupError:
        pass
signal.signal(signal.SIGINT, stop)
signal.signal(signal.SIGTERM, stop)
start = time.monotonic()
try:
    while child.poll() is None:
        # If the outer controller dies, stop the surviving guard's work promptly.
        if os.getppid() == 1:
            stop(signal.SIGTERM, None)
        if time.monotonic() - start >= 12:
            raise TimeoutError('Control launcher hard bound')
        time.sleep(.05)
    sys.exit(child.returncode)
except BaseException:
    stop(signal.SIGTERM, None)
    try:
        child.wait(timeout=1)
    except subprocess.TimeoutExpired:
        os.kill(child.pid, signal.SIGKILL)
        child.wait(timeout=1)
    raise
