#!/usr/bin/env python3
"""Finite correctness preparation/checks, using accepted corpus process supervision; no counters."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
NET = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
FLAGS = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
ENV = {key: os.environ[key] for key in ['PATH', 'HOME', 'TMPDIR', 'LANG'] if key in os.environ}
ENV.update(DOTNET_ROOT=str(Path(NET).parent), DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
           DOTNET_NOLOGO='1', DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1', DOTNET_EnableDiagnostics='0',
           MSBUILDDISABLENODEREUSE='1', NUGET_PACKAGES='/Users/sebastienros/.nuget/packages')

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def source_identity():
    groups = ['src/Xemele', 'src/Xemele.Tool', 'tests/Xemele.Tests/M8-cli', 'tests/Xemele.Conformance.Tests/SchemaIntegration',
              'tests/Xemele.Fuzz/Validation', 'docs/reports/M8-cli', 'samples/M8-cli', 'docs/api']
    files = []
    for group in groups:
        files += [p for p in (ROOT/group).rglob('*') if p.is_file() and not {'bin', 'obj', '__pycache__'}.intersection(p.relative_to(ROOT).parts)
                  and p.name not in ['prepared.json', 'results.json', 'handoff.md']]
    files += [ROOT/n for n in ['global.json', 'Directory.Build.props', 'Directory.Packages.props', 'docs/reports/M8-xsd-corpus/supervision.py']]
    rows = {str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))}
    return {'files': len(rows), 'sha256': hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest()}

def runtime_identity():
    runtime = Path(NET).parent/'shared/Microsoft.NETCore.App/10.0.11'
    paths = [Path(NET), runtime/'libcoreclr.dylib', runtime/'System.Private.CoreLib.dll',
             Path(NET).parent/'sdk/10.0.400/Roslyn/bincore/csc.dll']
    return {str(p): sha(p) for p in paths}

def binaries():
    paths = [ROOT/'src/Xemele.Tool/bin/Release/net10.0/Xemele.Tool.dll', ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll',
             ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll']
    for mode in ['false', 'true']:
        folder = ROOT/f'artifacts/M8-cli/probe-{mode}-bin/Release/net10.0'
        paths += [folder/'SchemaCliChecks.dll', folder/'Xemele.dll', folder/'SchemaCliChecks.runtimeconfig.json']
    return {str(p.relative_to(ROOT)): sha(p) for p in paths}

def commands(phase):
    if phase == 'prepare':
        projects = [('src/Xemele.Tool/Xemele.Tool.csproj', []), ('tests/Xemele.Tests/M8-cli/Probe.csproj', ['-p:SchemaXmlOnly=false']),
                    ('tests/Xemele.Tests/M8-cli/Probe.csproj', ['-p:SchemaXmlOnly=true']), ('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', [])]
        return [([NET, verb, project, *(['--source', '/Users/sebastienros/.nuget/packages'] if verb == 'restore' else ['-c', 'Release', '--no-restore']), *FLAGS, *extra], 240)
                for project, extra in projects for verb in ['restore', 'build']]
    return [([NET, '--fx-version', '10.0.11', f'artifacts/M8-cli/probe-{mode}-bin/Release/net10.0/SchemaCliChecks.dll'], 120) for mode in ['false', 'true']] + [
        ([sys.executable, 'tests/Xemele.Tests/M8-cli/process_controls.py'], 120),
        ([sys.executable, 'docs/reports/M5-validation-model/check_api.py'], 60),
        ([sys.executable, 'tools/planning/tasks.py', 'validate'], 30), (['git', 'diff', '--check'], 30)]

def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ['recipe', 'prepare', 'check']: raise SystemExit('recipe|prepare|check')
    phase = sys.argv[1]
    inputs = {'source': source_identity(), 'runtime': runtime_identity(), 'environment': ENV,
              'revision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True, timeout=5).strip()}
    if phase == 'recipe':
        print(json.dumps({'inputs': inputs, 'prepare': commands('prepare'), 'check': commands('check'),
                          'prepare_total_work_seconds': 1500, 'check_total_work_seconds': 480, 'per_command_cleanup_seconds': 10,
                          'phase_terminal_cleanup_seconds': 10, 'supervisor': 'docs/reports/M8-xsd-corpus/supervision.py'}, indent=2)); return
    if phase == 'check':
        prepared = json.loads((HERE/'prepared.json').read_text())
        assert prepared['inputs'] == inputs, 'Preparation inputs changed'
        assert prepared['binaries'] == binaries(), 'Prepared binaries changed'
    spec = importlib.util.spec_from_file_location('schema_cli_supervision', ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
    supervision = importlib.util.module_from_spec(spec); spec.loader.exec_module(supervision)
    supervision.ROOT = ROOT
    transient = ROOT/'artifacts/M8-cli'/('transient-'+phase)
    transient.mkdir(parents=True, exist_ok=False); supervision.ARTIFACTS = transient
    records = []
    end = time.monotonic() + (1500 if phase == 'prepare' else 480)
    succeeded = False
    try:
        for index, (argv, seconds) in enumerate(commands(phase)):
            work = min(end, time.monotonic()+seconds)
            record = supervision.supervise_command(argv, index, ENV, work, work+10)
            record['argv'] = argv
            records.append(record)
            output = (transient/f'command-{index}.log').read_text(errors='replace')
            # Inspect actual outputs before deletion. Keep concise PASS/boundary/build summaries.
            record['summary'] = [line for line in output.splitlines() if line.startswith(('PASS ', 'BOUNDARY ', 'API baseline', 'Validated ', 'Build succeeded.', '    0 Warning', '    0 Error'))][-12:]
            print(json.dumps(record), flush=True)
            if not supervision.workload_succeeded(record):
                print('\n'.join(output.splitlines()[-16:]), flush=True)
                break
        succeeded = len(records) == len(commands(phase)) and all(supervision.workload_succeeded(r) for r in records)
        payload = {'inputs': inputs, 'passed': succeeded, 'commands': records}
        if succeeded and phase == 'prepare': payload['binaries'] = binaries()
        (HERE/('prepared.json' if phase == 'prepare' else 'results.json')).write_text(json.dumps(payload, indent=2)+'\n')
    finally:
        # Raw output deleted only after every launched owned process group is absent and output inspected.
        if all(r['directChildReaped'] and r['ownedGroupAbsent'] and r['logClosed'] for r in records): shutil.rmtree(transient)
        else: raise RuntimeError('Cleanup incomplete; raw output retained for terminal cleanup')
    raise SystemExit(0 if succeeded else 1)

if __name__ == '__main__': main()
