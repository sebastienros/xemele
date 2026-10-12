"""Whole-family absence precedes bounded summaries and terminal raw deletion."""
import json
import re
import shutil
import time
from pathlib import Path
import bindings as b
from family import nested, publish_finite
from diagnostics import preserve_failure


# Exact receipt filenames/positions. Unknown JSON/binary/extensionless payloads
# never survive merely because their suffix resembles a receipt.
CONCISE={'owner.json','family-cleanup.json','positive-controls.json','checks-summary.json','prepared.json','consumed.json',
         'disabled-summary.json','required-commands.json','measurement-semantic.json','allocation-summary.json','discard-summary.json',
         'recovery-intent.json','recovery-result.json','terminal-cleanup.json','terminal-outcome.json','preparation-abort.json','recovery-terminal.json','failure-diagnostics.json','failure-diagnostics-abort.json','failure-diagnostics-fallback.json','failure-diagnostics-fallback-abort.json'}
REGISTRY=re.compile(r'(?:intent|group|ack|not-launched|registration-failure)-[0-9a-f]{32}\.json|closed\.json|cleanup\.json|cleanup-recovery-[0-9a-f]{32}\.json|deadline-failure\.json')
INDEXED=re.compile(r'(?:lifecycle|binary-inputs|command-inputs)-[0-9]{3}\.json')


def retained_receipt(relative):
    parts=relative.parts
    if parts and parts[0] in ['checks','preparation']:parts=parts[1:]
    if len(parts)==1:return parts[0] in CONCISE or INDEXED.fullmatch(parts[0]) is not None
    if len(parts)==2 and (parts[0]=='root-registry' or re.fullmatch(r'nested-[0-9]{3}',parts[0])):
        return REGISTRY.fullmatch(parts[1]) is not None
    return False


def verify_membership(row,extra=()):
    expected=dict(row['retainedIdentities'],**row['activeOutputIdentities'])
    expected.update(b.identities(extra))
    actual=b.directory_inputs([b.resolve(p) for p in row['roots']])
    assert all(not b.resolve(p).exists() for p in row['absentRoots']),'Previously absent owned output root appeared'
    assert actual==expected,'Unexpected survivor or altered retained receipt; release refused'
    return expected


def absence(runner):
    assert runner.closed and runner.receipt and runner.receipt['ownership']['allOwnedGroupsAbsent']
    assert all(row['directChildReaped'] and row['ownedGroupAbsent'] and row['logClosed']
               and row['nestedOwnership']['allOwnedGroupsAbsent'] for row in runner.sequence)


def cleanup(runner,action,prepared=None,preserve=False,error=None,detail=None,receipt='terminal-cleanup.json'):
    absence(runner)
    assert time.monotonic()<runner.hard-3, 'Terminal reserve exhausted'
    failure=None
    abort=receipt=='preparation-abort.json'
    try: failure=preserve_failure(runner,error,detail,abort=abort,extra_directories=[] if prepared is None else [Path(prepared['probePath']).parents[2],prepared['measurementDirectory']])
    except BaseException as caught:
        runner.stopped=True
        path=runner.directory/('failure-diagnostics-fallback-abort.json' if abort else 'failure-diagnostics-fallback.json')
        nested.durable(path,dict(outcomeCompleted=False,error=type(caught).__name__,counterEnabled=False))
        failure=dict(path=str(path),sha256=b.digest(path))
    roots=[runner.directory]
    if prepared is not None:
        roots += [Path(prepared['probePath']).parents[2],Path(prepared['measurementDirectory'])]
    kept=None
    if action=='prepare' and preserve and error is None and runner.receipt['completed']:
        kept=runner.directory/'preparation/active'
    remove=[]; directories=[]; retained={}; active_inputs={}
    if kept is not None:
        candidate=json.loads((runner.directory/'preparation/prepared.json').read_text())
        active_inputs=candidate['activeOutputIdentities']
        assert b.directory_inputs([kept])==active_inputs,'Active-prepared complete inventory changed'
    for root in roots:
        root=b.owned(root)
        if not root.exists():continue
        for path in root.rglob('*'):
            assert not path.is_symlink(),'Symlinked terminal tree refused'
            if not path.is_file():continue
            if kept is not None and path.is_relative_to(kept):continue
            if retained_receipt(path.relative_to(root)):
                retained[b.key(path)]=b.digest(path)
            else:remove.append(path)
    removed=b.identities(remove)
    for path in remove:
        if time.monotonic()>=runner.hard-2: raise TimeoutError('Raw deletion reserve exhausted; no success')
        path.unlink(missing_ok=True)
    for root in roots:
        root=b.owned(root)
        if not root.exists():continue
        for path in sorted((p for p in root.rglob('*') if p.is_dir()),key=lambda p:len(p.parts),reverse=True):
            if kept is not None and (path==kept or path.is_relative_to(kept)):continue
            if not any(path.iterdir()):path.rmdir();directories.append(path)
    expected=dict(retained,**active_inputs)
    assert b.directory_inputs([p for p in roots if p.exists()])==expected,'Unexpected survivor after terminal deletion'
    assert all(not b.resolve(name).exists() for name in removed)
    row=dict(completed=True,action=action,familyOutcome=runner.receipt['completed'],outerError=error,
             removedIdentities=removed,removedCount=len(removed),removedDirectories=[b.key(p) for p in sorted(set(directories))],
             activePreparedOutputs=kept is not None,activePreparedRoot=None if kept is None else b.key(kept),activeOutputIdentities=active_inputs,
             retainedIdentities=retained,roots=[b.key(p) for p in roots if p.exists()],absentRoots=[b.key(p) for p in roots if not p.exists()],failureDiagnostics=failure,
             limitation='Exact owned file deletion after registered whole-family absence; no raw byte archive')
    publish_finite(runner.directory/receipt,row,runner.hard)
    return row


def abort_preparation(runner,error,detail=None):
    # Failed/late final publication revokes all retained prepared outputs.
    for candidate in [runner.directory/'preparation/prepared.json',runner.directory/'controller-release.json']:
        candidate.unlink(missing_ok=True)
    row=cleanup(runner,'prepare',error=error,detail=detail,receipt='preparation-abort.json')
    assert not (runner.directory/'preparation/active').exists()
    return row


def publish_controller(runner,action,payloads,review_sha):
    absence(runner)
    assert runner.receipt['completed'] and not runner.stopped and payloads
    terminal=runner.directory/'terminal-cleanup.json';data=json.loads(terminal.read_text())
    assert data['completed'] and data['familyOutcome'] and data['outerError'] is None
    assert all(not b.resolve(name).exists() for name in data['removedIdentities'])
    verify_membership(data,[terminal])
    assert time.monotonic()<runner.hard-10,'Final publication must preserve rollback reserve'
    family=runner.directory/'family-cleanup.json'
    row=dict(completed=True,action=action,sourceSha256=b.digest(b.SOURCE),reviewSha256=review_sha,
             familyReceipt=str(family),familySha256=b.digest(family),terminalReceipt=str(terminal),terminalSha256=b.digest(terminal),
             payloads=payloads,counterLauncher=action=='measure')
    try:publish_finite(runner.directory/'controller-release.json',row,runner.hard-8)
    except BaseException as caught:
        if action=='prepare':abort_preparation(runner,type(caught).__name__)
        raise
    return row
