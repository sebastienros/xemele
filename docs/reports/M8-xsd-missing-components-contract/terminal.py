"""Owned terminal retention boundary. No phase or control runs on import.

Raw scratch is active input only. Confirm registered-family absence, consume
concise summaries, then delete that unique scratch root without following links.
Terminal summaries cannot act as a future verification/control gate.
"""
import hashlib
import json
import os
import signal
from pathlib import Path
import time
import uuid
import nested_ownership as nested

OUT = Path(__file__).resolve().parents[3] / 'artifacts/M8-xsd-missing-components-contract'


def checkpoint(deadline):
    if time.monotonic() >= deadline: raise TimeoutError('terminal cleanup deadline')


def owned(path):
    raw = Path(path).absolute()
    assert not raw.is_symlink(), 'scratch root cannot redirect deletion'
    path = raw.resolve()
    assert path != OUT and OUT.resolve() in path.resolve().parents, 'unique owned scratch root required'
    summary_root=(OUT/'summaries').resolve()
    assert summary_root!=path and summary_root not in path.parents, 'terminal summaries are preserved'
    assert not path.is_symlink(), 'scratch root cannot redirect deletion'
    return path


def identity(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()


def absence(path, deadline):
    path=owned(path); groups=set()
    for file in path.rglob('group-*.json'):
        checkpoint(deadline)
        value=json.loads(file.read_text())
        assert value['pid']==value['pgid']>1 and value['pgid']!=os.getpgrp(), 'invalid registered group'
        groups.add(value['pgid'])
    live=nested.group_members_snapshot(groups,min(2,deadline-time.monotonic()))
    checkpoint(deadline)
    assert not live, 'terminal deletion requires independently confirmed family absence'
    return {'confirmed':True,'groupCount':len(groups),'groupsSha256':identity(sorted(groups))}


def concise(value):
    # Consume identities/outcomes while files still exist; no raw log/projection contents.
    return {'state':value.get('state','failed'),'sourceSha':value.get('sourceSha'),
        'planSha256':value.get('planSha256'),'actualRuntime':value.get('actualRuntime'),
        'reason':str(value.get('reason',''))[:400],
        'inputIdentityCount':len(value.get('inputs',{})), 'inputIdentitiesSha256':identity(value.get('inputs',{})),
        'outputIdentityCount':len(value.get('outputs',{})), 'outputIdentitiesSha256':identity(value.get('outputs',{})),
        'projectionComparison':value.get('projectionComparison'),
        'projectionIdentities':{Path(p).name:h for p,h in value.get('outputs',{}).items() if p.endswith('.actual.txt')},
        'commandOutcomes':[{'exit':r.get('exit'),'expectedExit':r.get('expectedExit'),
            'reason':r.get('reason'),'directChildReaped':r.get('directChildReaped'),
            'ownedGroupAbsent':r.get('ownedGroupAbsent'),'logClosed':r.get('logClosed'),
            'logSha256':r.get('logSha256'),'launchInputsSha256':r.get('launchInputsSha256')}
            for r in value.get('commands',[])],
        'caseOutcomes':value.get('outcomes',{}),'consumedDetailsSha256':identity(value)}


def remove_tree(path, deadline):
    # Never traverse a symlink to a source, toolchain or cache outside owned scratch.
    checkpoint(deadline)
    for child in path.iterdir():
        checkpoint(deadline)
        if child.is_symlink() or not child.is_dir(): child.unlink()
        else: remove_tree(child,deadline)
    path.rmdir()
    checkpoint(deadline)


def finish(path,phase,consumed,deadline):
    path=owned(path); deferred=[]
    def defer(sig,frame): deferred.append(sig)
    handlers={sig:signal.signal(sig,defer) for sig in (signal.SIGTERM,signal.SIGINT)}
    try:
        return finish_absent_scratch(path,phase,consumed,deadline,deferred)
    finally:
        for sig,handler in handlers.items(): signal.signal(sig,handler)


def finish_absent_scratch(path,phase,consumed,deadline,deferred):
    summary_dir=OUT/'summaries'; summary_dir.mkdir(parents=True,exist_ok=True)
    stem=phase+'-'+uuid.uuid4().hex
    candidate=summary_dir/(stem+'-consumed.json'); final=summary_dir/(stem+'.json')
    value={'receiptRole':'terminal-summary','phase':phase,'verification':'consumed while active; not rechecked after deletion',
        'reusableControlsGate':False,'scratchRoot':str(path),'scratchDeleted':False,'consumed':concise(consumed)}
    # Publish consumed facts before deleting; this is never an active passing gate.
    nested.durable(candidate,value)
    try:
        value['familyAbsence']=absence(path,deadline)
        try: remove_tree(path,deadline)
        except KeyboardInterrupt:
            # One finite injected interruption; real TERM/INT are deferred. No new deadline.
            deferred.append('KeyboardInterrupt')
            if path.exists(): remove_tree(path,deadline)
        if deferred:
            consumed['state']='failed'; consumed['reason']='interruption during terminal cleanup'
            value['consumed']=concise(consumed); value['deferredInterruptions']=deferred
        value['scratchDeleted']=not path.exists()
        assert value['scratchDeleted'], 'scratch remained after terminal deletion'
        checkpoint(deadline)
    except BaseException as error:
        value['terminalCleanup']='failed'; value['reason']=type(error).__name__+': '+str(error)
        nested.durable(final,value)
        raise
    value['terminalCleanup']='passed'
    nested.durable(final,value)
    # TERM/INT can arrive DURING durable final publication, after the earlier
    # reconciliation. Withdraw every stored phase-success before propagating it.
    if deferred:
        consumed['state']='failed'; consumed['reason']='interruption during terminal cleanup/publication'
        value['consumed']=concise(consumed); value['deferredInterruptions']=list(deferred)
        final.unlink(); candidate.unlink(missing_ok=True)
        final=summary_dir/(stem+'-interrupted.json')
        nested.durable(final,value)
    if time.monotonic()>=deadline:
        failed=summary_dir/(stem+'-late.json')
        value['terminalCleanup']='failed'; value['reason']='late terminal summary publication'
        nested.durable(failed,value); final.unlink()
        raise TimeoutError(value['reason'])
    candidate.unlink(missing_ok=True)
    if deferred: raise KeyboardInterrupt('terminal cleanup completed after interruption')
    return final


def attempt(path,phase,action,deadline,capture=None):
    """Shared finite seam: real bound action in production, Python action in controls."""
    with nested.interruptions():
        return attempt_active(path,phase,action,deadline,capture)


def attempt_active(path,phase,action,deadline,capture):
    consumed={'state':'failed'}; failed=False
    try:
        consumed=action()
        return consumed
    except BaseException as error:
        failed=True
        consumed['reason']=type(error).__name__+': '+str(error)
        raise
    finally:
        if capture is not None:
            try: details=capture()
            except BaseException as error:
                details=None; consumed['state']='failed'; consumed['reason']='capture failed: '+str(error)
            if details:
                details=dict(details)
                if failed:
                    details['state']='failed'; details['reason']=consumed.get('reason',details.get('reason',''))
                consumed.clear(); consumed.update(details)
        finish(path,phase,consumed,deadline)
