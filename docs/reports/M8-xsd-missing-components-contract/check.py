#!/usr/bin/env python3
"""Owned source-reviewed contract harness. Every actual check remains UNRUN."""
import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import threading
import time
import uuid
import xml.etree.ElementTree as ET
import bindings
import supervision
import nested_ownership as nested

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
FIXTURE = ROOT / 'tests/Contracts/Fixtures/M8/MissingComponents'
OUT = ROOT / 'artifacts/M8-xsd-missing-components-contract'
WORK_SECONDS, CLEANUP_SECONDS, HARD_SECONDS = 180, 12, 195
# Original six required behavioral scenarios; adversarial invariant controls also required.
CASES = ('normal', 'descendant', 'timeout', 'late', 'invariant', 'parent-death')
EXTRA_CASES = ('inventory-failure', 'expired-cleanup', 'delayed-prelaunch', 'delayed-absence',
               'delayed-publication', 'changed-command', 'changed-project', 'changed-source',
               'changed-binary', 'missing-controls', 'mismatched-controls', 'wrong-control-outcome')
ALL_CASES = CASES + EXTRA_CASES


def durable(path, value):
    nested.durable(Path(path), value)


def finite_output(argv, deadline):
    left = deadline - time.monotonic()
    if left <= 0: raise TimeoutError('receipt budget exhausted')
    result = subprocess.check_output(argv, cwd=ROOT, text=True, timeout=min(2, left))
    if time.monotonic() >= deadline: raise TimeoutError('late command output')
    return result


def checkpoint(deadline, caller_pid=None):
    if time.monotonic() >= deadline: raise TimeoutError('work deadline')
    if caller_pid is not None and os.getppid() != caller_pid: raise RuntimeError('caller died')


def publish(path, receipt, deadline, caller_pid=None):
    # A passing candidate is durable first, then committed by a separate exclusive marker.
    # Slow fsync/absence/verification cannot publish a committed late success.
    candidate = Path(path).with_name(Path(path).stem + '-candidate.json')
    checkpoint(deadline, caller_pid)
    durable(candidate, receipt)
    try:
        checkpoint(deadline, caller_pid)
        durable(path, {'candidate': str(candidate), 'sha256': bindings.digest(candidate), 'state': receipt['state']})
        checkpoint(deadline, caller_pid)
    except BaseException:
        # Never overwrite prior evidence. Quarantine candidate/commit and leave failure marker.
        quarantine = Path(path).with_name(Path(path).stem + '-quarantine')
        quarantine.mkdir(exist_ok=False)
        for file in (candidate, Path(path)):
            if file.exists(): file.rename(quarantine / file.name)
        durable(quarantine / 'failure.json', {'state': 'failed', 'reason': 'late final publication'})
        raise


def load_receipt(path, expected_sha):
    assert bindings.digest(path) == expected_sha, 'frozen controls/receipt hash differs'
    commit = json.loads(Path(path).read_text())
    candidate = Path(commit['candidate'])
    assert bindings.digest(candidate) == commit['sha256'], 'receipt candidate changed'
    value = json.loads(candidate.read_text())
    assert value['state'] == commit['state'], 'receipt state mismatch'
    return value


def require_controls(path, identity, plan_sha, source_sha):
    assert path and identity, 'successful matching controls receipt required before compile'
    receipt = load_receipt(path, identity)
    assert receipt['state'] == 'passed' and receipt['planSha256'] == plan_sha and receipt['sourceSha'] == source_sha, 'controls receipt mismatch'
    assert receipt['cases'] == list(ALL_CASES) and all(receipt['outcomes'].values()), 'missing/wrong control outcomes'
    assert receipt['independentAbsence'] and receipt['noNextAfterFailure'], 'controls lifecycle invariants missing'
    return receipt


