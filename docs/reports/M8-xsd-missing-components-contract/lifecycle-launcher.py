#!/usr/bin/env python3
"""Finite registered single-case caller; invoked only by owned controls verifier."""
import os
from pathlib import Path
import signal
import sys
import threading
import time
import check

plan, expected, case = Path(sys.argv[1]),sys.argv[2],sys.argv[3]
os.environ['XEMELE_CONTROL_CALLER'] = str(os.getpid())
parent = os.getppid(); done = threading.Event()
def watch_parent():
    while not done.wait(.05):
        if os.getppid() != parent:
            os.kill(os.getpid(),signal.SIGTERM); return
threading.Thread(target=watch_parent,daemon=True).start()
try:
    record = check.run_owned_guard(plan,expected,control=case)
    sys.exit(0 if check.supervision.workload_succeeded(record) else 1)
finally:
    done.set()
    # The verifier must reap this registered caller before proving whole-family
    # absence. Raw case inputs remain active only until that verifier consumes
    # readiness/outcome/identities and calls terminal.finish; this caller cannot
    # assert its own group absent while still alive. Interruption follows the
    # same outer terminal boundary, including a SIGKILLed caller with no finally.
    if os.getppid()==parent:
        check.durable(plan.parent/'active-verifier-handoff.json',
            {'receiptRole':'active-control-awaiting-verifier','parentPid':parent,
             'planSha256':expected,'terminalOwner':'controls.py real_case finally'})
