#!/usr/bin/env python3
"""Exact finite source/preparation/check/counter phases. ROOT launches counters only after release."""
import sys
sys.dont_write_bytecode = True
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
NET = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
FLAGS = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
FIXED_ENV = dict(DOTNET_ROOT=str(Path(NET).parent), DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
           DOTNET_NOLOGO='1', DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1', DOTNET_EnableDiagnostics='0',DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',DOTNET_ROLL_FORWARD='Disable',DOTNET_gcServer='0',DOTNET_GCConcurrent='0',
           MSBUILDDISABLENODEREUSE='1', PYTHONDONTWRITEBYTECODE='1', NUGET_PACKAGES='/Users/sebastienros/.nuget/packages')
def effective_environment():
    result = {key: os.environ[key] for key in ['PATH','HOME','TMPDIR','LANG','XEMELE_CORPUS_OWNED_REGISTRIES','XEMELE_GATE_PACKET_SHA256'] if key in os.environ}
    result.update(FIXED_ENV)
    return result
FIXED_ENV.update(XEMELE_DOTNET=NET,XEMELE_BCL_RUNNER=str(ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'),XEMELE_CORPUS_RESOURCES=str(ROOT/'tests/corpora/xml'),LANG='C.UTF-8',LC_ALL='C.UTF-8',LC_CTYPE='C.UTF-8',MallocNanoZone='0')
ENV = effective_environment()

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
from recipes import commands, PHASES
from identities import source_identity, runtime_identity, binaries
import semantic
STATE = ROOT/'artifacts/M8-gate/state'

def artifact_snapshot():
    return {str(p.relative_to(ROOT)):sha(p) for p in (ROOT/'artifacts').rglob('*') if p.is_file() and 'M8-gate' not in p.parts}

def stable_inputs(inputs):
    # The approved ancestor ownership registry is phase-specific and remains fully pinned in that phase's receipt.
    return {**inputs, 'environment': {k: v for k, v in inputs['environment'].items() if k != 'XEMELE_CORPUS_OWNED_REGISTRIES'}}

def authority_identity():
    packet=json.loads((HERE/'source-packet.json').read_text())
    expected=packet['reuse']['recordHashes']
    actual={name:sha(ROOT/name) for name in expected}
    assert actual==expected, 'Authoritative reuse/policy record bytes changed'
    return actual

def input_identities():
    return {'source': source_identity(), 'runtime': runtime_identity(), 'authority':authority_identity(), 'environment': effective_environment(),
            'revision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True, timeout=5).strip()}

def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result

def verify_preparation_certificate(path, prepared_path):
    path = Path(path).resolve()
    assert (ROOT/'artifacts/M8-gate').resolve() in path.parents, 'Root preparation certificate must be in task artifacts'
    certificate = json.loads(path.read_text())
    assert certificate['issuer'] == 'M8-root-coordinator', 'Root-issued preparation certificate required'
    assert certificate['preparedSha256'] == sha(prepared_path), 'Root certificate does not match preparation receipt'
    outer_path = Path(certificate['outerReceiptPath']).resolve()
    assert (ROOT/'artifacts/M8-gate').resolve() in outer_path.parents, 'Owned outer terminal receipt required'
    assert certificate['outerReceiptSha256'] == sha(outer_path), 'Outer terminal receipt changed'
    outer = json.loads(outer_path.read_text())
    assert outer['exit'] == 0 and outer['reason'] == 'completed', 'Outer preparation did not exit successfully'
    assert outer['directChildReaped'] and outer['ownedGroupAbsent'] and outer['logClosed'], 'Outer family did not terminate cleanly'
    ownership = outer['nestedOwnership']
    assert ownership['allOwnedGroupsAbsent'] and not ownership.get('pendingIntents') and not ownership.get('remainingGroups'), 'Outer nested ownership incomplete'
    assert outer['deadlineExceeded'] is False and outer['inputsMatched'] is True and outer['logsInspected'] is True and outer['rawDeleted'] is True, 'Outer deadline/binding/inspection/deletion guards failed'
    return {'path': str(path), 'sha256': sha(path), 'outerReceiptSha256': sha(outer_path)}

def publish_phase(target, payload, identifier):
    temporary = target.parent/(identifier+'-publish.tmp')
    try:
        with temporary.open('x') as stream:
            json.dump(payload, stream, indent=2); stream.write('\n'); stream.flush(); os.fsync(stream.fileno())
        os.replace(temporary, target)
        descriptor = os.open(target.parent, os.O_RDONLY)
        try: os.fsync(descriptor)
        finally: os.close(descriptor)
    finally:
        temporary.unlink(missing_ok=True)

def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ['recipe', 'prepare', 'check', 'counter', 'identity']: raise SystemExit('recipe|prepare|check|counter ROOT_CERTIFICATE')
    phase = sys.argv[1]
    if len(sys.argv) != (3 if phase in ['check','counter','identity'] else 2): raise SystemExit('recipe|prepare|check|counter ROOT_CERTIFICATE')
    if phase == 'recipe':
        print(json.dumps({'inputs': input_identities(), 'prepare': commands('prepare'), 'check': commands('check'), 'counter':commands('counter'),
                          'phaseBounds': PHASES, 'per_command_cleanup_seconds': 10,
                          'phase_terminal_cleanup_seconds': 120, 'supervisor': 'docs/reports/M8-xsd-corpus/supervision.py',
                          'check_requires': 'ROOT-issued certificate matching prepared.json and completed clean outer terminal receipt; worker never issues certificates',
                          'nested_original_sha256': sha(ROOT/'docs/reports/M8-xsd-corpus/nested_ownership.py'),
                          'nested_copy_sha256': sha(HERE/'nested_ownership.py')}, indent=2)); return
    # Set one total deadline before binding; neither failed commands nor binding work reset it.
    started = time.monotonic()
    hard_cap = min(started+PHASES[phase]['work']+PHASES[phase]['reserve'],float(os.environ.get('XEMELE_GATE_HARD_DEADLINE','inf')))
    end = min(started+PHASES[phase]['work'],float(os.environ.get('XEMELE_GATE_WORK_DEADLINE','inf')),hard_cap-PHASES[phase]['reserve'])
    hard = min(end+PHASES[phase]['reserve'],hard_cap)
    identifier = phase+'-'+uuid.uuid4().hex
    STATE.mkdir(parents=True,exist_ok=True)
    receipt = STATE/(identifier+'.receipt.json')
    supervision = module('gate_supervision', ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
    nested = module('gate_nested', HERE/'nested_ownership.py')
    supervision.ROOT = ROOT
    payload = {'phase': phase, 'passed': False, 'commands': [], 'bindingFailure': None, 'primaryFailure': None,
               'workDeadline': end, 'hardDeadline': hard, 'started': started, 'rawDeleted': False}
    transient = ROOT/'artifacts/M8-gate'/identifier
    logs_inspected = False
    with nested.interruptions():
        try:
            payload['inputs'] = input_identities()
            assert os.environ['XEMELE_GATE_PACKET_SHA256']==sha(HERE/'source-packet.json'),'Root SOURCE release packet hash required'
            packet=json.loads((HERE/'source-packet.json').read_text())
            assert payload['inputs']['source']==packet['sourceClosure'],'Reviewed complete source packet changed'
            payload['beforeArtifacts']=artifact_snapshot()
            assert 'XEMELE_GATE_IDENTITY_NEGATIVE' not in os.environ, 'Unknown effective caller identity control refused'
            assert payload['inputs']['environment'] == ENV, 'Effective environment changed before launch'
            # Ancestors must be valid open registries in this task's artifact root, and propagate to every command.
            assert nested.registries(ENV), 'At least the root ancestor registry is required'
            if phase in ['check','counter','identity']:
                payload['preparationCertificate'] = verify_preparation_certificate(sys.argv[2], STATE/('check.json' if phase=='counter' else 'prepared.json'))
                prepared = json.loads((STATE/'prepared.json').read_text())
                assert prepared['passed'] and prepared['rawDeleted'] and not prepared['deadlineExceeded'], 'Preparation did not pass'
                if phase=='counter':
                    prior=json.loads((STATE/'check.json').read_text())
                    nested.durable(STATE/'counter-consumed.json',{'sourcePacketSha256':os.environ['XEMELE_GATE_PACKET_SHA256'],'checkCertificateSha256':sha(Path(sys.argv[2])),'state':'claimed; never reusable'})
                    assert prior['passed'] and prior['rawDeleted'] and not prior['deadlineExceeded'], 'Counter phase needs a completed checked recipe'
                assert stable_inputs(prepared['inputs']) == stable_inputs(payload['inputs']), 'Preparation inputs changed'
                payload['binaries'] = binaries()
                assert prepared['binaries'] == payload['binaries'], 'Prepared output directories changed'
            transient.mkdir(parents=True, exist_ok=False); supervision.ARTIFACTS = transient
            for index, (argv, seconds) in enumerate(commands(phase)):
                assert authority_identity()==payload['inputs']['authority'], 'Authority/policy reuse record drift before command'
                assert source_identity()==payload['inputs']['source'], 'Source membership/config drift before command'
                if phase in ['check','counter','identity']:assert binaries()==payload['binaries'], 'Frozen dependency/resource/package drift before command'
                work = min(end, time.monotonic()+seconds)
                assert time.monotonic() < work, 'Total work deadline reached'
                unresolved = {'argv': argv, 'reason': 'MissingTerminalReceipt', 'directChildReaped': False, 'ownedGroupAbsent': False, 'logClosed': False}
                payload['commands'].append(unresolved)
                argv = [arg.replace('${RAW}',str(transient)).replace('${CERT}',sys.argv[2] if phase in ['check','counter','identity'] else '') for arg in argv]
                child_env = ENV|{'XEMELE_GATE_RUN_DIR':str(transient),'XEMELE_GATE_WORK_DEADLINE':str(work),'XEMELE_GATE_HARD_DEADLINE':str(min(hard-max(2,PHASES[phase]['reserve']-20),work+10))}
                record = nested.supervise_family_command(supervision, argv, index, child_env, work, min(hard-max(2,PHASES[phase]['reserve']-20), work+10))
                assert record is not None, 'Missing command terminal receipt'
                record['argv'] = argv; record['effectiveEnvironment']=child_env; record['workDeadline']=work; record['hardDeadline']=min(hard-max(2,PHASES[phase]['reserve']-20),work+10); payload['commands'][-1] = record
                workload_failed = not supervision.workload_succeeded(record) or not record['nestedOwnership']['allOwnedGroupsAbsent']
                if workload_failed:
                    payload['primaryFailure'] = {'command': index, 'argv': argv, 'exit': record['exit'],
                                                 'reason': record['reason'], 'diagnostics': []}
                # Bind a terminal receipt before output inspection can fail.
                nested.durable(STATE/(identifier+f'-command-{index}.receipt.json'), record)
                assert record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'] and record['nestedOwnership']['allOwnedGroupsAbsent'], 'Whole family must be absent before inspection'
                output = (transient/f'command-{index}.log').read_text(errors='replace')
                record['summary'] = [line for line in output.splitlines() if line.startswith(('PASS ', 'BOUNDARY ', 'API baseline', 'Validated ', 'Build succeeded.', 'Build FAILED.', '    0 Warning', '    0 Error'))][-12:]
                record['diagnostics'] = list(dict.fromkeys(line[:512] for line in output.splitlines() if ': error ' in line or line.startswith(('error ', 'Unhandled exception.', 'System.'))))[:8]
                # Inspection occurs after this whole registered command family is durable and absent.
                try: record['semantic']=semantic.inspect_command(argv,output,transient)
                except BaseException as error:
                    payload['inspectionFailure']=type(error).__name__+': '+str(error)
                    record['semanticFailure']=payload['inspectionFailure']
                    if '-result-xml' in argv:
                        diagnostic=Path(argv[argv.index('-result-xml')+1]).with_name(Path(argv[argv.index('-result-xml')+1]).stem+'-inspection.json')
                        if diagnostic.is_file():record['failedSuiteDiagnostics']=json.loads(diagnostic.read_text())
                    workload_failed=True
                    if payload['primaryFailure'] is None:payload['primaryFailure']={'command':index,'argv':argv,'reason':payload['inspectionFailure']}
                nested.durable(STATE/(identifier+f'-inspected-{index}.receipt.json'),record)
                print(json.dumps(record), flush=True)
                if workload_failed:
                    payload['primaryFailure']['diagnostics'] = record['diagnostics']
                    print('\n'.join(output.splitlines()[-16:]), flush=True)
                    break
            logs_inspected = 'inspectionFailure' not in payload
            payload['passed'] = (len(payload['commands']) == len(commands(phase)) and all(
                supervision.workload_succeeded(r) and r['nestedOwnership']['allOwnedGroupsAbsent'] and not r.get('semanticFailure') for r in payload['commands']))
        except BaseException as error:
            payload['bindingFailure'] = type(error).__name__+': '+str(error); payload['passed'] = False
            if payload['primaryFailure'] is None:
                payload['primaryFailure'] = {'reason': payload['bindingFailure'], 'stage': 'work/binding'}
        finally:
            try:
                after = input_identities(); payload['afterInputs'] = after
                assert payload.get('inputs') == after, 'Source/runtime/effective environment/revision changed during phase'
                after_binaries = binaries(); payload['afterBinaries'] = after_binaries
                if phase in ['check','counter','identity']: assert payload.get('binaries') == after_binaries, 'Output directory drift during checks'
                else: payload['binaries'] = after_binaries
                if phase in ['check','counter','identity']:
                    certificate_after = verify_preparation_certificate(sys.argv[2], STATE/('check.json' if phase=='counter' else 'prepared.json'))
                    assert payload.get('preparationCertificate') == certificate_after, 'Preparation certificate or outer receipt changed during checks'
            except BaseException as error:
                payload['postPhaseBindingFailure'] = type(error).__name__+': '+str(error); payload['passed'] = False
                if payload['bindingFailure'] is None: payload['bindingFailure'] = payload['postPhaseBindingFailure']
                if payload['primaryFailure'] is None:
                    payload['primaryFailure'] = {'reason': payload['postPhaseBindingFailure'], 'stage': 'post-phase binding'}
            # Sweep every local registry, including one whose supervisor raised before returning a record.
            cleanup = []
            try:
                if transient.exists():
                    for registry in sorted(transient.glob('nested-*')):
                        if (registry/'cleanup.json').exists():
                            record = json.loads((registry/'cleanup.json').read_text())
                            if (registry/'deadline-failure.json').exists(): record = json.loads((registry/'deadline-failure.json').read_text())
                        else: record = nested.drain(supervision, registry, hard-2)
                        cleanup.append(record)
            except BaseException as error:
                cleanup.append({'allOwnedGroupsAbsent': False, 'cleanupError': type(error).__name__+': '+str(error)})
                payload['passed'] = False
            payload['terminalCleanup'] = cleanup
            payload['allOwnedGroupsAbsent'] = all(r.get('allOwnedGroupsAbsent') and not r.get('pendingIntents') and not r.get('remainingGroups') for r in cleanup)
            payload['allDirectChildrenReaped'] = all(r['directChildReaped'] and r['ownedGroupAbsent'] and r['logClosed'] for r in payload['commands'])
            payload['deadlineExceeded'] = time.monotonic() >= hard
            payload['passed'] &= payload['allOwnedGroupsAbsent'] and payload['allDirectChildrenReaped'] and not payload['deadlineExceeded']
            payload['logsInspected'] = logs_inspected
            if phase=='counter' and payload['allOwnedGroupsAbsent'] and payload['allDirectChildrenReaped']:
                try: payload['allocationChildren']=semantic.inspect_allocation_delta(payload['beforeArtifacts'])
                except BaseException as error:
                    payload['allocationInspectionFailure']=type(error).__name__+': '+str(error);payload['passed']=False;logs_inspected=False
                    payload['logsInspected']=False
            nested.durable(receipt, payload)
            if transient.exists() and payload['allOwnedGroupsAbsent'] and payload['allDirectChildrenReaped'] and logs_inspected:
                try:
                    if phase=='counter':semantic.delete_allocation_delta(payload['beforeArtifacts'],payload['allocationChildren'])
                    shutil.rmtree(transient); payload['rawDeleted'] = True
                    if phase=='counter':payload['allocationChildren'].pop('rawIdentities',None)
                except BaseException as error:
                    payload['rawDeletionFailure'] = type(error).__name__+': '+str(error); payload['passed'] = False
            else: payload['passed'] = False
            # Include final inspection/deletion/publication work in the immutable total hard deadline.
            payload['deadlineExceeded'] |= time.monotonic() >= hard
            payload['passed'] &= not payload['deadlineExceeded'] and payload['rawDeleted']
            target = STATE/(phase+'.json' if phase != 'prepare' else 'prepared.json')
            try:
                nested.durable(STATE/(identifier+'-final.receipt.json'), payload)
                publish_phase(target, payload, identifier)
                # Check only after replacement and directory fsync have returned.
                if time.monotonic() >= hard: raise TimeoutError('Phase hard deadline exceeded during final publication')
            except BaseException as error:
                payload['passed'] = False
                payload['publicationFailure'] = type(error).__name__+': '+str(error)
                payload['deadlineExceeded'] |= time.monotonic() >= hard
                # Exactly one reconciliation attempt. A failed/overtime outer run cannot issue a certificate,
                # even if interrupted filesystem writes leave an old inner passed receipt behind.
                try:
                    nested.durable(STATE/(identifier+'-publication-failure.receipt.json'), payload)
                    publish_phase(target, payload, identifier+'-reconcile')
                except BaseException as reconciliation:
                    payload['reconciliationFailure'] = type(reconciliation).__name__+': '+str(reconciliation)
    print(json.dumps({'phase': phase, 'passed': payload['passed'], 'bindingFailure': payload['bindingFailure'], 'primaryFailure': payload['primaryFailure'],
                      'allOwnedGroupsAbsent': payload['allOwnedGroupsAbsent'], 'rawDeleted': payload['rawDeleted'],
                      'deadlineExceeded': payload['deadlineExceeded'], 'receipt': str(receipt)}), flush=True)
    raise SystemExit(0 if payload['passed'] else 1)

if __name__ == '__main__': main()
