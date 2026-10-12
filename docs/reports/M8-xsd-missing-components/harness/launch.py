"""Coordinator-only finite root. Execution remains HOLD until exact SOURCE PASS."""
import sys
sys.dont_write_bytecode=True
import json
import os
import time
import uuid
from pathlib import Path
import bindings as b
from birth import identity
from family import Runner,nested,publish_finite
import terminal

COORDINATOR='01a12c6c-b333-71d3-877f-0d186618e21d'


def main():
    started=time.monotonic()
    assert len(sys.argv)>=4 and sys.argv[1]=='--released','--released reviewSHA controls|check|prepare|measure|discard [receipt SHA]'
    review_sha,action=sys.argv[2:4]
    assert action in ['controls','check','prepare','measure','discard']
    assert os.environ['XEMELE_MISSING_COORDINATOR']==COORDINATOR,'Only named coordinator launches roots'
    assert not nested.registries(os.environ),'Root cannot be hidden inside another family'
    source=b.review(review_sha,action)
    assert len(sys.argv)==(8 if action=='prepare' else 6 if action in ['measure','discard'] else 4)
    seconds={'controls':120,'check':600,'prepare':600,'measure':120,'discard':40}[action]
    reserve=60 if action in ['check','prepare'] else 30
    phase_started=started
    if action=='prepare':
        checks=b.verify_release(sys.argv[6],sys.argv[7],'check',review_sha)
        family=json.loads(Path(checks['familyReceipt']).read_text())
        phase_started=family['startedMonotonic']
        assert time.monotonic()<family['workDeadline'],'Combined600-second correctness/preparation work deadline expired'
        os.environ['XEMELE_MISSING_HARD_DEADLINE']=str(family['hardDeadline'])
        os.environ['XEMELE_MISSING_WORK_DEADLINE']=str(family['workDeadline'])
    directory=b.OUT/('root-'+action+'-'+uuid.uuid4().hex)
    env=b.environment(source['runtimeSourceCommit'])
    env.update(XEMELE_MISSING_REVIEW_SHA256=review_sha,XEMELE_MISSING_RUN_DIRECTORY=str(directory),XEMELE_MISSING_ACTION=action)
    runner=Runner(directory,seconds,env,require_ancestor=False,counter_launcher=action=='measure',started=phase_started,cleanup_reserve=reserve)
    owner=dict(controllerBirth=identity(os.getpid()),coordinator=COORDINATOR,coordinatorNonce=uuid.uuid4().hex,
               sourceSha256=b.digest(b.SOURCE),reviewSha256=review_sha,action=action,directory=str(directory),hardDeadline=runner.hard,
               registry=str(runner.registry),argv=sys.argv,cwd=str(b.ROOT),environmentSha256=b.identity_sha(runner.environment))
    nested.durable(directory/'owner.json',owner)
    print('RECOVERY IDENTITY',directory/'owner.json',b.digest(directory/'owner.json'),owner['coordinatorNonce'],flush=True)
    script='controls.py' if action=='controls' else action+'.py'
    argv=[sys.executable,str(b.HERE/script),'--root',*sys.argv[4:]]
    prepared=None;error=None;detail=None;payload=None
    try:
        with runner:
            if action in ['measure','discard']:prepared,_=b.verify_prepared(sys.argv[4],sys.argv[5],review_sha)
            runner.run(argv,seconds-2)
            b.review(review_sha)
        assert runner.receipt['completed']
        if action=='controls':payload=directory/'positive-controls.json'
        elif action=='check':payload=directory/'checks/checks-summary.json'
        elif action=='prepare':payload=directory/'preparation/prepared.json'
        elif action=='measure':
            assert prepared is not None
            raw=Path(prepared['measurementDirectory'])/'allocations.json'
            summary=b.allocation_summary(raw,prepared);summary['controller']=str(directory)
            payload=directory/'allocation-summary.json';publish_finite(payload,summary,runner.hard)
        else:
            payload=directory/'discard-summary.json'
            publish_finite(payload,dict(completed=True,controller=str(directory),preparationSha256=sys.argv[5],state='discarded; never reusable'),runner.hard)
        data=json.loads(payload.read_text());assert data['completed'] and data['controller']==str(directory)
    except BaseException as caught:error=type(caught).__name__;detail=str(caught)[:768]
    finally:
        if not runner.closed:runner.close(error or 'IncompleteController')
        # The exclusive consumed marker, bound to this controller, is required to
        # delete another preparation's retained outputs after a failed claim.
        if prepared is not None:
            marker=Path(sys.argv[4]).parent/'consumed.json'
            if not marker.is_file() or json.loads(marker.read_text())['controller']!=str(directory):prepared=None
        try:terminal.cleanup(runner,action,prepared,preserve=error is None,error=error,detail=detail)
        except BaseException as caught:
            nested.durable(directory/'terminal-outcome.json',dict(completed=False,error=type(caught).__name__,detail=str(caught)[:768]))
            return 1
    if error is not None:
        if action=='prepare':terminal.abort_preparation(runner,error,detail)
        nested.durable(directory/'terminal-outcome.json',dict(completed=False,error=error,detail=detail));return 1
    try:
        terminal.publish_controller(runner,action,{b.key(payload):b.digest(payload)},review_sha)
    except BaseException as caught:
        if action=='prepare' and not (directory/'preparation-abort.json').exists():terminal.abort_preparation(runner,type(caught).__name__)
        nested.durable(directory/'terminal-outcome.json',dict(completed=False,error=type(caught).__name__,detail=str(caught)[:768]));return 1
    print('RELEASE',payload,b.digest(payload),flush=True)
    return 0


if __name__=='__main__':
    with nested.interruptions():raise SystemExit(main())
