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
ARTIFACTS = ROOT / 'artifacts/M8-sch-compile'
HOST = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
BIN = ROOT / 'benchmarks/Xemele.Benchmarks/M8-sch-compile/bin/Release/net10.0'
SOURCE = '65c7da313379285bea463fa992b870550caa4809'
MANIFEST = ROOT / 'docs/reports/M8-sch-compile/preparation.json'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def members(group):
    listing = subprocess.check_output(['ps', '-axo', 'pid,pgid'], text=True, timeout=2)
    return [int(pid) for pid, pgid in (line.split() for line in listing.splitlines()[1:]) if int(pgid) == group]


def prepare():
    control = json.loads((ARTIFACTS / 'control.json').read_text())
    assert control['mode'] == '--control' and control['sourceRevision'] == SOURCE
    assert len(control['rows']) == 6 and all(row['bytes'] is None for row in control['rows'])
    assert all(row.get('consumed', 64) == 64 for row in control['rows'])
    assert control['retainedApproximateBytes'] is None and control['processWorkingSet'] is None
    # Elevated process-inspection preflight; no command text or unrelated processes
    # are emitted. This must succeed before requesting the coordinator's lease.
    inspected = len(subprocess.check_output(['ps', '-axo', 'pid,pgid'], text=True, timeout=2).splitlines()) - 1
    files = [HOST, HOST.parent / 'shared/Microsoft.NETCore.App/10.0.11/System.Private.CoreLib.dll']
    files += sorted(path for path in BIN.iterdir() if path.is_file())
    tracked = subprocess.check_output(['git', 'ls-files', 'src/Xemele', 'Directory.Build.props', 'Directory.Packages.props', 'global.json',
                                      'benchmarks/Xemele.Benchmarks/M8-sch-compile', 'docs/reports/M8-sch-compile/lease.py'], cwd=ROOT, text=True)
    files += [ROOT / item for item in tracked.splitlines() if item.endswith(('.cs', '.csproj', '.props', '.json', '.py'))]
    files += [ARTIFACTS / 'control.json']
    identities = [{'path': str(path), 'sha256': digest(path)} for path in sorted(set(files))]
    manifest = {
        'sourceRevision': SOURCE,
        'preparationRevision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'cwd': str(ROOT), 'runtime': '10.0.11', 'sdk': '10.0.400', 'maximumSeconds': 30,
        'lease': 'M8-sch-compile-allocation-1', 'workSeconds': 25, 'cleanupSeconds': 5,
        'command': ['python3', 'docs/reports/M8-sch-compile/lease.py', '--granted', '<durably-approved-manifest-sha256>'],
        'childCommand': [str(HOST), '--fx-version', '10.0.11', str(BIN / 'Xemele.Benchmarks.dll'), '--measure', str(ARTIFACTS / 'allocation.json')],
        'environment': {'DOTNET_TieredCompilation': '0', 'DOTNET_gcServer': '0', 'DOTNET_GCConcurrent': '0', 'XEMELE_SCH_REVISION': SOURCE},
        'controlRoutes': 6, 'processInspectionPreflight': {'passed': True, 'processCount': inspected},
        'identities': identities,
        'scope': 'One prebuilt synchronous allocation-only probe: positive owned control, cold parse/compile, three owned graph cases (simple, abstract/phase/message, long multilingual), invalid diagnostic, retained16graphs/RSS. No borrowed-zero claim, BDN/timing campaign, builds, tests, resolver allocation regression, or raw inspection under lease.'
    }
    MANIFEST.write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps({'prepared': True, 'source': SOURCE, 'identities': len(identities), 'manifestSha256': digest(MANIFEST)}))



def identities(manifest):
    mismatches = []
    for identity in manifest['identities']:
        try:
            if digest(Path(identity['path'])) != identity['sha256']:
                mismatches.append(identity['path'])
        except OSError:
            mismatches.append(identity['path'])
    return mismatches


def kill_group(pid, sig):
    try:
        os.killpg(pid, sig)
    except ProcessLookupError:
        pass