def verify_inputs(plan_path, expected_sha, deadline, caller_pid, controls_path=None, controls_sha=None, control=None):
    checkpoint(deadline, caller_pid)
    plan = bindings.verify(plan_path, expected_sha, deadline)
    if control:
        assert control in ALL_CASES and plan['controlCase'] == control, 'control recipe mismatch'
        assert plan['commands'] == control_commands(control, plan_path.parent), 'changed required control command'
    else:
        assert plan['commands'] == compile_commands(plan['dotnet'], plan_path.parent), 'changed required compile command'
        require_controls(controls_path, controls_sha, expected_sha, plan['sourceSha'])
    checkpoint(deadline, caller_pid)
    return plan


def output_identities(folder):
    # Includes all real compiled dependency assemblies/configuration and emitted API projections.
    return {str(p): bindings.digest(p) for p in sorted(folder.rglob('*'))
            if p.is_file() and (('bin' in p.parts and p.suffix in ('.dll', '.json', '.pdb')) or p.name.endswith('.actual.txt'))}


def guard(plan_path, expected_sha, caller_pid, controls_path=None, controls_sha=None, control=None):
    started = time.monotonic()
    work, cleanup, hard, cap = (10, 4, 14, 2) if control else (WORK_SECONDS, CLEANUP_SECONDS, HARD_SECONDS, 40)
    work_end, hard_end = started + work, started + hard
    launch_end = work_end - cleanup
    def hard_stop(sig,frame): raise TimeoutError('guard absolute hard deadline')
    signal.signal(signal.SIGALRM,hard_stop); signal.setitimer(signal.ITIMER_REAL,hard)
    plan = verify_inputs(plan_path, expected_sha, launch_end, caller_pid, controls_path, controls_sha, control)
    receipt = {'state': 'failed', 'sourceSha': plan['sourceSha'], 'planSha256': expected_sha,
               'controlCase': control, 'commands': [], 'inputs': plan['inputs'], 'outputs': {},
               'fixtureMethodsExecuted': False, 'runtimeTestsExecuted': False,
               'workSeconds': work, 'cleanupReserveInsideWork': cleanup, 'hardSeconds': hard}
    supervision.ARTIFACTS = plan_path.parent
    environment = os.environ.copy()
    for override in ('MSBuildSDKsPath','MSBUILD_EXE_PATH','DOTNET_ROOT','DOTNET_ROOT_ARM64','DOTNET_ROOT_X64',
                     'DOTNET_ROLL_FORWARD','DOTNET_ROLL_FORWARD_TO_PRERELEASE','MSBuildExtensionsPath',
                     'MSBuildExtensionsPath32','MSBuildExtensionsPath64','NUGET_PLUGIN_PATHS'):
        environment.pop(override,None)
    environment.update(DOTNET_MULTILEVEL_LOOKUP='0', DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
        DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER='1', MSBUILDDISABLENODEREUSE='1',
        DOTNET_CLI_HOME=str(plan_path.parent / 'cli-home'))
    environment[nested.BOUND_PLAN] = str(plan_path); environment[nested.BOUND_HASH] = expected_sha
    done = threading.Event()
    def watch_caller():
        while not done.wait(.05):
            if os.getppid() != caller_pid or time.monotonic() >= launch_end:
                os.kill(os.getpid(), signal.SIGTERM); return
    watcher = threading.Thread(target=watch_caller, daemon=True); watcher.start()
    def before_launch():
        verify_inputs(plan_path, expected_sha, launch_end, caller_pid, controls_path, controls_sha, control)
        for name, digest in receipt['outputs'].items():
            assert bindings.digest(name) == digest, 'compiled assembly/projection changed'
        checkpoint(launch_end, caller_pid)
    nested.PRELAUNCH_HOOK = before_launch
    try:
        with nested.interruptions():
            for index, command in enumerate(plan['commands']):
                before_launch()
                launch_inputs = dict(plan['inputs'], **receipt['outputs'])
                if not control:
                    commit = json.loads(Path(controls_path).read_text())
                    launch_inputs[str(controls_path)] = controls_sha
                    launch_inputs[commit['candidate']] = commit['sha256']
                snapshot = plan_path.parent / f'launch-inputs-{index:03d}.json'
                durable(snapshot, {'inputs':launch_inputs, 'planSha256':expected_sha, 'argv':command['argv']})
                environment[nested.BOUND_PLAN] = str(snapshot)
                environment[nested.BOUND_HASH] = bindings.digest(snapshot)
                before_launch()
                record = nested.supervise_family_command(supervision, command['argv'], index, environment,
                    min(time.monotonic() + cap, launch_end), min(work_end, hard_end))
                record.update(argv=command['argv'], expectedExit=command.get('expectedExit', 0), launchInputs=str(snapshot), launchInputsSha256=bindings.digest(snapshot))
                receipt['commands'].append(record)
                # Exclusive durable cleanup receipt precedes reading log/output.
                durable(plan_path.parent / f'lifecycle-{index:03d}.json', record)
                checkpoint(launch_end, caller_pid)
                clean = record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'] and record['nestedOwnership']['allOwnedGroupsAbsent']
                assert clean and record['reason'] in ('completed', 'failed') and record['exit'] == command.get('expectedExit', 0), 'command lifecycle/outcome failed; no next'
                log = plan_path.parent / f'command-{index}.log'
                record['logSha256'] = bindings.digest(log)
                text = log.read_text(errors='replace')
                assert all(value in text for value in command.get('contains', [])), 'output invariant; no next'
                if not control and index == 0: assert text.strip() == '10.0.400', 'SDK mismatch'
                if 'RUNTIME ' in text:
                    receipt['actualRuntime'] = next(line[8:] for line in text.splitlines() if line.startswith('RUNTIME '))
                    assert receipt['actualRuntime'] == '10.0.11'
                receipt['outputs'].update(output_identities(plan_path.parent))
            before_launch()
            if not control: verify_projection(plan_path.parent)
            checkpoint(launch_end, caller_pid)
            receipt['state'] = 'passed'
    except BaseException as error:
        receipt['reason'] = type(error).__name__ + ': ' + str(error)
    finally:
        done.set(); nested.PRELAUNCH_HOOK = None
        receipt['elapsedSeconds'] = time.monotonic() - started
        if time.monotonic() >= work_end or os.getppid() != caller_pid:
            receipt['state'] = 'failed'; receipt.setdefault('reason', 'caller/deadline before final publication')
        publish(plan_path.parent / 'receipt.json', receipt, work_end, caller_pid if receipt['state']=='passed' else None)
        signal.setitimer(signal.ITIMER_REAL,0)
    return 0 if receipt['state'] == 'passed' else 1


