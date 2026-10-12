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
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[3]
ARTIFACTS = ROOT / 'artifacts/M8-sch-report'
HOST = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
BIN = ROOT / 'benchmarks/Xemele.Benchmarks/M8-sch-report/bin/Release/net10.0'
SOURCE = 'f6db4e76dca6c57ef97f11149348edc3a6a4272c'
MANIFEST = ROOT / 'docs/reports/M8-sch-report/preparation.json'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def members(group):
    listing = subprocess.check_output(['ps', '-axo', 'pid,pgid'], text=True, timeout=2)
    return [int(pid) for pid, pgid in (line.split() for line in listing.splitlines()[1:]) if int(pgid) == group]


def prepare():
    # Exercise actual elevated TERM/KILL/reap/group/log cleanup with the exact
    # prebuilt child in counter-disabled mode before any grant request.
    preflight = {'pid': None, 'exit': None, 'reason': 'not-started', 'directChildReaped': False,
                 'ownedGroupAbsent': False, 'logClosed': False, 'groupMembers': []}
    environment = os.environ.copy()
    environment.update({'DOTNET_TieredCompilation': '0', 'DOTNET_gcServer': '0', 'DOTNET_GCConcurrent': '0',
                        'XEMELE_SCH_REPORT_REVISION': SOURCE})
    now = time.monotonic()
    supervise([str(HOST), '--fx-version', '10.0.11', str(BIN / 'Xemele.Benchmarks.dll'), '--control',
               str(ARTIFACTS / 'preflight-control.json')], environment, now + 20, now + 30, preflight, 'preflight-control.log')
    assert preflight['exit'] == 0 and preflight['reason'] == 'completed' and preflight['directChildReaped'] and preflight['ownedGroupAbsent'] and preflight['logClosed'], preflight
    preflight['countersDisabled'] = True
    (ARTIFACTS / 'preflight-lifecycle.json').write_text(json.dumps(preflight, indent=2) + '\n')
    control = json.loads((ARTIFACTS / 'control.json').read_text())
    assert control['mode'] == '--control' and control['sourceRevision'] == SOURCE
    assert len(control['rows']) == 13 and all(row['bytes'] is None for row in control['rows'])
    assert all(row.get('consumed', 32) == 32 for row in control['rows'])
    assert control['retainedApproximateBytes'] is None and control['processWorkingSet'] is None
    # Elevated process-inspection preflight; no command text or unrelated processes
    # are emitted. This must succeed before requesting the coordinator's lease.
    inspected = len(subprocess.check_output(['ps', '-axo', 'pid,pgid'], text=True, timeout=2).splitlines()) - 1
    files = [HOST]
    files += sorted(path for path in (HOST.parent / 'shared/Microsoft.NETCore.App/10.0.11').iterdir() if path.is_file())
    files += sorted(path for path in (HOST.parent / 'host/fxr/10.0.11').iterdir() if path.is_file())
    files += sorted(path for path in BIN.iterdir() if path.is_file())
    tracked = subprocess.check_output(['git', 'ls-files', 'src/Xemele', 'Directory.Build.props', 'Directory.Packages.props', 'global.json',
                                      'benchmarks/Xemele.Benchmarks/M8-sch-report', 'tests/Xemele.Tests/M8-sch-report', 'tests/fixtures/M8-sch-report',
                                      'samples/M8-sch-report', 'docs/api/M8-sch-report.txt', 'docs/reports/M8-sch-report/ApiCheck',
                                      'docs/reports/M8-sch-report/XmlOnly', 'docs/reports/M8-sch-report/checks.py', 'docs/reports/M8-sch-report/lease.py'], cwd=ROOT, text=True)
    files += [ROOT / item for item in tracked.splitlines() if item.endswith(('.cs', '.csproj', '.props', '.json', '.py', '.txt', '.xml'))]
    files += [ARTIFACTS / 'control.json', ARTIFACTS / 'preflight-control.json', ARTIFACTS / 'preflight-lifecycle.json', ROOT / 'docs/reports/M8-sch-report/lease.py']
    identities = [{'path': str(path), 'sha256': digest(path)} for path in sorted(set(files))]
    manifest = {
        'sourceRevision': SOURCE,
        'preparationRevision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'cwd': str(ROOT), 'runtime': '10.0.11', 'sdk': '10.0.400', 'maximumSeconds': 30,
        'lease': 'M8-sch-report-allocation-1', 'workSeconds': 20, 'cleanupSeconds': 10,
        'command': ['python3', 'docs/reports/M8-sch-report/lease.py', '--granted', '<durably-approved-manifest-sha256>', '<latest-start-UTC-ISO>', '<absolute-expiry-UTC-ISO>'],
        'childCommand': [str(HOST), '--fx-version', '10.0.11', str(BIN / 'Xemele.Benchmarks.dll'), '--measure', str(ARTIFACTS / 'allocation.json')],
        'environment': {'DOTNET_TieredCompilation': '0', 'DOTNET_gcServer': '0', 'DOTNET_GCConcurrent': '0', 'XEMELE_SCH_REPORT_REVISION': SOURCE},
        'controlRoutes': 13, 'processInspectionPreflight': {'passed': True, 'processCount': inspected, 'cleanup': preflight},
        'identities': identities,
        'scope': 'One prebuilt synchronous allocation-only probe: cold string/UTF8 output, positive owned control, four paired string/UTF8 routes (short, long multilingual, dense escaping,128events), invalid XML scalar string/stream, retained16long strings/RSS. Caller digest sink reused; destination storage excluded. No borrowed-zero claim, BDN/timing campaign, builds, tests, or raw inspection under lease.'
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


def supervise(argv, environment, work_deadline, hard_deadline, record, log_name="allocation.log"):
    child = None
    log = None
    try:
        log = (ARTIFACTS / log_name).open('xb')
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


def measure(approved_hash, latest_start_text, expiry_text):
    # A late start cannot extend the root's absolute UTC bounds. Existing raw
    # output is never overwritten, including a prior failed lifecycle.
    now = datetime.now(timezone.utc)
    latest_start = datetime.fromisoformat(latest_start_text.replace('Z', '+00:00'))
    expiry = datetime.fromisoformat(expiry_text.replace('Z', '+00:00'))
    if latest_start.tzinfo is None or expiry.tzinfo is None or now > latest_start or expiry <= now:
        raise SystemExit('Lease start deadline/expiry refused; no child or measurement started.')
    remaining = min(30, (expiry - now).total_seconds())
    if remaining <= 10:
        raise SystemExit('Lease cleanup reserve exhausted; no child started.')
    if any((ARTIFACTS / name).exists() for name in ['allocation.json', 'allocation.log', 'lifecycle.json', 'release.json']):
        raise SystemExit('Preserved raw/lifecycle output exists; no replacement or retry.')
    started = time.monotonic()
    hard_deadline = started + remaining
    work_deadline = hard_deadline - 10
    lifecycle = {'lease': 'M8-sch-report-allocation-1', 'status': 1, 'beforeIdentities': False, 'afterIdentities': False,
                 'beforeManifestSha256': False, 'afterManifestSha256': False, 'sequence': [], 'rawInspected': False,
                 'latestStart': latest_start_text, 'absoluteExpiry': expiry_text, 'maximumRemainingSeconds': remaining}
    manifest = None
    try:
        contents = MANIFEST.read_bytes()
        actual_hash = hashlib.sha256(contents).hexdigest()
        if actual_hash != approved_hash:
            raise ValueError('Manifest differs from durable grant')
        lifecycle['beforeManifestSha256'] = True
        manifest = json.loads(contents)
        command = [str(HOST), '--fx-version', '10.0.11', str(BIN / 'Xemele.Benchmarks.dll'), '--measure', str(ARTIFACTS / 'allocation.json')]
        if manifest['lease'] != lifecycle['lease'] or manifest['childCommand'] != command or manifest['maximumSeconds'] != 30 or manifest['workSeconds'] != 20 or manifest['cleanupSeconds'] != 10:
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
        if datetime.now(timezone.utc) > latest_start:
            raise TimeoutError('Latest start elapsed during preflight; no child started')
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
        if lifecycle['elapsedSeconds'] > remaining:
            lifecycle['status'] = 1
            lifecycle['deadlineExceeded'] = True
        # Durable lifecycle and local cleanup RELEASE precede any raw inspection.
        # The coordinator separately records host-wide release/assessment.
        for name, data in [('lifecycle.json', lifecycle), ('release.json', {
            'lease': lifecycle['lease'], 'releasedAt': datetime.now(timezone.utc).isoformat(),
            'status': lifecycle['status'], 'rawInspected': False,
            'sequence': lifecycle['sequence'], 'identityCount': lifecycle.get('identityCount'),
            'beforeIdentities': lifecycle['beforeIdentities'], 'afterIdentities': lifecycle['afterIdentities'],
            'beforeManifestSha256': lifecycle['beforeManifestSha256'], 'afterManifestSha256': lifecycle['afterManifestSha256']})]:
            with (ARTIFACTS / name).open('x') as output:
                output.write(json.dumps(data, indent=2) + '\n'); output.flush(); os.fsync(output.fileno())
        print(json.dumps(lifecycle), flush=True)
    raise SystemExit(lifecycle['status'])


parser = argparse.ArgumentParser()
parser.add_argument('--prepare', action='store_true')
parser.add_argument('--verify-only', action='store_true')
parser.add_argument('--granted', nargs=3, metavar=('MANIFEST_SHA256', 'LATEST_START_UTC', 'ABSOLUTE_EXPIRY_UTC'))
arguments = parser.parse_args()
assert sum([arguments.prepare, arguments.verify_only, arguments.granted is not None]) == 1
if arguments.prepare:
    prepare()
elif arguments.verify_only:
    verify_only()
else:
    measure(*arguments.granted)
