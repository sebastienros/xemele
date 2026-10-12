#!/usr/bin/env python3
"""Registered control payloads. No unregistered fork/setsid escape guarantee."""
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time
import supervision
import nested_ownership as nested

case, folder = sys.argv[1], Path(sys.argv[2])
if case == 'next':
    (folder/'next-command.txt').write_text('next\n'); sys.exit(0)
if case == 'invariant': sys.exit(3)
if case == 'late': time.sleep(2.1); sys.exit(0)
if case == 'payload':
    # Reached only AFTER group publication and complete ACK in every ancestor registry.
    nested.durable(folder/'descendant-ready.json', {'pid':os.getpid(),'pgid':os.getpgrp(),
        'caller':int(os.environ['XEMELE_CONTROL_CALLER'])})
    signal.signal(signal.SIGTERM,signal.SIG_IGN)
    time.sleep(30); sys.exit(0)
if case in ('descendant','parent-death'):
    # Explicitly registered new session. Its group is durable before exec/readiness.
    deadline = time.monotonic()+2
    with nested.track_launches(supervision,os.environ.copy(),deadline):
        child = supervision.subprocess.Popen([sys.executable,str(Path(__file__).resolve()),'payload',str(folder)],
            env=os.environ.copy(),stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
    while not (folder/'descendant-ready.json').exists():
        if time.monotonic() >= deadline: raise TimeoutError('registered descendant readiness')
        time.sleep(.01)
    if case == 'descendant': sys.exit(0)
if case in ('timeout','parent-death'):
    signal.signal(signal.SIGTERM,signal.SIG_IGN); time.sleep(30)
sys.exit(0)