def verify_projection(folder):
    def rows(path): return {r['id']: r for r in map(json.loads, path.read_text().splitlines()[1:])}
    old = rows(folder / 'old.actual.txt'); new = rows(folder / 'candidate.actual.txt')
    expected = old.copy()
    for change in json.loads((HERE / 'baseline-delta.json').read_text())['changes']:
        assert expected[change['old']['id']] == change['old']
        del expected[change['old']['id']]; expected[change['candidate']['id']] = change['candidate']
    assert new == expected, 'projection differs beyond exactly three approved members'


def compile_commands(dotnet, folder):
    commands = [{'argv': [dotnet, '--version'], 'contains': ['10.0.400']},
                {'argv': [dotnet, '--list-runtimes'], 'contains': ['Microsoft.NETCore.App 10.0.11 ']}]
    common = ['--disable-build-servers', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', '-nodeReuse:false', '-maxcpucount:1', '--verbosity', 'quiet']
    for name in ('Prerequisites', 'Old', 'Candidate', 'ApiCheck', 'Reject'):
        p = folder / name / (name + '.csproj')
        commands.append({'argv': [dotnet, 'restore', str(p), '--configfile', str(folder/'NuGet.Config'), '-p:NuGetAudit=false', *common]})
        commands.append({'argv': [dotnet, 'build', str(p), '-c', 'Release', '--no-restore', *common], **({'expectedExit':1,'contains':['CS0266']} if name=='Reject' else {})})
    inspect = [dotnet, '--fx-version', '10.0.11', str(folder/'ApiCheck/bin/Release/net10.0/ApiCheck.dll')]
    old = str(folder/'Old/bin/Release/net10.0/Old.dll'); new = str(folder/'Candidate/bin/Release/net10.0/Candidate.dll')
    for mode, assembly, target in [('compare',old,FIXTURE/'old.golden.txt'),('compare',new,FIXTURE/'candidate.golden.txt'),('emit',old,folder/'old.actual.txt'),('emit',new,folder/'candidate.actual.txt')]:
        commands.append({'argv': [*inspect, mode, assembly, str(target)]})
    for label in ('drop-member', 'remove-nullable'):
        commands.append({'argv': [*inspect,'compare',new,str(folder/(label+'.txt'))], 'expectedExit':1,'contains':['API mismatch']})
    return commands


def control_commands(case, folder):
    return [{'argv': [sys.executable, str(HERE/'lifecycle-hazard.py'), case, str(folder)]},
            {'argv': [sys.executable, str(HERE/'lifecycle-hazard.py'), 'next', str(folder)]}]


def run_owned_guard(plan, expected, controls=None, controls_sha=None, control=None):
    # Ancestor registry includes guard AND every registered nested command/session.
    root_registry = plan.parent / ('caller-' + uuid.uuid4().hex); root_registry.mkdir()
    environment = os.environ.copy()
    environment[nested.REGISTRIES] = json.dumps([str(p) for p in nested.registries(environment)+[root_registry]])
    supervision.ARTIFACTS = plan.parent
    start = time.monotonic(); work_end = start + (24 if control else 200); hard_end = work_end + 14
    def hard_stop(sig,frame): raise TimeoutError('caller absolute hard deadline')
    old_alarm=signal.signal(signal.SIGALRM,hard_stop); signal.setitimer(signal.ITIMER_REAL,hard_end-start)
    argv = [sys.executable,str(HERE/'check.py'),'--guard',str(plan),'--expected-plan-sha',expected,'--caller-pid',str(os.getpid())]
    if control: argv += ['--control-case',control]
    else: argv += ['--controls-receipt',str(controls),'--expected-controls-sha',controls_sha]
    try:
        with nested.interruptions():
            return nested.supervise_family_command(supervision, argv, 999, environment, work_end-(8 if control else 16), work_end-4)
    finally:
        # Kill registered known groups even when ps fails or budget has expired.
        cleanup = nested.drain(supervision, root_registry, work_end)
        durable(plan.parent / 'caller-cleanup.json', cleanup)
        assert cleanup['allOwnedGroupsAbsent'], 'caller could not prove owned family absent'
        checkpoint(work_end)
        signal.setitimer(signal.ITIMER_REAL,0); signal.signal(signal.SIGALRM,old_alarm)


def prepare(dotnet, sha):
    # This function runs only after explicit source review release.
    prepare_deadline = time.monotonic() + 45
    deadline = time.monotonic() + 4
    if finite_output(['git', 'rev-parse', 'HEAD'], deadline).strip() != sha:
        raise RuntimeError('Release SHA must be current HEAD')
    if finite_output(['git', 'status', '--porcelain'], deadline).strip():
        raise RuntimeError('Source release requires clean checkout')
    artifact = ROOT / 'artifacts/M8-xsd-missing-components-contract' / (sha + '-' + uuid.uuid4().hex)
    artifact.mkdir(parents=True)
    sources = {}
    def source(path):
        sources[str(path.relative_to(ROOT))] = bindings.digest(path)
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
    baseline=(FIXTURE/'candidate.golden.txt').read_text()
    for label, contents in [('drop-member', '\n'.join(baseline.splitlines()[:-1])+'\n'),
                             ('remove-nullable',baseline.replace('SchemaName? Xemele.Validation.XsdElementDeclaration.Type','SchemaName Xemele.Validation.XsdElementDeclaration.Type'))]:
        bad=artifact/(label+'.txt'); bad.write_text(contents)
    # Offline config clears inherited user sources; config and generated projects are bound.
    (artifact/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="'+str(FIXTURE)+'"/></packageSources></configuration>')
    (artifact/'Directory.Build.props').write_text('<Project><Import Project="'+str(ROOT/'Directory.Build.props')+'"/></Project>')
    (artifact/'Directory.Build.targets').write_text('<Project/>')
    inputs = {str(ROOT/name):identity for name,identity in sources.items()}
    inputs[str(ROOT/'Directory.Packages.props')] = bindings.digest(ROOT/'Directory.Packages.props')
    for file in artifact.rglob('*'):
        if file.is_file() and file.suffix in ('.csproj','.txt','.props','.targets'):
            inputs[str(file)] = bindings.digest(file)
    inputs[str(artifact/'NuGet.Config')] = bindings.digest(artifact/'NuGet.Config')
    toolroot = Path(dotnet).parent
    # Bind the actual SDK, Roslyn, host and runtime files, not just the dotnet launcher.
    toolfiles = [Path(dotnet)]
    for directory in [toolroot/'sdk/10.0.400', toolroot/'shared/Microsoft.NETCore.App/10.0.11', toolroot/'host/fxr']:
        assert directory.is_dir(), 'pinned SDK/runtime/host path missing'
        toolfiles += [file for file in directory.rglob('*') if file.is_file() and
                      (file.suffix in ('.dll','.json','.props','.targets','.dylib') or file.name.startswith('dotnet'))]
    for file in toolfiles:
        if time.monotonic() >= prepare_deadline: raise TimeoutError('finite preparation budget')
        inputs[str(file)] = bindings.digest(file)
    plan=artifact/'plan.json'; durable(plan, {'sourceSha':sha,'inputs':inputs,'dotnet':dotnet,'commands':compile_commands(dotnet,artifact)})
    checkpoint(prepare_deadline)
    return plan



def main():
    p = argparse.ArgumentParser()
    p.add_argument('--prepare',action='store_true'); p.add_argument('--dotnet'); p.add_argument('--release-sha')
    p.add_argument('--plan',type=Path); p.add_argument('--expected-plan-sha')
    p.add_argument('--guard',type=Path); p.add_argument('--caller-pid',type=int); p.add_argument('--control-case')
    p.add_argument('--controls-receipt',type=Path); p.add_argument('--expected-controls-sha')
    a = p.parse_args()
    if a.prepare:
        assert a.dotnet and a.release_sha
        plan = prepare(str(Path(a.dotnet).resolve()),a.release_sha)
        print(json.dumps({'plan':str(plan),'sha256':bindings.digest(plan),'state':'prepared only; controls/compile UNRUN'}))
        return 0
    assert a.expected_plan_sha, '--guard/--plan requires expected plan hash'
    if a.guard:
        assert a.caller_pid
        return guard(a.guard.resolve(),a.expected_plan_sha,a.caller_pid,a.controls_receipt,a.expected_controls_sha,a.control_case)
    assert a.plan and not a.control_case, 'control cases launch only through finite verifier'
    verify_inputs(a.plan,a.expected_plan_sha,time.monotonic()+5,None,a.controls_receipt,a.expected_controls_sha)
    record = run_owned_guard(a.plan,a.expected_plan_sha,a.controls_receipt,a.expected_controls_sha)
    return 0 if supervision.workload_succeeded(record) else 1

if __name__ == '__main__':
    sys.exit(main())
