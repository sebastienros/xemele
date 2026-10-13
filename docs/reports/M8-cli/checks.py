#!/usr/bin/env python3
"""Finite correctness preparation/checks, using accepted corpus process supervision; no counters."""
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
           DOTNET_NOLOGO='1', DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1', DOTNET_EnableDiagnostics='0',
           MSBUILDDISABLENODEREUSE='1', PYTHONDONTWRITEBYTECODE='1', NUGET_PACKAGES='/Users/sebastienros/.nuget/packages')
def effective_environment():
    result = {key: os.environ[key] for key in ['PATH', 'HOME', 'TMPDIR', 'LANG', 'XEMELE_CORPUS_OWNED_REGISTRIES'] if key in os.environ}
    result.update(FIXED_ENV)
    return result
ENV = effective_environment()

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def source_identity():
    groups = ['src/Xemele', 'src/Xemele.Tool', 'tests/Xemele.Tests/M8-cli', 'tests/Xemele.Conformance.Tests/SchemaIntegration',
              'tests/Xemele.Fuzz/Validation', 'docs/reports/M8-cli', 'samples/M8-cli', 'docs/api', 'docs/reports/M0-contracts/ApiCheck']
    files = []
    for group in groups:
        files += [p for p in (ROOT/group).rglob('*') if p.is_file() and not {'bin', 'obj', '__pycache__'}.intersection(p.relative_to(ROOT).parts)
                  and p.name not in ['prepared.json', 'results.json', 'handoff.md'] and not p.name.endswith('.receipt.json')]
    files += [ROOT/n for n in ['global.json', 'Directory.Build.props', 'Directory.Packages.props', 'docs/reports/M8-xsd-corpus/supervision.py', 'docs/reports/M8-xsd-corpus/nested_ownership.py',
                             'docs/reports/M5-validation-model/check_api.py', 'tools/planning/tasks.py']]
    rows = {str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))}
    return {'files': len(rows), 'sha256': hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest()}

def directory_identity(folder):
    assert folder.is_dir(), str(folder)
    rows = {str(p.relative_to(folder)): sha(p) for p in sorted(folder.rglob('*')) if p.is_file()}
    assert rows, str(folder)
    return {'files': len(rows), 'sha256': hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest()}

def runtime_identity():
    installation = Path(NET).parent
    return {'host': sha(Path(NET)), 'python': {'path': sys.executable, 'sha256': sha(Path(sys.executable))},
            'runtime10.0.11': directory_identity(installation/'shared/Microsoft.NETCore.App/10.0.11'),
            'sdk10.0.400': directory_identity(installation/'sdk/10.0.400')}

def binaries():
    folders = ['src/Xemele.Tool/bin/Release/net10.0', 'src/Xemele/bin/Release/net10.0',
               'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0',
               'artifacts/M8-cli/probe-false-bin/Release/net10.0', 'artifacts/M8-cli/probe-true-bin/Release/net10.0',
               'artifacts/M8-cli/xml-only-bin/Release/net10.0']
    return {folder: directory_identity(ROOT/folder) for folder in folders}

def stable_inputs(inputs):
    # The approved ancestor ownership registry is phase-specific and remains fully pinned in that phase's receipt.
    return {**inputs, 'environment': {k: v for k, v in inputs['environment'].items() if k != 'XEMELE_CORPUS_OWNED_REGISTRIES'}}