def supervise(argv, environment, work_deadline, hard_deadline, record):
    child = None
    log = None
    try:
        log = (ARTIFACTS / 'allocation.log').open('wb')
        if time.monotonic() >= work_deadline:
            raise TimeoutError('Work deadline reached')
        child = subprocess.Popen(argv, cwd=ROOT, env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        record['pid'] = child.pid
        record['exit'] = child.wait(timeout=max(.001, work_deadline - time.monotonic()))
        record['reason'] = 'completed' if record['exit'] == 0 else 'failed'
    except BaseException as error:
        record['reason'] = type(error).__name__
    finally:
        try:
            if child is not None:
                kill_group(child.pid, signal.SIGTERM)
                try:
                    child.wait(timeout=max(.001, min(1, hard_deadline - time.monotonic())))
                except subprocess.TimeoutExpired:
                    kill_group(child.pid, signal.SIGKILL)
                    child.wait(timeout=max(.001, min(1, hard_deadline - time.monotonic())))
                kill_group(child.pid, signal.SIGKILL)
                record['exit'] = child.returncode
                record['directChildReaped'] = child.returncode is not None
                while True:
                    remaining = members(child.pid)
                    record['groupMembers'] = remaining
                    if not remaining:
                        record['ownedGroupAbsent'] = True
                        break
                    if time.monotonic() >= hard_deadline - 2:
                        raise TimeoutError('Owned group did not drain')
                    kill_group(child.pid, signal.SIGKILL)
                    time.sleep(.05)
        except BaseException as error:
            record['cleanupError'] = type(error).__name__
        finally:
            if log is not None:
                log.close()
                record['logClosed'] = log.closed
            else:
                record['logClosed'] = True
            if child is None:
                record['directChildReaped'] = True
                record['ownedGroupAbsent'] = True


def verify_only():
    manifest = json.loads(MANIFEST.read_text())
    mismatches = identities(manifest)
    assert not mismatches, mismatches
    assert os.getpid() in members(os.getpgid(os.getpid()))
    print(json.dumps({'verified': True, 'identities': len(manifest['identities']), 'manifestSha256': digest(MANIFEST),
                      'processInspectionPreflight': True, 'countersDisabled': True, 'rawInspected': False}))


def measure(approved_hash):
    started = time.monotonic()
    work_deadline, hard_deadline = started + 25, started + 30
    lifecycle = {'lease': 'M8-sch-compile-allocation-1', 'status': 1, 'beforeIdentities': False, 'afterIdentities': False,
                 'beforeManifestSha256': False, 'afterManifestSha256': False, 'sequence': [], 'rawInspected': False}
    manifest = None
    try:
        contents = MANIFEST.read_bytes()
        actual_hash = hashlib.sha256(contents).hexdigest()
        if actual_hash != approved_hash:
            raise ValueError('Manifest differs from durable grant')
        lifecycle['beforeManifestSha256'] = True
        manifest = json.loads(contents)
        command = [str(HOST), '--fx-version', '10.0.11', str(BIN / 'Xemele.Benchmarks.dll'), '--measure', str(ARTIFACTS / 'allocation.json')]
        if manifest['lease'] != lifecycle['lease'] or manifest['childCommand'] != command or manifest['maximumSeconds'] != 30 or manifest['workSeconds'] != 25 or manifest['cleanupSeconds'] != 5:
            raise ValueError('Command/lease/bounds mismatch')
        lifecycle['beforeMismatches'] = identities(manifest)
        if lifecycle['beforeMismatches']:
            raise ValueError('Prepared identities changed before measurement')
        lifecycle['beforeIdentities'] = True
        lifecycle['identityCount'] = len(manifest['identities'])
        if os.getpid() not in members(os.getpgid(os.getpid())):
            raise ValueError('Process permission preflight failed')
        environment = os.environ.copy()
        environment.update(manifest['environment'])
        record = {'pid': None, 'exit': None, 'reason': 'not-started', 'directChildReaped': False,
                  'ownedGroupAbsent': False, 'logClosed': False, 'groupMembers': []}
        lifecycle['sequence'].append(record)
        supervise(command, environment, work_deadline, hard_deadline, record)
        if record['exit'] == 0 and record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'] and record['reason'] == 'completed':
            lifecycle['status'] = 0
    except BaseException as error:
        lifecycle['error'] = type(error).__name__
    finally:
        try:
            # Verify the approved manifest and every identity again even on a
            # preflight/launch/wait/cleanup failure. No raw result is opened.
            lifecycle['afterManifestSha256'] = digest(MANIFEST) == approved_hash
            if manifest is None:
                manifest = json.loads(MANIFEST.read_text())
            lifecycle['afterMismatches'] = identities(manifest)
            lifecycle['afterIdentities'] = not lifecycle['afterMismatches']
        except BaseException as error:
            lifecycle['afterError'] = type(error).__name__
        if not lifecycle['afterIdentities'] or not lifecycle['afterManifestSha256']:
            lifecycle['status'] = 1
        lifecycle['elapsedSeconds'] = time.monotonic() - started
        if lifecycle['elapsedSeconds'] > 30:
            lifecycle['status'] = 1
            lifecycle['deadlineExceeded'] = True
        (ARTIFACTS / 'lifecycle.json').write_text(json.dumps(lifecycle, indent=2) + '\n')
        print(json.dumps(lifecycle), flush=True)
    raise SystemExit(lifecycle['status'])


parser = argparse.ArgumentParser()
parser.add_argument('--prepare', action='store_true')
parser.add_argument('--verify-only', action='store_true')
parser.add_argument('--granted')
arguments = parser.parse_args()
assert sum([arguments.prepare, arguments.verify_only, arguments.granted is not None]) == 1
if arguments.prepare:
    prepare()
elif arguments.verify_only:
    verify_only()
else:
    measure(arguments.granted)
