#!/usr/bin/env python3
"""Finite SOURCE-prepared independent lifecycle verifier. All actual controls UNRUN."""
import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import threading
import time
from unittest.mock import patch
import uuid
import check as c


def independent_absence(registries, deadline):
    groups = set()
    for registry in registries:
        for file in registry.rglob('group-*.json'):
            identity = json.loads(file.read_text())
            assert identity['pid'] == identity['pgid'] > 1
            groups.add(identity['pgid'])
    live = c.nested.group_members_snapshot(groups,min(2,deadline-time.monotonic()))
    c.checkpoint(deadline)
    assert not live, 'independent registered-group inventory found live children'
    return groups


def rejects(action, message=None):
    try: action()
    except (AssertionError,TimeoutError,RuntimeError,FileNotFoundError) as error:
        assert message is None or message in str(error), 'wrong rejection reason: '+str(error)
        return True
    raise AssertionError('required rejecting control was accepted')


def verify_real_case(case, folder, base, deadline, environment):
    folder.mkdir()
    # Only harness inputs are executed in these Python controls. SDK compilation is gated later.
    inputs = {name:identity for name,identity in base['inputs'].items() if str(c.HERE) in name or str(c.FIXTURE) in name}
    plan = folder/'plan.json'
    c.durable(plan,{'sourceSha':base['sourceSha'],'inputs':inputs,'controlCase':case,'commands':c.control_commands(case,folder)})
    expected = c.bindings.digest(plan)
    errors=[]; killer=None
    if case == 'parent-death':
        def kill_after_ACK():
            try:
                ready=folder/'descendant-ready.json'; ready_end=min(deadline,time.monotonic()+5)
                while not ready.exists():
                    c.checkpoint(ready_end); time.sleep(.01)
                identity=json.loads(ready.read_text())
                entries=[(file,json.loads(file.read_text())) for file in folder.parent.rglob('group-*.json')]
                assert any(entry['pid']==identity['caller'] for file,entry in entries), 'caller not registered'
                matching=[(file,entry) for file,entry in entries if entry['pgid']==identity['pgid']]
                assert matching, 'descendant not registered'
                for file,entry in matching:
                    assert c.nested.complete_record(file.with_name('ack-'+entry['token']+'.json'),{'token':entry['token'],'pid':entry['pid']}), 'readiness preceded ACK'
                os.kill(identity['caller'],signal.SIGKILL)
            except BaseException as error: errors.append(repr(error))
        killer=threading.Thread(target=kill_after_ACK); killer.start()
    c.supervision.ARTIFACTS=folder
    try:
        record=c.nested.supervise_family_command(c.supervision,
            [sys.executable,str(c.HERE/'lifecycle-launcher.py'),str(plan),expected,case],
            888,environment,min(deadline-6,time.monotonic()+20),deadline)
    finally:
        if killer is not None: killer.join(timeout=min(1,max(.001,deadline-time.monotonic())))
    assert not errors and (killer is None or not killer.is_alive()), errors
    assert record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'] and record['nestedOwnership']['allOwnedGroupsAbsent']
    groups=independent_absence([folder],deadline)
    success=case in ('normal','descendant')
    if case=='parent-death':
        # The surviving outer owner may kill the guard before it publishes a receipt.
        # Its registered-group cleanup is authoritative; do not invent a guard outcome.
        assert len(list(folder.glob('launch-inputs-*.json')))==1, 'parent died after more than one command'
        guard_identity=c.bindings.digest(folder/'receipt.json') if (folder/'receipt.json').exists() else None
        if guard_identity: assert c.load_receipt(folder/'receipt.json',guard_identity)['state']=='failed'
    else:
        guard_identity=c.bindings.digest(folder/'receipt.json')
        value=c.load_receipt(folder/'receipt.json',guard_identity)
        assert value['state']==('passed' if success else 'failed'), 'wrong control outcome'
        assert len(value['commands'])==(2 if success else 1), 'wrong launch count'
    assert (folder/'next-command.txt').exists()==success, 'no-next invariant'
    assert record['exit']==(0 if success else (-signal.SIGKILL if case=='parent-death' else 1))
    if case in ('descendant','parent-death'):
        identity=json.loads((folder/'descendant-ready.json').read_text())
        assert identity['pgid'] in groups, 'registered descendant was not independently inventoried'
    return {'expectedOutcomeVerified':True,'groups':sorted(groups),'lifecycle':record,'receiptSha256':guard_identity}