def input_identities():
    return {'source': source_identity(), 'runtime': runtime_identity(), 'environment': effective_environment(),
            'revision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True, timeout=5).strip()}

def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result

def commands(phase):
    if phase == 'prepare':
        projects = [('src/Xemele.Tool/Xemele.Tool.csproj', []), ('tests/Xemele.Tests/M8-cli/Probe.csproj', ['-p:SchemaXmlOnly=false']),
                    ('tests/Xemele.Tests/M8-cli/Probe.csproj', ['-p:SchemaXmlOnly=true']), ('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', [])]
        return [([NET, verb, project, *(['--source', '/Users/sebastienros/.nuget/packages'] if verb == 'restore' else ['-c', 'Release', '--no-restore']), *FLAGS, *extra], 240)
                for project, extra in projects for verb in ['restore', 'build']]
    return [([NET, '--fx-version', '10.0.11', f'artifacts/M8-cli/probe-{mode}-bin/Release/net10.0/SchemaCliChecks.dll'], 120) for mode in ['false', 'true']] + [
        ([sys.executable, 'docs/reports/M8-cli/guard_controls.py'], 60),
        ([sys.executable, 'tests/Xemele.Tests/M8-cli/process_controls.py'], 120),
        ([sys.executable, 'docs/reports/M5-validation-model/check_api.py'], 60),
        ([sys.executable, 'tools/planning/tasks.py', 'validate'], 30), (['git', 'diff', '--check'], 30)]

def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ['recipe', 'prepare', 'check']: raise SystemExit('recipe|prepare|check')
    phase = sys.argv[1]
    if phase == 'recipe':
        print(json.dumps({'inputs': input_identities(), 'prepare': commands('prepare'), 'check': commands('check'),
                          'prepare_total_work_seconds': 1500, 'check_total_work_seconds': 540, 'per_command_cleanup_seconds': 10,
                          'phase_terminal_cleanup_seconds': 10, 'supervisor': 'docs/reports/M8-xsd-corpus/supervision.py',
                          'nested_original_sha256': sha(ROOT/'docs/reports/M8-xsd-corpus/nested_ownership.py'),
                          'nested_copy_sha256': sha(HERE/'nested_ownership.py')}, indent=2)); return
    # Set one total deadline before binding; neither failed commands nor binding work reset it.
    started = time.monotonic(); end = started + (1500 if phase == 'prepare' else 540); hard = end+20
    identifier = phase+'-'+uuid.uuid4().hex
    receipt = HERE/(identifier+'.receipt.json')
    supervision = module('schema_cli_supervision', ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
    nested = module('schema_cli_nested', HERE/'nested_ownership.py')
    supervision.ROOT = ROOT
    payload = {'phase': phase, 'passed': False, 'commands': [], 'bindingFailure': None,
               'workDeadline': end, 'hardDeadline': hard, 'started': started, 'rawDeleted': False}
    transient = ROOT/'artifacts/M8-cli'/identifier
    logs_inspected = False
    with nested.interruptions():
        try:
            payload['inputs'] = input_identities()
            assert payload['inputs']['environment'] == ENV, 'Effective environment changed before launch'
            # Ancestors must be valid open registries in this task's artifact root, and propagate to every command.
            nested.registries(ENV)
            if phase == 'check':
                prepared = json.loads((HERE/'prepared.json').read_text())
                assert prepared['passed'], 'Preparation did not pass'
                assert stable_inputs(prepared['inputs']) == stable_inputs(payload['inputs']), 'Preparation inputs changed'
                payload['binaries'] = binaries()
                assert prepared['binaries'] == payload['binaries'], 'Prepared output directories changed'
            transient.mkdir(parents=True, exist_ok=False); supervision.ARTIFACTS = transient
            for index, (argv, seconds) in enumerate(commands(phase)):
                work = min(end, time.monotonic()+seconds)
                assert time.monotonic() < work, 'Total work deadline reached'
                unresolved = {'argv': argv, 'reason': 'MissingTerminalReceipt', 'directChildReaped': False, 'ownedGroupAbsent': False, 'logClosed': False}
                payload['commands'].append(unresolved)
                record = nested.supervise_family_command(supervision, argv, index, ENV, work, min(hard-10, work+10))
                assert record is not None, 'Missing command terminal receipt'
                record['argv'] = argv; payload['commands'][-1] = record
                # Bind a terminal receipt before output inspection can fail.
                nested.durable(HERE/(identifier+f'-command-{index}.receipt.json'), record)
                output = (transient/f'command-{index}.log').read_text(errors='replace')
                record['summary'] = [line for line in output.splitlines() if line.startswith(('PASS ', 'BOUNDARY ', 'API baseline', 'Validated ', 'Build succeeded.', '    0 Warning', '    0 Error'))][-12:]
                print(json.dumps(record), flush=True)
                if not supervision.workload_succeeded(record) or not record['nestedOwnership']['allOwnedGroupsAbsent']:
                    print('\n'.join(output.splitlines()[-16:]), flush=True)
                    break
            logs_inspected = True
            payload['passed'] = (len(payload['commands']) == len(commands(phase)) and all(
                supervision.workload_succeeded(r) and r['nestedOwnership']['allOwnedGroupsAbsent'] for r in payload['commands']))
        except BaseException as error:
            payload['bindingFailure'] = type(error).__name__+': '+str(error); payload['passed'] = False
        finally:
            try:
                after = input_identities(); payload['afterInputs'] = after
                assert payload.get('inputs') == after, 'Source/runtime/effective environment/revision changed during phase'
                after_binaries = binaries(); payload['afterBinaries'] = after_binaries
                if phase == 'check': assert payload.get('binaries') == after_binaries, 'Output directory drift during checks'
                else: payload['binaries'] = after_binaries
            except BaseException as error:
                payload['bindingFailure'] = type(error).__name__+': '+str(error); payload['passed'] = False
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
            nested.durable(receipt, payload)
            if transient.exists() and payload['allOwnedGroupsAbsent'] and payload['allDirectChildrenReaped'] and logs_inspected:
                try: shutil.rmtree(transient); payload['rawDeleted'] = True
                except BaseException as error:
                    payload['rawDeletionFailure'] = type(error).__name__+': '+str(error); payload['passed'] = False
            else: payload['passed'] = False
            # Include final inspection/deletion/publication work in the immutable total hard deadline.
            payload['deadlineExceeded'] |= time.monotonic() >= hard
            payload['passed'] &= not payload['deadlineExceeded'] and payload['rawDeleted']
            nested.durable(HERE/(identifier+'-final.receipt.json'), payload)
            # Stable convenience receipt is fsynced and atomically replaced, including failed attempts.
            target = HERE/('prepared.json' if phase == 'prepare' else 'results.json')
            temporary = HERE/(identifier+'-publish.tmp')
            with temporary.open('x') as stream:
                json.dump(payload, stream, indent=2); stream.write('\n'); stream.flush(); os.fsync(stream.fileno())
            os.replace(temporary, target)
            descriptor = os.open(HERE, os.O_RDONLY)
            try: os.fsync(descriptor)
            finally: os.close(descriptor)
    print(json.dumps({'phase': phase, 'passed': payload['passed'], 'bindingFailure': payload['bindingFailure'],
                      'allOwnedGroupsAbsent': payload['allOwnedGroupsAbsent'], 'rawDeleted': payload['rawDeleted'],
                      'deadlineExceeded': payload['deadlineExceeded'], 'receipt': str(receipt)}), flush=True)
    raise SystemExit(0 if payload['passed'] else 1)

if __name__ == '__main__': main()
