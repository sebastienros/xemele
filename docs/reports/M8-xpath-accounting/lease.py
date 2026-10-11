#!/usr/bin/env python3
"""Prepare identities, then execute one externally authorized allocation sequence.

This helper never grants its own lease. --measure requires the coordinator's
exclusive grant. Raw results remain unread until lifecycle RELEASE is reported.
"""
import argparse
import hashlib
import json
import os
import pathlib
import platform
import signal
import subprocess
import time

ROOT = pathlib.Path(__file__).resolve().parents[3]
PREPARED = ROOT / 'artifacts/M8-xpath-accounting/prepared'
RAW = ROOT / 'artifacts/M8-xpath-accounting/raw'
MANIFEST = PREPARED / 'identities.json'
PLAN = ROOT / 'docs/reports/M8-xpath-accounting/measurement-plan.json'
DOTNET = pathlib.Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def environment(revision):
    env = dict(os.environ)
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1', XEMELE_ACCOUNTING_REVISION=revision)
    return env

def projection(env):
    return {k: v for k, v in env.items() if k.startswith(('DOTNET_', 'COMPlus_', 'CORECLR_', 'XEMELE_ACCOUNTING_')) or k in ('PATH', 'LANG', 'LC_ALL', 'LC_CTYPE', 'TZ')}

def identities(files, revision):
    return {'files': {path: sha(pathlib.Path(path)) for path in files},
            'environmentSha256': hashlib.sha256(json.dumps(projection(environment(revision)), sort_keys=True).encode()).hexdigest(),
            'host': {'platform': platform.platform(), 'machine': platform.machine(), 'cpuCount': os.cpu_count()}}

def prepare():
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT).strip(), 'Commit preparation sources first.'
    preparation_revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    revision = json.loads(PLAN.read_text())['source_revision']
    unchanged = subprocess.check_output(['git', 'diff', '--name-only', revision, '--', 'src/Xemele', 'tests/Xemele.Tests/M8-xpath-accounting', 'benchmarks/Xemele.Benchmarks/M8-xpath-accounting', 'docs/reports/M8-xpath-accounting/lease.py'], cwd=ROOT)
    assert not unchanged.strip(), 'Candidate measurement sources changed.'
    files = set()
    for parent in [ROOT / 'src/Xemele', ROOT / 'benchmarks/Xemele.Benchmarks/M8-xpath-accounting', ROOT / 'docs/reports/M8-xpath-accounting', ROOT / 'tests/Xemele.Tests/M8-xpath-accounting']:
        for path in parent.rglob('*'):
            if path.is_file() and path.suffix in ('.cs', '.csproj', '.fixture', '.py', '.txt', '.json', '.dll', '.pdb', '.config'):
                files.add(str(path.resolve()))
    for path in PREPARED.rglob('*'):
        if path.is_file() and path.suffix in ('.dll', '.pdb', '.json', '.fixture', '.cs', '.config') and path != MANIFEST:
            files.add(str(path.resolve()))
    for name in ('Directory.Build.props', 'Directory.Packages.props', 'global.json', 'NuGet.Config'):
        path = ROOT / name
        if path.exists(): files.add(str(path.resolve()))
    runtime_root = DOTNET.resolve().parent
    files.add(str(DOTNET.resolve()))
    for parent in [runtime_root / 'sdk/10.0.400', runtime_root / 'shared/Microsoft.NETCore.App/10.0.11', runtime_root / 'host/fxr/10.0.11']:
        assert parent.is_dir(), parent
        files.update(str(path.resolve()) for path in parent.rglob('*') if path.is_file())
    prepared = identities(sorted(files), revision)
    prepared.update(revision=revision, preparationRevision=preparation_revision, runtime='10.0.11', sdk='10.0.400', input='<r><a>1</a><a>2</a><a>根😀</a></r>')
    MANIFEST.write_text(json.dumps(prepared, indent=2, sort_keys=True) + '\n')
    print(json.dumps({'revision': revision, 'files': len(files), 'identitySha256': sha(MANIFEST), 'host': prepared['host']}))

def measure():
    manifest = json.loads(MANIFEST.read_text()); plan = json.loads(PLAN.read_text())
    assert manifest['revision'] == plan['source_revision']
    expected = {k: manifest[k] for k in ('files', 'environmentSha256', 'host')}
    assert identities(manifest['files'], manifest['revision']) == expected, 'Initial identity mismatch.'
    RAW.mkdir(exist_ok=True)
    env = environment(manifest['revision']); start = time.monotonic(); results = []; no_children = True
    try:
        for name in ('baseline', 'candidate'):
            argv = [str(DOTNET), '--fx-version', '10.0.11', str(PREPARED / (name + '-probe/Xemele.Benchmarks.dll')), '--measure', str(RAW / (name + '.json'))]
            with (RAW / (name + '.log')).open('w') as output:
                process = subprocess.Popen(argv, cwd=ROOT, env=env, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
                try:
                    code = process.wait(timeout=min(15, 40 - (time.monotonic() - start)))
                finally:
                    try: os.killpg(process.pid, signal.SIGTERM)
                    except ProcessLookupError: pass
                    if process.poll() is None:
                        try: process.wait(timeout=1)
                        except subprocess.TimeoutExpired: os.killpg(process.pid, signal.SIGKILL); process.wait(timeout=1)
                    # Reap or kill any unexpected process still in the owned group.
                    try: os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError: pass
                    try: os.killpg(process.pid, 0); no_children = False
                    except ProcessLookupError: pass
            results.append({'name': name, 'exitCode': code})
            assert code == 0 and no_children, 'Measurement failure; release before inspecting logs.'
        assert identities(manifest['files'], manifest['revision']) == expected, 'Final identity mismatch.'
        print(json.dumps({'lifecycle': 'RELEASE_READY', 'results': results, 'no_children': no_children, 'identities': 'passed', 'durationSeconds': time.monotonic() - start}))
    finally:
        (RAW / 'lifecycle.json').write_text(json.dumps({'results': results, 'no_children': no_children, 'durationSeconds': time.monotonic() - start}, indent=2) + '\n')

parser = argparse.ArgumentParser(); parser.add_argument('mode', choices=['--prepare', '--measure'])
# A positional value starting with -- is supplied after -- to argparse.
args = parser.parse_args()
if args.mode == '--prepare': prepare()
else: measure()
