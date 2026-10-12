"""Caller ownership for nested sessions; accepted supervisor bodies remain unchanged.

Every nested session publishes a durable group identity before executing workload.
Pre-launch intents make a missing publication a cleanup failure, never a clean claim.
All enclosing registries receive the identity; closing any registry prevents a late
launcher from executing its workload. Controllers drain registered groups finitely.
"""
import contextlib
import json
import os
import signal
import subprocess
import sys
import time
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'artifacts/M8-xsd-corpus'
REGISTRIES = 'XEMELE_CORPUS_OWNED_REGISTRIES'


def durable(path, value):
    with path.open('x') as stream:
        json.dump(value, stream, sort_keys=True)
        stream.flush()
        os.fsync(stream.fileno())
    fd = os.open(path.parent, os.O_RDONLY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def registries(environment):
    values = [Path(value).resolve() for value in json.loads(environment.get(REGISTRIES, '[]'))]
    assert all(OUT.resolve() in path.parents and path.is_dir() for path in values), 'owned registry required'
    return values


@contextlib.contextmanager
def track_launches(supervisor, environment, work_deadline=None):
    """Wrap only new-session workload launches; ps probes keep their normal Popen."""
    original = supervisor.subprocess.Popen
    def launch(argv, *args, **kwargs):
        owners = registries(kwargs.get('env') or environment)
        if not owners or kwargs.get('start_new_session') is not True:
            return original(argv, *args, **kwargs)
        token = uuid.uuid4().hex
        launch_deadline = min(time.monotonic() + 2, work_deadline) if work_deadline is not None else time.monotonic() + 2
        for owner in owners:
            assert not (owner / 'closed.json').exists(), 'owner already closing'
            durable(owner / ('intent-' + token + '.json'), {'token': token, 'argv': list(argv),
                    'parentPid': os.getpid(), 'launchDeadline': launch_deadline})
        wrapper = [sys.executable, str(Path(__file__).resolve()), '--launch', token] + list(argv)
        child = None
        try:
            child = original(wrapper, *args, **kwargs)
            deadline = launch_deadline
            while not all((owner / ('group-' + token + '.json')).exists() for owner in owners):
                if child.poll() is not None or time.monotonic() >= deadline or any((owner / 'closed.json').exists() for owner in owners):
                    os.killpg(child.pid, signal.SIGKILL)
                    child.wait(timeout=.5)
                    raise TimeoutError('Nested registration failed before ACK')
                time.sleep(.01)
            if time.monotonic() >= launch_deadline:
                raise TimeoutError('Nested ACK missed work deadline')
            for owner in owners:
                durable(owner / ('ack-' + token + '.json'), {'token': token, 'pid': child.pid})
            return child
        except BaseException:
            if child is not None:
                try:
                    os.killpg(child.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                child.wait(timeout=.5)
            for owner in owners:
                durable(owner / ('not-launched-' + token + '.json'), {'token': token})
            raise
    supervisor.subprocess.Popen = launch
    try:
        yield
    finally:
        supervisor.subprocess.Popen = original


def launch_workload(token, argv):
    owners = registries(os.environ)
    assert owners and os.getpid() == os.getpgrp(), 'launcher must own its new session'
    # Identity exists in EVERY ancestor registry before payload can start.
    for owner in owners:
        durable(owner / ('group-' + token + '.json'), {'token': token, 'pid': os.getpid(), 'pgid': os.getpgrp()})
    intent = json.loads((owners[-1] / ('intent-' + token + '.json')).read_text())
    while not all((owner / ('ack-' + token + '.json')).exists() for owner in owners):
        if (time.monotonic() >= intent['launchDeadline'] or os.getppid() != intent['parentPid'] or
                any((owner / 'closed.json').exists() for owner in owners)):
            return 125
        time.sleep(.01)
    if time.monotonic() >= intent['launchDeadline'] or os.getppid() != intent['parentPid'] or any((owner / 'closed.json').exists() for owner in owners):
        return 125
    for owner in owners:
        ack = json.loads((owner / ('ack-' + token + '.json')).read_text())
        assert ack['token'] == token and ack['pid'] == os.getpid()
    os.execvpe(argv[0], argv, os.environ.copy())
    return 125


def drain(supervisor, registry, hard_deadline):
    groups = set()
    termed = set()
    record = {'registry': str(registry), 'groups': [], 'pendingIntents': [], 'allOwnedGroupsAbsent': False}
    try:
        durable(registry / 'closed.json', {'controller': os.getpid()})
    except BaseException as error:
        record['closingError'] = type(error).__name__
    try:
        while True:
            intents = {p.stem.removeprefix('intent-') for p in registry.glob('intent-*.json')}
            cancelled = {p.stem.removeprefix('not-launched-') for p in registry.glob('not-launched-*.json')}
            published = set()
            for file in registry.glob('group-*.json'):
                try:
                    identity = json.loads(file.read_text())
                except (json.JSONDecodeError, OSError):
                    continue  # Keep its intent pending; still kill every other published group.
                assert identity['pid'] == identity['pgid'] and identity['pgid'] > 1
                assert identity['pgid'] != os.getpgrp(), 'cannot signal controller group'
                groups.add(identity['pgid'])
                published.add(identity['token'])
            pending = sorted(intents - published - cancelled)
            live = {}
            for group in sorted(groups):
                if group not in termed:
                    supervisor.kill_group(group, signal.SIGTERM)
                    termed.add(group)
                supervisor.kill_group(group, signal.SIGKILL)
            for group in sorted(groups):
                remaining = supervisor.members(group)
                if remaining:
                    live[str(group)] = remaining
            record.update(groups=sorted(groups), pendingIntents=pending, remainingGroups=live)
            if not pending and not live:
                record['allOwnedGroupsAbsent'] = 'closingError' not in record
                break
            if time.monotonic() >= hard_deadline - 2:
                raise TimeoutError('Nested ownership did not drain')
            time.sleep(.05)
    except BaseException as error:
        record['cleanupError'] = type(error).__name__
    finally:
        durable(registry / 'cleanup.json', record)
    return record


def supervise_family_command(supervisor, argv, index, environment, work_deadline, hard_deadline):
    registry = supervisor.ARTIFACTS / f'nested-{index:03d}'
    registry.mkdir(exist_ok=False)
    enclosing = registries(environment)
    child_environment = environment.copy()
    child_environment[REGISTRIES] = json.dumps([str(path) for path in enclosing + [registry]])
    record = None
    try:
        with track_launches(supervisor, child_environment, work_deadline):
            record = supervisor.supervise_command(argv, index, child_environment, work_deadline, min(hard_deadline, work_deadline + 4))
    finally:
        cleanup = drain(supervisor, registry, min(hard_deadline, time.monotonic() + 6))
        if record is not None:
            record['nestedOwnership'] = cleanup
            if not cleanup['allOwnedGroupsAbsent']:
                record['reason'] = 'NestedCleanupFailure'
    return record


@contextlib.contextmanager
def interruptions():
    """Turn TERM/INT of a controller into unwinding through its owned cleanup."""
    def stop(signum, frame):
        raise KeyboardInterrupt('controller signal ' + str(signum))
    previous = {sig: signal.signal(sig, stop) for sig in (signal.SIGTERM, signal.SIGINT)}
    try:
        yield
    finally:
        for sig, handler in previous.items():
            signal.signal(sig, handler)


if __name__ == '__main__':
    assert len(sys.argv) >= 4 and sys.argv[1] == '--launch'
    raise SystemExit(launch_workload(sys.argv[2], sys.argv[3:]))
