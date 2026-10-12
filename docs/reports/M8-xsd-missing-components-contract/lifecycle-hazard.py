#!/usr/bin/env python3
"""Prepared supervisor control child only; UNRUN, never a runtime fixture."""
import json
import os
from pathlib import Path
import signal
import sys
import time

case, output = sys.argv[1], Path(sys.argv[2])
if case == 'next':
    output.write_text('next-command-launched\n')
    sys.exit(0)
if case == 'invariant':
    sys.exit(3)
if case == 'descendant':
    pid = os.fork()
    if pid == 0:
        os.setsid()  # escape original process group; retain unique family environment
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        time.sleep(30)
        os._exit(0)
    output.write_text(json.dumps({'escapedDescendant': pid}) + '\n')
    sys.exit(0)
if case in ('timeout', 'parent-death'):
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
    output.write_text(json.dumps({'pid': os.getpid(), 'case': case}) + '\n')
    time.sleep(30)
if case == 'late':
    time.sleep(2.1)  # control command deadline is 2s; never earn success
sys.exit(0)
