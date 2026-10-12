"""Root ancestor controller; SOURCE ONLY until coordinator review/release.
Terminal cleanup follows verified whole-family absence on every normal unwind.
SIGKILL of this outermost root requires coordinator recovery/drain before cleanup.
"""
import sys
sys.dont_write_bytecode = True
import json
import os
import time
import uuid
from pathlib import Path
import bindings as b
from family import Runner, nested, publish_finite


def main():
    started=time.monotonic()
    assert len(sys.argv)>=4 and sys.argv[1]=='--released','SOURCE-only: --released reviewSHA256 controls|check|prepare|measure|discard'
    reviewed=sys.argv[2];action=sys.argv[3]
    b.review(reviewed)
    assert action in ['check','prepare','controls','measure','discard']
    prepared=None
    if action in ['measure','discard']:
        assert len(sys.argv)==6,'Root supplies reviewed preparation path and SHA256'
        argv=[sys.executable,str(b.HERE/('measure.py' if action=='measure' else 'discard.py')),'--root',sys.argv[4],sys.argv[5]]
        seconds=150 if action=='measure' else 40
    else:
        assert len(sys.argv)==(6 if action=='prepare' else 4)
        script,seconds={'check':('check.py',840),'prepare':('prepare.py',440),'controls':('lifecycle-check.py',100)}[action]
        argv=[sys.executable,str(b.HERE/script),'--released']+(sys.argv[4:] if action=='prepare' else [])
    directory=b.OUT/('root-'+action+'-'+uuid.uuid4().hex)
    env=os.environ.copy();env.update(XEMELE_IMPORT_REVIEW_SHA256=reviewed,XEMELE_IMPORT_RUN_DIRECTORY=str(directory))
    runner=Runner(directory,seconds,env,require_ancestor=False,counter_launcher=action=='measure',started=started)
    error=None;payload=None
    try:
        with runner:
            b.review(reviewed)
            if action in ['measure','discard']:prepared,_=b.verify_prepared(Path(sys.argv[4]),sys.argv[5],reviewed)
            runner.run(argv,seconds-2);b.review(reviewed)
        assert runner.receipt['completed'] and time.monotonic()<runner.hard-1
        if action=='controls':
            raw=directory/'positive-controls.json';data=json.loads(raw.read_text())
            assert data['completed'] and data['sourceSha256']==b.digest(b.SOURCE) and data['testsRun']==11 and not data['counterEnabled']
            receipt=directory/'family-cleanup.json'
            payload=directory/'positive-controls-release.json'
            publish_finite(payload,dict(completed=True,sourceSha256=b.digest(b.SOURCE),coordinatorReviewSha256=reviewed,
                semanticReceipt=str(raw),semanticSha256=b.digest(raw),familyReceipt=str(receipt),familySha256=b.digest(receipt),counterEnabled=False),runner.hard)
        elif action=='check':
            payload=directory/'checks/results/checks-completed.json'
            data=json.loads(payload.read_text())
            assert data['completed'] and data['harnessSourceSha256']==b.digest(b.SOURCE)
            b.verify_correctness_evidence(data['evidence'])
        elif action=='prepare':payload=directory/'preparation/prepared.json'
        elif action=='measure':
            assert b.digest(sys.argv[4])==sys.argv[5]
            b.verify_active_prepared(prepared)
            payload=Path(prepared['directory'])/'release.json'
            assert json.loads(payload.read_text())['completed']
            b.verify_control(Path(prepared['directory'])/'control.json',prepared['probePath'])
            summary=b.allocation_summary(Path(prepared['directory'])/'allocations.json',prepared)
            publish_finite(Path(prepared['directory'])/'allocation-summary.json',summary,runner.hard)
        else:payload=directory/'discard-summary.json'
    except BaseException as caught:
        error=type(caught).__name__
    finally:
        # A child may fail after claiming its prepared binaries. Only this
        # controller's exclusive claim authorizes deleting those active outputs.
        if action in ['measure','discard']:
            path=Path(sys.argv[4]).resolve()
            assert b.OUT.resolve() in path.parents and path.name=='prepared.json'
            marker=path.parent/'consumed.json'
            if marker.is_file() and json.loads(marker.read_text())['controller']==str(directory):
                assert prepared is not None,'No verified initial preparation snapshot'
            else:prepared=None
        try:
            b.terminal_cleanup(runner,action,prepared,preserve_preparation=error is None,outer_error=error)
        except BaseException as cleanup_error:
            if action=='prepare':b.abort_preparation(runner,type(cleanup_error).__name__)
            raise
    if error is not None:
        nested.durable(directory/'terminal-outcome.json',{'completed':False,'error':error,'counterLauncher':action=='measure'})
        return 1
    try:
        assert payload is not None
        if action=='discard':publish_finite(payload,{'completed':True,'preparationSha256':sys.argv[5],'state':'discarded; never reusable'},runner.hard)
        payloads={b.key(payload):b.digest(payload)}
        if action=='measure':
            summary=Path(prepared['directory'])/'allocation-summary.json';payloads[b.key(summary)]=b.digest(summary)
        b.publish_controller(runner,action,payloads,reviewed)
    except BaseException as publication_error:
        if action=='prepare' and not (directory/'preparation-abort.json').exists():
            b.abort_preparation(runner,type(publication_error).__name__)
        nested.durable(directory/'terminal-outcome.json',{'completed':False,'error':type(publication_error).__name__,'counterLauncher':action=='measure'})
        return 1
    return 0


if __name__=='__main__':
    with nested.interruptions():raise SystemExit(main())
