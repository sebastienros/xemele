"""Owned finite command families. No CLI, runtime work or counters on import.
Accepted supervisor cleanup/deadline bodies are preserved; the owned copy pins
the process-inventory executable. Every cooperative new-session launcher has
intent/publication/ACK in every ancestor, including root, before exec.
"""
import json
import os
import subprocess
import time
import uuid
from pathlib import Path
import supervision
import nested_ownership as nested

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'artifacts/M8-xsd-resolved-availability'


def clean(record):
    return (supervision.workload_succeeded(record)
            and record.get('nestedOwnership', {}).get('allOwnedGroupsAbsent') is True
            and not record.get('lateSuccess', False))


def publish_finite(path, value, hard_deadline):
    """Late candidates are quarantined, never left at an acceptance path."""
    path = Path(path)
    if time.monotonic() >= hard_deadline - 1:
        raise TimeoutError('Receipt reserve exhausted before publication')
    nested.durable(path, value)
    if time.monotonic() >= hard_deadline:
        quarantine = path.with_name(path.name + '.invalid-' + uuid.uuid4().hex)
        try:
            os.link(path, quarantine)
        finally:
            path.unlink(missing_ok=True)
        nested.durable(path.with_name(path.name + '.deadline-failure.json'),
                       {'completed': False, 'status': 'failed', 'reason': 'LatePublication', 'candidate': str(quarantine)})
        raise TimeoutError('Publication crossed hard deadline; candidate invalidated')
    return value


