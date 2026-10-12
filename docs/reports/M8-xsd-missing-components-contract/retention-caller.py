#!/usr/bin/env python3
"""Owned Python-only seam in the production compile ownership/terminal path."""
import os
from pathlib import Path
import sys
import time
import check as c

plan,expected,gate,gate_sha,ready=Path(sys.argv[1]),sys.argv[2],Path(sys.argv[3]),sys.argv[4],Path(sys.argv[5])
stage=plan.parent

def action():
    claim=c.bindings.digest(stage/'compile-started.json')
    # This payload execs only after registered all-ancestor ACK; no .NET command runs.
    payload=("import os,time,json,sys\nfrom pathlib import Path\n"+
        "sys.path.insert(0,"+repr(str(c.HERE))+")\n"+
        "p=Path("+repr(str(stage))+");(p/'bin').mkdir();(p/'obj').mkdir();(p/'bin/sentinel').write_text('owned output');(p/'obj/sentinel').write_text('owned scratch')\n"+
        "import nested_ownership as n\n"+
        "n.durable(Path("+repr(str(ready))+"),dict(ownerPid="+str(os.getpid())+",childPid=os.getpid(),childGroup=os.getpgrp(),claimSha="+repr(claim)+"))\n"+
        "time.sleep(30)\n")
    c.supervision.ARTIFACTS=stage
    record=c.nested.supervise_family_command(c.supervision,[sys.executable,'-c',payload],705,
        os.environ.copy(),time.monotonic()+10,time.monotonic()+14)
    raise AssertionError('caller kill control unexpectedly completed: '+str(record['exit']))

c.compile_attempt(plan,expected,gate,gate_sha,action)
