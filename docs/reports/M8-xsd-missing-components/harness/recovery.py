"""Exact coordinator/owner-SHA/nonce recovery after SIGKILL; never phase success."""
import sys
sys.dont_write_bytecode=True
import json
import os
import time
from pathlib import Path
from types import SimpleNamespace
import bindings as b
from birth import identity
from family import nested,supervision,publish_finite
import terminal

COORDINATOR='01a12c6c-b333-71d3-877f-0d186618e21d'


def recover(owner_path,owner_sha,nonce,review_sha,maximum_seconds=40):
    started=time.monotonic();hard=min(started+maximum_seconds,float(os.environ.get("XEMELE_MISSING_HARD_DEADLINE","inf")))
    assert os.environ['XEMELE_MISSING_COORDINATOR']==COORDINATOR
    b.recovery_review(review_sha)
    owner_path=b.owned(owner_path);assert owner_path.name=='owner.json' and b.digest(owner_path)==owner_sha
    row=json.loads(owner_path.read_text());directory=b.owned(row['directory'])
    assert owner_path==directory/'owner.json' and row['coordinator']==COORDINATOR and row['coordinatorNonce']==nonce
    assert row['reviewSha256']==review_sha and row['sourceSha256']==b.digest(b.SOURCE)
    # The original controller must be absent/reaped; a reused/live PID is refused.
    assert identity(row['controllerBirth']['pid']) is None,'Controller still exists or PID reused; refuse recovery'
    assert not (directory/'controller-release.json').exists(),'Already released; no recovery reuse'
    assert not (directory/'recovery-intent.json').exists(),'Recovery is single-use'
    nested.durable(directory/'recovery-intent.json',dict(ownerSha256=owner_sha,nonce=nonce,recoveryBirth=identity(os.getpid()),maximumSeconds=maximum_seconds))
    supervision.ARTIFACTS=directory;supervision.ROOT=b.ROOT
    registry=b.owned(row['registry']);assert registry==directory/'root-registry'
    cleanup=nested.drain(supervision,registry,hard)
    assert cleanup['allOwnedGroupsAbsent'],'No deletion until whole registered family absent'
    sequence=[json.loads(p.read_text()) for p in sorted(directory.glob('lifecycle-*.json'))]
    births={json.loads(p.read_text())['pid']:json.loads(p.read_text())['birth'] for p in registry.glob('group-*.json')}
    for record in sequence:
        if record['pid'] is not None:
            assert record['pid'] in births and identity(record['pid']) is None
        record.update(directChildReaped=True,ownedGroupAbsent=True,logClosed=True,nestedOwnership={'allOwnedGroupsAbsent':True},
                      reason='RecoveredAfterSIGKILL; no original success credit')
    # A killed controller may never have saved its last direct-child receipt.
    # The ancestor registry still includes that child's birth identity; PID/group
    # disappearance proves exit/reap. Recovery has no successful sequence credit.
    fake=SimpleNamespace(directory=directory,closed=True,stopped=True,sequence=sequence,hard=hard,
                         receipt=dict(completed=False,ownership=cleanup))
    prepared=None
    if row['action'] in ['measure','discard']:
        path=b.owned(row['argv'][4]);expected=row['argv'][5]
        marker=path.parent/'consumed.json'
        if path.is_file() and marker.is_file():
            assert b.digest(path)==expected
            claim=json.loads(marker.read_text())
            if claim['controller']==str(directory) and claim['preparationSha256']==expected:prepared=json.loads(path.read_text())
    result=terminal.cleanup(fake,row['action'],prepared,error='SIGKILLRecovery',receipt='recovery-terminal.json')
    if row['action']=='prepare':
        (directory/'preparation/prepared.json').unlink(missing_ok=True)
    for p in directory.rglob('controller-release.json'):p.unlink(missing_ok=True)
    publish_finite(directory/'recovery-result.json',dict(completed=False,recovered=True,originalPhaseAccepted=False,
                   ownerSha256=owner_sha,coordinatorNonce=nonce,wholeFamilyAbsent=True,rawDeletion=True,
                   terminalReceipt=str(directory/'recovery-terminal.json'),terminalSha256=b.digest(directory/'recovery-terminal.json')),hard)
    return result


if __name__=='__main__':
    assert len(sys.argv)==6 and sys.argv[1]=='--released','--released reviewSHA owner.json ownerSHA nonce'
    with nested.interruptions():recover(sys.argv[3],sys.argv[4],sys.argv[5],sys.argv[2])
