#!/usr/bin/env python3
"""Prepare identities or execute one explicitly granted, prebuilt allocation probe.

Measurement mode never opens raw output: it reports lifecycle completion only.
The coordinator records RELEASE before either party inspects allocation.json.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
ARTIFACTS = ROOT / 'artifacts/M8-rng-compile'
HOST = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
BIN = ROOT / 'benchmarks/Xemele.Benchmarks/M8-rng-compile/bin/Release/net10.0'
SOURCE = 'd2066ee11a6c2616a5806ce67ae04385ab703316'
MANIFEST = ROOT / 'docs/reports/M8-rng-compile/preparation.json'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def members(group):
    listing = subprocess.check_output(['ps', '-axo', 'pid,pgid'], text=True)
    return [int(pid) for pid, pgid in (line.split() for line in listing.splitlines()[1:]) if int(pgid) == group]


def prepare():
    control = json.loads((ARTIFACTS / 'control.json').read_text())
    assert control['mode'] == '--control' and control['sourceRevision'] == SOURCE
    assert len(control['rows']) == 11 and all(row['bytes'] is None for row in control['rows'])
    assert all(row.get('consumed', 64) == 64 for row in control['rows'])
    assert control['retainedApproximateBytes'] is None and control['processWorkingSet'] is None
    # Elevated process-inspection preflight; no command text or unrelated processes
    # are emitted. This must succeed before requesting the coordinator's lease.
    inspected = len(subprocess.check_output(['ps', '-axo', 'pid,pgid'], text=True).splitlines()) - 1
    files = [HOST, HOST.parent / 'shared/Microsoft.NETCore.App/10.0.11/System.Private.CoreLib.dll']
    files += sorted(path for path in BIN.iterdir() if path.is_file())
    tracked = subprocess.check_output(['git', 'ls-files', 'src/Xemele', 'Directory.Build.props', 'Directory.Packages.props', 'global.json',
                                      'benchmarks/Xemele.Benchmarks/M8-rng-compile', 'docs/reports/M8-rng-compile/lease.py'], cwd=ROOT, text=True)
    files += [ROOT / item for item in tracked.splitlines() if item.endswith(('.cs', '.csproj', '.props', '.json', '.py'))]
    files += [ARTIFACTS / 'control.json']
    identities = [{'path': str(path), 'sha256': digest(path)} for path in sorted(set(files))]
    manifest = {
        'sourceRevision': SOURCE,
        'preparationRevision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'cwd': str(ROOT), 'runtime': '10.0.11', 'sdk': '10.0.400', 'maximumSeconds': 30,
        'command': ['python3', 'docs/reports/M8-rng-compile/lease.py', '--measure', '--lease', 'M8-rng-compile-allocation-1'],
        'childCommand': [str(HOST), '--fx-version', '10.0.11', str(BIN / 'Xemele.Benchmarks.dll'), '--measure', str(ARTIFACTS / 'allocation.json')],
        'environment': {'DOTNET_TieredCompilation': '0', 'DOTNET_gcServer': '0', 'DOTNET_GCConcurrent': '0', 'XEMELE_RNG_REVISION': SOURCE},
        'controlRoutes': 11, 'processInspectionPreflight': {'passed': True, 'processCount': inspected},
        'identities': identities,
        'scope': 'One prebuilt synchronous allocation-only probe, five zero-allocation borrowed/baseline routes, positive owned control, cold parse/compile, two owned graph cases, overlap scratch, invalid diagnostic, retained16graphs/RSS. No BDN/timing campaign, builds, tests, resolver allocation regression, or raw inspection under lease.'
    }
    MANIFEST.write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps({'prepared': True, 'source': SOURCE, 'identities': len(identities), 'manifestSha256': digest(MANIFEST)}))


def measure(lease):
    assert lease == 'M8-rng-compile-allocation-1', 'Exact coordinator lease required'
    manifest = json.loads(MANIFEST.read_text())
    for identity in manifest['identities']:
        assert digest(Path(identity['path'])) == identity['sha256'], identity['path']
    environment = os.environ.copy()
    environment.update(manifest['environment'])
    start = time.monotonic()
    child = None
    timed_out = False
    log_path = ARTIFACTS / 'allocation.log'
    with log_path.open('wb') as log:
        try:
            child = subprocess.Popen(manifest['childCommand'], cwd=ROOT, env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                child.wait(timeout=manifest['maximumSeconds'])
            except subprocess.TimeoutExpired:
                timed_out = True
                os.killpg(child.pid, signal.SIGKILL)
                child.wait()
        finally:
            if child is not None and child.poll() is None:
                os.killpg(child.pid, signal.SIGKILL)
                child.wait()
            if child is not None and members(child.pid):
                os.killpg(child.pid, signal.SIGKILL)
    assert child is not None
    remaining = members(child.pid)
    result = {'lease': lease, 'pid': child.pid, 'exit': child.returncode, 'reaped': child.poll() is not None,
              'timedOut': timed_out, 'elapsedSeconds': time.monotonic() - start, 'groupMembers': remaining,
              'logClosed': True, 'rawInspected': False, 'identities': len(manifest['identities'])}
    (ARTIFACTS / 'lifecycle.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))
    if child.returncode != 0 or timed_out or remaining:
        raise SystemExit(1)


parser = argparse.ArgumentParser()
parser.add_argument('--prepare', action='store_true')
parser.add_argument('--measure', action='store_true')
parser.add_argument('--lease')
arguments = parser.parse_args()
assert arguments.prepare != arguments.measure
if arguments.prepare:
    prepare()
else:
    measure(arguments.lease)