def real_case(case,folder,base,deadline,environment):
    consumed={'state':'failed'}
    try:
        record=verify_real_case(case,folder,base,deadline,environment)
        consumed={'state':'passed','sourceSha':base['sourceSha'],'outcomes':{case:True},'commands':[record['lifecycle']]}
        return record
    except BaseException as error:
        consumed['reason']=str(error)
        raise
    finally:
        if folder.exists():
            c.terminal.finish(folder,'control-'+case,consumed,deadline)

def cleanup_fault(case, folder, deadline, environment):
    folder.mkdir(); registry=folder/'owned'; registry.mkdir()
    env=environment.copy()
    env[c.nested.REGISTRIES]=json.dumps([str(p) for p in c.nested.registries(env)+[registry]])
    child=None
    try:
        with c.nested.track_launches(c.supervision,env,time.monotonic()+2):
            child=c.supervision.subprocess.Popen([sys.executable,'-c','import time;time.sleep(30)'],
                env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
        assert child.poll() is None, 'control needs live child'
        if case=='inventory-failure':
            with patch.object(c.nested,'group_members_snapshot',side_effect=OSError('injected ps failure')):
                result=c.nested.drain(c.supervision,registry,time.monotonic()+3)
        else:
            # Known groups must receive SIGKILL even when no scan budget remains.
            result=c.nested.drain(c.supervision,registry,time.monotonic()-1)
        assert not result['allOwnedGroupsAbsent'] and 'cleanupError' in result
        child.wait(timeout=min(1,deadline-time.monotonic()))
        assert child.returncode is not None
        groups=independent_absence([registry],deadline)
        return {'expectedOutcomeVerified':True,'groups':sorted(groups),'failure':result}
    finally:
        if child is not None:
            c.supervision.kill_group(child.pid,signal.SIGKILL)
            child.wait(timeout=max(.001,min(1,deadline-time.monotonic())))


def invariant_case(case, folder, base, expected_plan_sha, deadline, environment):
    folder.mkdir()
    if case in ('compile-before-controls-gate','compile-caller-recovery'):
        return compile_ownership_case(case,folder,base,deadline,environment)
    if case.startswith('terminal-'):
        return terminal_case(case,folder,deadline,environment)
    if case in ('inventory-failure','expired-cleanup'):
        # Called with an already unique folder by main, use separate fault subfolder.
        return cleanup_fault(case,folder/'fault',deadline,environment)
    if case=='delayed-prelaunch':
        registry=folder/'owned'; registry.mkdir()
        env=environment.copy(); env[c.nested.REGISTRIES]=json.dumps([str(p) for p in c.nested.registries(env)+[registry]])
        old=c.nested.PRELAUNCH_HOOK
        try:
            c.nested.PRELAUNCH_HOOK=lambda:time.sleep(.15)
            with c.nested.track_launches(c.supervision,env,time.monotonic()+.1):
                rejects(lambda:c.supervision.subprocess.Popen([sys.executable,str(c.HERE/'lifecycle-hazard.py'),'next',str(folder)],env=env,start_new_session=True))
        finally: c.nested.PRELAUNCH_HOOK=old
        result=c.nested.drain(c.supervision,registry,time.monotonic()+3)
        assert result['allOwnedGroupsAbsent'] and not (folder/'next-command.txt').exists()
        independent_absence([registry],deadline)
    elif case=='delayed-absence':
        registry=folder/'owned'; registry.mkdir()
        def late_absence(groups,timeout): time.sleep(.15); return {}
        with patch.object(c.nested,'group_members_snapshot',side_effect=late_absence):
            result=c.nested.drain(c.supervision,registry,time.monotonic()+2.1)
        assert not result['allOwnedGroupsAbsent'] and 'cleanupError' in result
        independent_absence([registry],deadline)
    elif case=='delayed-publication':
        real=c.durable
        def delayed(path,value):
            real(path,value)
            if Path(path).name.endswith('-candidate.json'): time.sleep(.15)
        with patch.object(c,'durable',side_effect=delayed):
            rejects(lambda:c.publish(folder/'receipt.json',{'state':'passed'},time.monotonic()+.1))
        assert not (folder/'receipt.json').exists() and (folder/'receipt-quarantine/failure.json').exists()
    elif case=='changed-command':
        plan=folder.parent.parent/('changed-command-'+uuid.uuid4().hex+'.json'); value=dict(base); value['commands']=list(base['commands'])
        value['commands'][0]={'argv':[sys.executable,'-c','pass']}; c.durable(plan,value)
        try: rejects(lambda:c.verify_inputs(plan,c.bindings.digest(plan),deadline,None), 'changed required compile command')
        finally: plan.unlink()
    elif case in ('changed-project','changed-source','changed-binary'):
        file=folder/({'changed-project':'Candidate.csproj','changed-source':'Usage.cs.fixture','changed-binary':'dependency.dll'}[case])
        file.write_bytes(b'bound'); plan=folder/'plan.json'
        c.durable(plan,{'inputs':{str(file):c.bindings.digest(file)}}); expected=c.bindings.digest(plan)
        file.write_bytes(b'changed'); rejects(lambda:c.bindings.verify(plan,expected,deadline), 'bound source/project/config/binary changed')
    elif case=='missing-controls':
        rejects(lambda:c.require_controls(None,None,expected_plan_sha,base['sourceSha']), 'successful matching controls receipt required')
    elif case in ('mismatched-controls','wrong-control-outcome'):
        value={'state':'passed','receiptRole':'active-controls-gate','pendingPhase':'compile','stage':str(folder.parent.parent),'planSha256':expected_plan_sha,'sourceSha':base['sourceSha'],
            'cases':list(c.ALL_CASES),'outcomes':dict.fromkeys(c.ALL_CASES,True),'independentAbsence':True,'noNextAfterFailure':True}
        if case=='mismatched-controls': value['planSha256']='0'*64
        else: value['outcomes']['normal']=False
        c.publish(folder/'receipt.json',value,deadline)
        rejects(lambda:c.require_controls(folder/'receipt.json',c.bindings.digest(folder/'receipt.json'),expected_plan_sha,base['sourceSha']), 'controls receipt mismatch' if case=='mismatched-controls' else 'missing/wrong control outcomes')
    else: raise AssertionError('unknown invariant control')
    return {'expectedOutcomeVerified':True}



def terminal_case(case,folder,deadline,environment):
    stage=folder/'attempt'; stage.mkdir()
    canonical=folder/'canonical.fixture'; canonical.write_bytes(b'preserve canonical input')
    cache=folder/'cache-sentinel'; cache.write_bytes(b'preserve cache')
    project=stage/'Candidate.csproj'; project.write_bytes(b'owned staged project')
    plan=stage/'plan.json'; c.durable(plan,{'inputs':{str(project):c.bindings.digest(project),str(canonical):c.bindings.digest(canonical)}})
    expected=c.bindings.digest(plan); captured={}
    def action():
        c.bindings.verify(plan,expected,deadline)
        payload="from pathlib import Path; p=Path("+repr(str(stage))+" ); (p/'bin').mkdir(); (p/'obj').mkdir(); (p/'bin/fake.dll').write_bytes(b'control output'); (p/'obj/scratch').write_text('scratch'); (p/'candidate.actual.txt').write_text('control projection'); (p/'drop-member.txt').write_text('control negative')"
        code=0 if case in ('terminal-successful-compile','terminal-interrupted','terminal-unexpected-exit','terminal-publication-term') else 7
        actual_code=9 if case=='terminal-unexpected-exit' else code
        payload+='; raise SystemExit('+str(actual_code)+')'
        c.supervision.ARTIFACTS=stage
        record=c.nested.supervise_family_command(c.supervision,[sys.executable,'-c',payload],701,environment,min(time.monotonic()+2,deadline-4),deadline)
        assert record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'] and record['nestedOwnership']['allOwnedGroupsAbsent']
        c.bindings.verify(plan,expected,deadline)
        outputs=c.output_identities(stage)
        assert stage.exists() and (stage/'bin/fake.dll').exists() and outputs
        captured.update(state='passed' if record['exit']==0 and record['reason']=='completed' else 'failed',inputs=json.loads(plan.read_text())['inputs'],outputs=outputs,commands=[record])
        assert record['exit']==code, 'unexpected retention child exit'
        assert record['reason']==('completed' if code==0 else 'failed'), 'unexpected retention completion reason'
        if code: raise RuntimeError('finite seam failed phase')
        return captured
    if case=='terminal-interrupted':
        # Real absent family plus a one-shot interruption during physical recursive deletion.
        remove=c.terminal.remove_tree; calls=[0]
        def interrupt_once(path,end):
            calls[0]+=1
            if calls[0]==1: raise KeyboardInterrupt('finite cleanup interruption seam')
            return remove(path,end)
        with patch.object(c.terminal,'remove_tree',side_effect=interrupt_once):
            try: c.terminal.attempt(stage,case,action,deadline,lambda:captured)
            except KeyboardInterrupt as error: assert 'terminal cleanup completed' in str(error)
            else: raise AssertionError('cleanup interruption lost')
        assert calls[0]>=2 and captured['state']=='failed'
    elif case=='terminal-publication-term':
        real=c.terminal.nested.durable; sent=[False]
        def interrupt_final(path,value):
            real(path,value)
            if value.get('terminalCleanup')=='passed' and not sent[0]:
                sent[0]=True; os.kill(os.getpid(),signal.SIGTERM)
        with patch.object(c.terminal.nested,'durable',side_effect=interrupt_final):
            try: c.terminal.attempt(stage,case,action,deadline,lambda:captured)
            except KeyboardInterrupt as error: assert 'terminal cleanup completed' in str(error)
            else: raise AssertionError('final publication TERM lost')
        summaries=[json.loads(p.read_text()) for p in (c.OUT/'summaries').glob('*.json')]
        retained=[value for value in summaries if value.get('scratchRoot')==str(stage.resolve())]
        assert sent[0] and retained and all(value['consumed']['state']=='failed' for value in retained), 'retained interrupted phase success'
    elif case=='terminal-unexpected-exit':
        rejects(lambda:c.terminal.attempt(stage,case,action,deadline,lambda:captured),'unexpected retention child exit')
        assert captured['commands'][0]['exit']==9
    elif case=='terminal-successful-compile':
        assert c.terminal.attempt(stage,case,action,deadline,lambda:captured)['state']=='passed'
    else:
        rejects(lambda:c.terminal.attempt(stage,case,action,deadline,lambda:captured),'finite seam failed phase')
        assert captured['commands'][0]['exit']==7 and captured['commands'][0]['reason']=='failed' and captured['commands'][0]['logClosed']
    assert not stage.exists(), 'terminal phase retained generated output archive'
    assert canonical.read_bytes()==b'preserve canonical input' and cache.read_bytes()==b'preserve cache'
    # Deleted files must not be reused or presented as fresh matching controls.
    rejects(lambda:c.bindings.verify(plan,expected,deadline))
    return {'expectedOutcomeVerified':True,'scratchDeleted':True,'canonicalAndCachePreserved':True}


def claim_controls(stage,expected):
    c.durable(stage/'controls-started.json',{'planSha256':expected})


def compile_ownership_case(case,folder,base,deadline,environment):
    stage=folder/'attempt'; stage.mkdir()
    project=stage/'Candidate.csproj'; project.write_text('owned project sentinel')
    plan=stage/'plan.json'
    # Synthetic gate/recipe is for Python ownership controls only; never used as
    # compilation evidence or fed to any .NET command.
    c.durable(plan,{'sourceSha':base['sourceSha'],'dotnet':sys.executable,
        'inputs':{str(project):c.bindings.digest(project)},'commands':c.compile_commands(sys.executable,stage)})
    expected=c.bindings.digest(plan)
    if case=='compile-before-controls-gate':
        claim_controls(stage,expected)  # Actual controls claim, paused before its first child.
        before=c.bindings.digest(project)
        rejects(lambda:c.compile_attempt(plan,expected,None,None,lambda:(_ for _ in ()).throw(AssertionError('premature child'))),
                'successful matching controls receipt required')
        assert stage.exists() and not (stage/'compile-started.json').exists() and not (stage/'compile-owned').exists()
        assert c.bindings.digest(project)==before and (stage/'controls-started.json').exists()
        c.supervision.ARTIFACTS=stage
        record=c.nested.supervise_family_command(c.supervision,
            [sys.executable,'-c',"from pathlib import Path;Path("+repr(str(stage/'continued'))+").write_text('controls continued')"],
            703,environment,min(deadline-4,time.monotonic()+2),deadline)
        assert record['exit']==0 and record['reason']=='completed' and record['logClosed'] and record['nestedOwnership']['allOwnedGroupsAbsent']
        assert (stage/'continued').exists() and c.bindings.digest(project)==before
        c.terminal.finish(stage,case,{'state':'passed','commands':[record]},deadline)
    else:
        gate=stage/'controls-receipt.json'
        c.publish(gate,{'state':'passed','receiptRole':'active-controls-gate','pendingPhase':'compile','stage':str(stage),
            'sourceSha':base['sourceSha'],'planSha256':expected,'cases':list(c.ALL_CASES),'outcomes':dict.fromkeys(c.ALL_CASES,True),
            'independentAbsence':True,'noNextAfterFailure':True,'scope':'synthetic ownership seam only'},deadline)
        gate_sha=c.bindings.digest(gate); ready=folder/'registered-ready.json'; errors=[]
        def kill_owner():
            try:
                end=min(deadline,time.monotonic()+5)
                while not ready.exists(): c.checkpoint(end); time.sleep(.01)
                value=json.loads(ready.read_text()); claim=json.loads((stage/'compile-started.json').read_text())
                assert claim['ownerPid']==value['ownerPid'] and c.bindings.digest(stage/'compile-started.json')==value['claimSha']
                registrations=[(p,json.loads(p.read_text())) for p in folder.rglob('group-*.json')]
                assert any(v['pid']==claim['ownerPid'] for p,v in registrations), 'compile controller not registered'
                matches=[(p,v) for p,v in registrations if v['pgid']==value['childGroup']]
                assert matches
                for path,entry in matches:
                    assert c.nested.complete_record(path.with_name('ack-'+entry['token']+'.json'),{'token':entry['token'],'pid':entry['pid']})
                os.kill(claim['ownerPid'],signal.SIGKILL)
            except BaseException as error: errors.append(repr(error))
        killer=threading.Thread(target=kill_owner); killer.start()
        c.supervision.ARTIFACTS=folder
        try:
            record=c.nested.supervise_family_command(c.supervision,
                [sys.executable,str(c.HERE/'retention-caller.py'),str(plan),expected,str(gate),gate_sha,str(ready)],
                704,environment,min(deadline-6,time.monotonic()+8),deadline)
        finally: killer.join(timeout=1)
        assert not killer.is_alive() and not errors, errors
        assert record['exit']==-signal.SIGKILL and record['directChildReaped'] and record['logClosed'] and record['nestedOwnership']['allOwnedGroupsAbsent']
        assert stage.exists() and (stage/'bin/sentinel').exists(), 'killed caller unexpectedly ran terminal finally'
        claim_sha=json.loads(ready.read_text())['claimSha']
        c.recover_compile(stage,expected,claim_sha,deadline)
        assert not stage.exists(), 'matched surviving recovery retained compile scratch'
    return {'expectedOutcomeVerified':True,'stagePreservedUntilGate':case=='compile-before-controls-gate','scratchDeleted':True}

def verify_campaign(plan_path,expected_plan_sha):
    a=argparse.Namespace(plan=plan_path,expected_plan_sha=expected_plan_sha)
    # CLI validation/phase ownership is outside this active campaign boundary.
    start=time.monotonic(); work_end=start+100; hard_end=start+115
    def stop_hard(sig,frame): raise TimeoutError('controls absolute hard deadline')
    signal.signal(signal.SIGALRM,stop_hard); signal.setitimer(signal.ITIMER_REAL,115)
    base=c.bindings.verify(a.plan,a.expected_plan_sha,work_end-10)
    assert base['commands']==c.compile_commands(base['dotnet'],a.plan.parent)
    folder=a.plan.parent/('controls-'+uuid.uuid4().hex); folder.mkdir()
    registry=folder/'controller-owned'; registry.mkdir()
    environment=os.environ.copy(); environment[c.nested.REGISTRIES]=json.dumps([str(p) for p in c.nested.registries(environment)+[registry]])
    result={'state':'failed','sourceSha':base['sourceSha'],'planSha256':a.expected_plan_sha,
            'cases':list(c.ALL_CASES),'outcomes':{},'records':{},'independentAbsence':False,'noNextAfterFailure':False}
    try:
        with c.nested.interruptions():
            for case in c.ALL_CASES:
                c.checkpoint(work_end-10)
                if case in c.CASES: record=real_case(case,folder/case,base,work_end-10,environment)
                else: record=invariant_case(case,folder/case,base,a.expected_plan_sha,work_end-10,environment)
                result['records'][case]=record; result['outcomes'][case]=record['expectedOutcomeVerified']
                c.durable(folder/(case+'-verified.json'),record)
            c.bindings.verify(a.plan,a.expected_plan_sha,work_end-10)
            result['state']='passed'; result['noNextAfterFailure']=True
    except BaseException as error:
        result['reason']=type(error).__name__+': '+str(error)
    finally:
        cleanup=c.nested.drain(c.supervision,registry,work_end)
        result['cleanup']=cleanup
        result['independentAbsence']=cleanup['allOwnedGroupsAbsent']
        if result['independentAbsence']: independent_absence([folder],work_end)
        else: result['state']='failed'
        c.terminal.finish(folder,'controls-scratch',result,work_end)
        signal.setitimer(signal.ITIMER_REAL,0)
    if result['state']=='passed':
        # Gate is concise and ACTIVE only while its staged compile inputs remain.
        gate={key:result[key] for key in ('state','sourceSha','planSha256','cases','outcomes','independentAbsence','noNextAfterFailure')}
        gate.update(receiptRole='active-controls-gate',stage=str(a.plan.parent),pendingPhase='compile',
                    consumedDetailsSha256=c.terminal.identity(result))
        c.publish(a.plan.parent/'controls-receipt.json',gate,work_end)
    return result


def main():
    p=argparse.ArgumentParser(); p.add_argument('--plan',type=Path,required=True); p.add_argument('--expected-plan-sha',required=True)
    a=p.parse_args(); stage=c.terminal.owned(a.plan.parent)
    claim_controls(stage,a.expected_plan_sha)
    result={'state':'failed','planSha256':a.expected_plan_sha}
    terminal_hard_end=time.monotonic()+115
    try:
        with c.nested.interruptions():
            result=verify_campaign(a.plan,a.expected_plan_sha)
            if result['state']!='passed': raise RuntimeError('controls campaign failed')
    except BaseException as error:
        result['state']='failed'; result['reason']=str(error)
        c.terminal.finish(stage,'controls-failed',result,min(terminal_hard_end,time.monotonic()+12))
        raise
    receipt=stage/'controls-receipt.json'
    print(json.dumps({'receipt':str(receipt),'sha256':c.bindings.digest(receipt),'state':'passed','receiptRole':'active-controls-gate','pendingPhase':'compile'}))
    return 0

if __name__=='__main__': sys.exit(main())
