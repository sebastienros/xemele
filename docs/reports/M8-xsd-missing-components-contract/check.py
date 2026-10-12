#!/usr/bin/env python3
"""SOURCE-only prepared finite offline contract runner. UNRUN until SHA release.

Guard process owns launches and cleanup even if the caller dies. No runtime
fixture execution, generic runner import, restore source fallback or server reuse.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
FIXTURE = ROOT / 'tests/Contracts/Fixtures/M8/MissingComponents'
TOKEN_KEY = 'XEMELE_MISSING_CONTRACT_FAMILY'
WORK_SECONDS = 180
CLEANUP_SECONDS = 12
HARD_SECONDS = 195
COMMAND_SECONDS = 45


def atomic(path, value):
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, indent=2) + '\n')
    temporary.replace(path)


def finite_output(argv, deadline):
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        raise TimeoutError('No inventory/receipt budget remains')
    return subprocess.check_output(argv, cwd=ROOT, text=True, timeout=min(2, remaining))


def inventory(token, known, groups, deadline):
    # Environment marker finds descendants that changed session/group or reparented.
    # Full host commands are never logged or persisted. Fail closed if unavailable.
    output = finite_output(['ps', 'eww', '-axo', 'pid,ppid,pgid,command'], deadline)
    rows = []
    for line in output.splitlines()[1:]:
        values = line.strip().split(None, 3)
        if len(values) == 4:
            pid, parent, group = map(int, values[:3])
            rows.append((pid, parent, group, f'{TOKEN_KEY}={token}' in values[3]))
    found = {pid for pid, parent, group, marked in rows if marked or group in groups or pid in known}
    while True:
        extra = {pid for pid, parent, group, marked in rows if parent in found}
        if extra <= found:
            break
        found |= extra
    # Never infer ownership from dotnet name or kill host-wide services.
    found.discard(os.getpid())
    return found


def kill_owned(pids, sig):
    for pid in pids:
        try:
            os.kill(pid, sig)
        except ProcessLookupError:
            pass


def drain(child, token, known, groups, deadline, record):
    clean = False
    # Inventory twice with absence; cleanup is included inside the work envelope.
    while time.monotonic() < deadline:
        active = inventory(token, known, groups, deadline)
        known |= active
        if not active:
            if clean:
                if child is not None:
                    child.wait(timeout=max(.001, min(.5, deadline - time.monotonic())))
                record.update(ownedFamilyAbsent=True, directChildReaped=child is None or child.returncode is not None)
                return
            clean = True
        else:
            clean = False
            kill_owned(active, signal.SIGKILL)
        if child is not None:
            child.poll()  # reap the direct child before checking its absence
        time.sleep(min(.05, max(0, deadline - time.monotonic())))
    raise TimeoutError('Owned family did not drain inside reserved cleanup budget')


def guard(plan_path, caller_pid):
    plan = json.loads(plan_path.read_text())
    started = time.monotonic()
    control = plan.get('lifecycleControl')
    if control not in (None, 'normal', 'descendant', 'timeout', 'late', 'invariant', 'parent-death'):
        raise ValueError('Unknown lifecycle control')
    work_seconds, cleanup_seconds, hard_seconds, command_seconds = (8, 3, 10, 2) if control else (WORK_SECONDS, CLEANUP_SECONDS, HARD_SECONDS, COMMAND_SECONDS)
    work_end = started + work_seconds
    launch_end = work_end - cleanup_seconds
    hard_end = started + hard_seconds
    token = uuid.uuid4().hex
    receipt = {'state': 'running', 'sourceSha': plan['sourceSha'], 'sources': plan['sources'],
               'sdk': 'UNRUN', 'runtime': 'UNRUN', 'guardPid': os.getpid(), 'familyToken': token, 'commands': [], 'workSeconds': work_seconds,
               'cleanupReservedInsideWork': cleanup_seconds, 'hardSeconds': hard_seconds,
               'lifecycleControl': control, 'planSha256': hashlib.sha256(plan_path.read_bytes()).hexdigest(),
               'fixtureMethodsExecuted': False, 'runtimeTestsExecuted': False}
    target = plan_path.parent / 'receipt.json'
    current = None
    current_log = None
    known, groups = set(), set()
    interrupted = False

    def stop(sig, frame):
        nonlocal interrupted
        interrupted = True
    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    # The absolute hard timer is a final fail-closed guard, not a new cleanup period.
    def hard_stop(sig, frame):
        raise TimeoutError('Absolute hard deadline')
    signal.signal(signal.SIGALRM, hard_stop)
    signal.setitimer(signal.ITIMER_REAL, hard_seconds)
    try:
        for index, command in enumerate(plan['commands']):
            if interrupted or os.getppid() != caller_pid:
                raise RuntimeError('Caller died or stop requested; no next launch')
            if time.monotonic() >= launch_end:
                raise TimeoutError('No launch budget; no next command')
            if inventory(token, known, groups, min(work_end, hard_end)):
                raise RuntimeError('Previous owned family still present')
            record = {'argv': command['argv'], 'expectedExit': command.get('expectedExit', 0),
                      'started': time.monotonic() - started, 'ownedFamilyAbsent': False}
            receipt['commands'].append(record)
            atomic(target, receipt)
            environment = dict(os.environ, **{TOKEN_KEY: token,
                'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1',
                'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER': '1', 'MSBUILDDISABLENODEREUSE': '1',
                'DOTNET_CLI_HOME': str(plan_path.parent / 'cli-home')})
            log_path = plan_path.parent / f'{index:02d}.log'
            command_end = min(launch_end, time.monotonic() + command_seconds)
            with log_path.open('xb') as log:
                current_log = log
                current = subprocess.Popen(command['argv'], cwd=ROOT, env=environment,
                    stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
                known.add(current.pid); groups.add(current.pid)
                record['pid'] = current.pid
                while current.poll() is None:
                    if interrupted or os.getppid() != caller_pid:
                        raise RuntimeError('Caller died or stop requested')
                    if time.monotonic() >= command_end:
                        raise TimeoutError('Command deadline; no next command')
                    known |= inventory(token, known, groups, min(command_end, work_end))
                    time.sleep(.05)
                record['exit'] = current.returncode
                # Completion observed after command/launch deadline cannot earn success.
                if time.monotonic() >= command_end:
                    raise TimeoutError('Late completion; no success')
                drain(current, token, known, groups, min(work_end, hard_end), record)
                current = None
                known.clear(); groups.clear()
            record['logClosed'] = log.closed
            record['logSha256'] = hashlib.sha256(log_path.read_bytes()).hexdigest()
            text = log_path.read_text(errors='replace')
            if record['exit'] != record['expectedExit'] or any(s not in text for s in command.get('contains', [])):
                raise RuntimeError('Command exit/output invariant failed; no next command')
            if index == 0:
                receipt['sdk'] = text.strip()
                if not control and text.strip() != '10.0.400':
                    raise RuntimeError('Pinned SDK mismatch')
            if 'RUNTIME ' in text:
                runtime = next(line.removeprefix('RUNTIME ') for line in text.splitlines() if line.startswith('RUNTIME '))
                if runtime != '10.0.11':
                    raise RuntimeError('Actual extractor runtime mismatch')
                receipt['runtime'] = runtime
            record['result'] = 'passed'
            atomic(target, receipt)
        # Immutable source and exact old/new projection postconditions share the work deadline.
        if time.monotonic() >= launch_end or interrupted or os.getppid() != caller_pid:
            raise TimeoutError('No publication budget or caller died')
        for relative, expected in plan['sources'].items():
            if hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() != expected:
                raise RuntimeError('Reviewed source changed during execution')
        def rows(path):
            return {row['id']: row for row in map(json.loads, path.read_text().splitlines()[1:])}
        if not control:
            if receipt['runtime'] != '10.0.11':
                raise RuntimeError('No actual extractor runtime receipt')
            old = rows(plan_path.parent / 'old.actual.txt')
            new = rows(plan_path.parent / 'candidate.actual.txt')
            delta = json.loads((HERE / 'baseline-delta.json').read_text())['changes']
            expected_old, expected_new = old.copy(), old.copy()
            for change in delta:
                if expected_old[change['old']['id']] != change['old']:
                    raise RuntimeError('Old compiled member differs from frozen source')
                del expected_new[change['old']['id']]
                expected_new[change['candidate']['id']] = change['candidate']
            if new != expected_new:
                raise RuntimeError('Candidate differs beyond three approved members')
            receipt['projection'] = 'exactly 3 changed members; public/interface rows unchanged'
        if time.monotonic() >= launch_end or interrupted or os.getppid() != caller_pid:
            raise RuntimeError('Caller/deadline changed before publication')
        receipt['state'] = 'passed'
    except BaseException as error:
        receipt['state'] = 'failed'
        receipt['reason'] = type(error).__name__ + ': ' + str(error)
    finally:
        try:
            cleanup = {}
            drain(current, token, known, groups, min(work_end, hard_end), cleanup)
            receipt['finalCleanup'] = cleanup
            if receipt['commands']:
                receipt['commands'][-1]['logClosed'] = current_log is None or current_log.closed
                if current is not None:
                    receipt['commands'][-1]['exit'] = current.returncode
        except BaseException as error:
            receipt['state'] = 'failed'; receipt['cleanupError'] = type(error).__name__
        if interrupted or os.getppid() != caller_pid:
            receipt['state'] = 'failed'
            receipt.setdefault('reason', 'Caller died or stop requested before final receipt')
        receipt['elapsedSeconds'] = time.monotonic() - started
        if time.monotonic() >= work_end:
            receipt['state'] = 'failed'; receipt['lateSuccessRejected'] = True
        atomic(target, receipt)
        signal.setitimer(signal.ITIMER_REAL, 0)
    return 0 if receipt['state'] == 'passed' else 1


def prepare(dotnet, sha):
    # This function runs only after explicit source review release.
    deadline = time.monotonic() + 4
    if finite_output(['git', 'rev-parse', 'HEAD'], deadline).strip() != sha:
        raise RuntimeError('Release SHA must be current HEAD')
    if finite_output(['git', 'status', '--porcelain'], deadline).strip():
        raise RuntimeError('Source release requires clean checkout')
    artifact = ROOT / 'artifacts/M8-xsd-missing-components-contract' / (sha + '-' + uuid.uuid4().hex)
    artifact.mkdir(parents=True)
    sources = {}
    def source(path):
        sources[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    for path in [*FIXTURE.iterdir(), *HERE.iterdir()]:
        if path.is_file(): source(path)
    def project(name, includes, references=(), roslyn=False):
        path = artifact / name / (name + '.csproj'); path.parent.mkdir()
        tree = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
        properties = ET.SubElement(tree, 'PropertyGroup')
        for key, value in {'OutputType':'Library', 'IsPackable':'false', 'EnableDefaultCompileItems':'false',
                           'RuntimeFrameworkVersion':'10.0.11'}.items():
            ET.SubElement(properties, key).text = value
        items = ET.SubElement(tree, 'ItemGroup')
        for include in includes: ET.SubElement(items, 'Compile', Include=str(include))
        for reference in references: ET.SubElement(items, 'ProjectReference', Include=str(reference))
        if roslyn:
            properties.find('OutputType').text = 'Exe'
            for library in ['Microsoft.CodeAnalysis', 'Microsoft.CodeAnalysis.CSharp']:
                ET.SubElement(items, 'Reference', Include=library, HintPath=f'$(MSBuildSDKsPath)/../Roslyn/bincore/{library}.dll')
        ET.ElementTree(tree).write(path, encoding='unicode')
        return path
    # Same integrated true XML-only prerequisite source family; staged outputs only.
    prerequisite_template = ROOT / 'tests/Contracts/Fixtures/M8/Prerequisites/Prerequisites.csproj'
    source(prerequisite_template); source(ROOT/'Directory.Build.props'); source(ROOT/'global.json')
    includes=[]
    for compile_item in ET.parse(prerequisite_template).findall('.//Compile'):
        pattern = str((prerequisite_template.parent / compile_item.attrib['Include']).resolve().relative_to(ROOT))
        for path in sorted(ROOT.glob(pattern)):
            includes.append(path); source(path)
    prereq = project('Prerequisites', includes)
    shared = [FIXTURE/name for name in ['Model.cs.fixture','XPathAccounting.cs.fixture','AccountingJoin.cs.fixture']]
    old = project('Old', shared+[FIXTURE/'Xsd.old.cs.fixture',FIXTURE/'OldUsage.cs.fixture'], [prereq])
    candidate = project('Candidate', shared+[FIXTURE/'Xsd.candidate.cs.fixture',FIXTURE/'Availability.cs.fixture',FIXTURE/'Usage.cs.fixture'], [prereq])
    reject = project('Reject', shared+[FIXTURE/'Xsd.candidate.cs.fixture',FIXTURE/'RejectOldUsage.cs.fixture'], [prereq])
    checker = project('ApiCheck', [HERE/'ApiCheck.cs.fixture'], roslyn=True)
    commands = [{'argv':[dotnet,'--version'],'contains':['10.0.400']},
                {'argv':[dotnet,'--list-runtimes'],'contains':['Microsoft.NETCore.App 10.0.11 ']}]
    common = ['--disable-build-servers','-p:UseSharedCompilation=false','-p:BuildInParallel=false','-nodeReuse:false','-maxcpucount:1','--verbosity','quiet']
    for p in [prereq, old, candidate, checker, reject]:
        commands.append({'argv':[dotnet,'restore',str(p),'--source',str(FIXTURE),'-p:NuGetAudit=false',*common]})
        commands.append({'argv':[dotnet,'build',str(p),'-c','Release','--no-restore',*common],
                         **({'expectedExit':1,'contains':['CS0266']} if p==reject else {})})
    inspect = [dotnet,'--fx-version','10.0.11',str(checker.parent/'bin/Release/net10.0/ApiCheck.dll')]
    olddll = str(old.parent/'bin/Release/net10.0/Old.dll'); newdll = str(candidate.parent/'bin/Release/net10.0/Candidate.dll')
    commands += [{'argv':[*inspect,'compare',olddll,str(FIXTURE/'old.golden.txt')]},
                 {'argv':[*inspect,'compare',newdll,str(FIXTURE/'candidate.golden.txt')]},
                 {'argv':[*inspect,'emit',olddll,str(artifact/'old.actual.txt')]},
                 {'argv':[*inspect,'emit',newdll,str(artifact/'candidate.actual.txt')]}]
    baseline=(FIXTURE/'candidate.golden.txt').read_text()
    for label, contents in [('drop-member', '\n'.join(baseline.splitlines()[:-1])+'\n'),
                             ('remove-nullable',baseline.replace('SchemaName? Xemele.Validation.XsdElementDeclaration.Type','SchemaName Xemele.Validation.XsdElementDeclaration.Type'))]:
        bad=artifact/(label+'.txt'); bad.write_text(contents)
        commands.append({'argv':[*inspect,'compare',newdll,str(bad)],'expectedExit':1,'contains':['API mismatch']})
    plan=artifact/'plan.json'; atomic(plan, {'sourceSha':sha,'sources':sources,'commands':commands})
    return plan



def prepare_controls(released_plan):
    base = json.loads(released_plan.read_text())
    hazard = HERE / 'lifecycle-hazard.py'
    for case in ['normal', 'descendant', 'timeout', 'late', 'invariant', 'parent-death']:
        folder = released_plan.parent / ('control-' + case)
        folder.mkdir()
        next_marker = folder / 'next-command.txt'
        command = [sys.executable, str(hazard), case, str(folder / 'hazard.json')]
        commands = [{'argv': command}, {'argv': [sys.executable, str(hazard), 'next', str(next_marker)]}]
        atomic(folder / 'plan.json', dict(sourceSha=base['sourceSha'], sources=base['sources'],
               commands=commands, lifecycleControl=case))
    # No generic harness executes these. Review the control launch/proof recipes first.

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--dotnet'); p.add_argument('--release-sha')
    p.add_argument('--prepare-lifecycle-controls', action='store_true')
    p.add_argument('--guard',type=Path); p.add_argument('--caller-pid',type=int)
    a=p.parse_args()
    if a.guard:
        return guard(a.guard,a.caller_pid)
    if not a.dotnet or not a.release_sha:
        p.error('Explicit coordinator SOURCE release SHA and pinned dotnet are required')
    plan=prepare(str(Path(a.dotnet).resolve()),a.release_sha)
    if a.prepare_lifecycle_controls:
        prepare_controls(plan)
        print('Lifecycle plans prepared; no controls launched: '+str(plan.parent), flush=True)
        return 0
    child=subprocess.Popen([sys.executable,str(Path(__file__).resolve()),'--guard',str(plan),'--caller-pid',str(os.getpid())],start_new_session=True)
    print('Receipt: '+str(plan.parent/'receipt.json'),flush=True)
    # Guard handles caller termination itself; forward ordinary signals for prompt cleanup.
    def forward(sig, frame):
        try: os.kill(child.pid,signal.SIGTERM)
        except ProcessLookupError: pass
    signal.signal(signal.SIGINT,forward); signal.signal(signal.SIGTERM,forward)
    try:
        return child.wait(timeout=HARD_SECONDS+2)
    except subprocess.TimeoutExpired:
        os.kill(child.pid,signal.SIGKILL); child.wait(timeout=1)
        return 1  # no late success or subsequent command

if __name__=='__main__':
    sys.exit(main())