class Runner:
    def __init__(self, directory, maximum_seconds, environment=None, require_ancestor=True, counter_launcher=False, started=None, cleanup_reserve=30):
        self.directory = Path(directory).resolve()
        assert OUT.resolve() in self.directory.parents
        self.directory.mkdir(parents=True, exist_ok=False)
        self.started=time.monotonic() if started is None else started
        self.maximum=maximum_seconds;self.reserve=cleanup_reserve
        self.hard = min(self.started + maximum_seconds, float(os.environ.get('XEMELE_MISSING_HARD_DEADLINE', 'inf')))
        self.work = min(self.hard - cleanup_reserve, float(os.environ.get('XEMELE_MISSING_WORK_DEADLINE', 'inf')))  # root registry drain + durable receipt reserve
        self.registry = self.directory / 'root-registry'
        self.registry.mkdir(exist_ok=False)
        self.environment = dict(os.environ if environment is None else environment)
        ancestors = nested.registries(self.environment)
        assert ancestors or not require_ancestor, 'Launch through owned launch.py root controller'
        self.environment[nested.REGISTRIES] = json.dumps([str(p) for p in ancestors + [self.registry]])
        self.environment['PYTHONDONTWRITEBYTECODE'] = '1'
        self.environment['XEMELE_MISSING_HARD_DEADLINE'] = str(self.hard)
        self.environment['XEMELE_MISSING_WORK_DEADLINE'] = str(self.work)
        self.sequence = []
        self.stopped = False
        self.closed = False
        self.receipt = None
        self.counter_launcher = counter_launcher

    def __enter__(self):
        return self

    def run(self, argv, child_seconds=180, environment=None, check=True):
        assert not self.closed and not self.stopped, 'No next command after lifecycle failure'
        now = time.monotonic()
        if now >= self.work:
            self.stopped=True
            raise TimeoutError('Root work deadline exhausted before command')
        child_hard = min(now + child_seconds, self.hard - 2)
        child_work = min(self.work, child_hard - 10)
        if now >= child_work:
            self.stopped=True
            raise TimeoutError('Child cleanup reserve unavailable')
        supervision.ARTIFACTS = self.directory
        supervision.ROOT = ROOT
        env = self.environment.copy()
        if environment is not None:
            env.update(environment)
            # Child-specific values cannot discard ancestor ownership.
            env[nested.REGISTRIES] = self.environment[nested.REGISTRIES]
        import bindings as b
        try:
            b.review(self.environment['XEMELE_MISSING_REVIEW_SHA256'])
            b.verify_executable(argv,env)
            b.assert_prior_execution(self.directory)
            restore_before=b.restore_inputs(argv)
            restore_binding=b.preceding_restore(self.directory,argv,restore_before)
            restore_scope=b.restore_scope(argv)
            before, folders = b.execution_inputs(argv)
            from diagnostics import capture_inputs
            inputs=capture_inputs(self.directory,len(self.sequence),list(argv),env,restore_binding=restore_binding)
        except BaseException:
            self.stopped=True
            raise
        record = nested.supervise_family_command(supervision, list(argv), len(self.sequence), env, child_work, child_hard)
        record['argv'] = list(argv)
        record['cwd'] = str(ROOT)
        record['environmentSha256'] = b.identity_sha(env)
        record['beforeBinaryInputs'] = before
        record['beforeRestoreInputs']=restore_before
        record['restoreScope']=restore_scope
        record['precedingRestore']=restore_binding
        record['binaryFolders'] = folders
        record['inputReceipt']=inputs['path'];record['inputReceiptSha256']=inputs['sha256']
        record['childHardSeconds'] = child_seconds
        record['startedMonotonic']=now;record['workDeadline']=child_work;record['hardDeadline']=child_hard
        record['finishedMonotonic']=time.monotonic()
        record['lateSuccess'] = time.monotonic() >= child_hard - 2
        self.sequence.append(record)
        try:
            after, after_folders = b.execution_inputs(argv) if before else ({}, [])
            restore_after=b.restore_inputs(argv)
            record['afterRestoreInputs']=restore_after
            if restore_scope is not None:assert any(name.endswith('/project.assets.json') for name in restore_after),'Successful restore/build requires complete asset inventory'
            if argv[1:2]==['build']:assert restore_before==restore_after,'Restore-derived inputs changed during no-restore build'
            if before != after or folders != after_folders:
                record['reason'] = 'InputIdentityChanged'; self.stopped = True
            if before: b.record_execution(self.directory, len(self.sequence)-1, argv, before, after, folders)
            b.review(self.environment['XEMELE_MISSING_REVIEW_SHA256'])
        except BaseException as caught:
            record['reason'] = 'PostLaunchBindingFailure'; record['bindingError'] = type(caught).__name__; self.stopped = True
            raise
        finally:
            record['finishedMonotonic']=time.monotonic()
            record['lateSuccess']=time.monotonic()>=child_hard-2
            nested.durable(self.directory / f'lifecycle-{len(self.sequence)-1:03d}.json', record)
        if time.monotonic() >= child_hard or not clean(record):
            self.stopped = True
        if check and self.stopped:
            raise subprocess.CalledProcessError(record['exit'] if record['exit'] else 1, argv)
        return subprocess.CompletedProcess(argv, 0 if not self.stopped else record['exit'] or 1)

    def close(self, error=None):
        assert not self.closed, 'Close exactly once'
        self.closed = True
        cleanup = nested.drain(supervision, self.registry, self.hard)
        completed = (error is None and not self.stopped and bool(self.sequence)
                     and all(clean(row) for row in self.sequence)
                     and cleanup['allOwnedGroupsAbsent'] and time.monotonic() < self.hard - 1)
        self.receipt = dict(completed=completed, sequence=self.sequence, ownership=cleanup,
                            startedMonotonic=self.started,finishedMonotonic=time.monotonic(),maximumSeconds=self.maximum,
                            hardDeadline=self.hard,workDeadline=self.work,cleanupReserveSeconds=self.reserve,
                            error=error, counterLauncher=self.counter_launcher)
        try:
            publish_finite(self.directory / 'family-cleanup.json', self.receipt, self.hard)
        except TimeoutError:
            self.receipt['completed'] = False
            self.receipt['error'] = 'LatePublication'
            self.stopped = True
        return self.receipt

    def __exit__(self, kind, value, traceback):
        self.close(None if kind is None else kind.__name__)
        if kind is None and not self.receipt['completed']:
            raise RuntimeError('Owned family cleanup/deadline failure')
