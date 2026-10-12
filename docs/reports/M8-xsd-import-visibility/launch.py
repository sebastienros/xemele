"""Root ancestor controller. SOURCE ONLY until coordinator reviews all harness files.
All controllers below run in distinct registered sessions; signal interruption
unwinds root family cleanup. Atomic intent/PID/PGID/ACK files allow an enclosing
controller to kill an intermediate controller and still drain its descendants.
"""
import sys
sys.dont_write_bytecode = True
import os
import time
import uuid
from pathlib import Path
import bindings as b
from family import Runner, nested


def main():
    started=time.monotonic()
    assert len(sys.argv) >= 4 and sys.argv[1] == '--released', 'SOURCE-only: --released reviewSHA256 check|prepare|controls|measure'
    reviewed = sys.argv[2]; action = sys.argv[3]
    b.review(reviewed)
    assert action in ['check', 'prepare', 'controls', 'measure']
    if action == 'measure':
        assert len(sys.argv) == 6, 'Root supplies reviewed preparation path and SHA256'
        argv = [sys.executable, str(b.HERE / 'measure.py'), '--root', sys.argv[4], sys.argv[5]]
        seconds = 150
    else:
        assert len(sys.argv) == (6 if action == 'prepare' else 4)
        script, seconds = {'check': ('check.py', 840), 'prepare': ('prepare.py', 440), 'controls': ('lifecycle-check.py', 100)}[action]
        argv = [sys.executable, str(b.HERE / script), '--released'] + (sys.argv[4:] if action == 'prepare' else [])
    directory = b.OUT / ('root-' + action + '-' + uuid.uuid4().hex)
    env = os.environ.copy()
    env.update(XEMELE_IMPORT_REVIEW_SHA256=reviewed, XEMELE_IMPORT_RUN_DIRECTORY=str(directory))
    with Runner(directory, seconds, env, require_ancestor=False, counter_launcher=action=='measure',started=started) as runner:
        b.review(reviewed)
        runner.run(argv, seconds - 2)
        b.review(reviewed)
    assert runner.receipt['completed']
    assert time.monotonic() < runner.hard-1
    if action == 'controls':
        raw=directory/'positive-controls.json'
        data=__import__('json').loads(raw.read_text())
        assert data['completed'] and data['sourceSha256']==b.digest(b.SOURCE) and data['testsRun']==8 and not data['counterEnabled']
        receipt=directory/'family-cleanup.json'
        nested.durable(directory/'positive-controls-release.json',dict(completed=True,sourceSha256=b.digest(b.SOURCE),
            coordinatorReviewSha256=reviewed,semanticReceipt=str(raw),semanticSha256=b.digest(raw),familyReceipt=str(receipt),familySha256=b.digest(receipt),counterEnabled=False))
        assert time.monotonic()<runner.hard,'No late positive-control release'
    print('ROOT FAMILY CLEAN', directory)


if __name__ == '__main__':
    with nested.interruptions(): main()
