"""Owned finite command families. No CLI, runtime work or counters on import.
Accepted supervisor functions are copied unchanged. Nested functions change only
owned output/environment constants; every cooperative new-session launcher has
intent/publication/ACK in every ancestor, including root, before exec.
"""
import json
import os
import subprocess
import time
from pathlib import Path
import supervision
import nested_ownership as nested

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'artifacts/M8-xsd-import-visibility'


def clean(record):
    return (supervision.workload_succeeded(record)
            and record.get('nestedOwnership', {}).get('allOwnedGroupsAbsent') is True
            and not record.get('lateSuccess', False))


class Runner:
    def __init__(self, directory, maximum_seconds, environment=None, require_ancestor=True, counter_launcher=False, started=None):
        self.directory = Path(directory).resolve()
        assert OUT.resolve() in self.directory.parents
        self.directory.mkdir(parents=True, exist_ok=False)
        self.hard = (time.monotonic() if started is None else started) + maximum_seconds
        self.work = self.hard - 20  # root registry drain + durable receipt reserve
        self.registry = self.directory / 'root-registry'
        self.registry.mkdir(exist_ok=False)
        self.environment = dict(os.environ if environment is None else environment)
        ancestors = nested.registries(self.environment)
        assert ancestors or not require_ancestor, 'Launch through owned launch.py root controller'
        self.environment[nested.REGISTRIES] = json.dumps([str(p) for p in ancestors + [self.registry]])
        self.environment['PYTHONDONTWRITEBYTECODE'] = '1'
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
        assert now < self.work, 'Root work deadline exhausted before command'
        child_hard = min(now + child_seconds, self.hard - 2)
        child_work = min(self.work, child_hard - 10)
        assert now < child_work, 'Child cleanup reserve unavailable'
        supervision.ARTIFACTS = self.directory
        env = self.environment.copy()
        if environment is not None:
            env.update(environment)
            # Child-specific values cannot discard ancestor ownership.
            env[nested.REGISTRIES] = self.environment[nested.REGISTRIES]
        record = nested.supervise_family_command(supervision, list(argv), len(self.sequence), env, child_work, child_hard)
        record['argv'] = list(argv)
        record['childHardSeconds'] = child_seconds
        record['lateSuccess'] = time.monotonic() >= child_hard - 2
        self.sequence.append(record)
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
                            error=error, counterLauncher=self.counter_launcher)
        nested.durable(self.directory / 'family-cleanup.json', self.receipt)
        if time.monotonic() >= self.hard:
            self.receipt['completed'] = False
            nested.durable(self.directory / 'late-publication-failure.json', self.receipt)
        return self.receipt

    def __exit__(self, kind, value, traceback):
        self.close(None if kind is None else kind.__name__)
        if kind is None and not self.receipt['completed']:
            raise RuntimeError('Owned family cleanup/deadline failure')
