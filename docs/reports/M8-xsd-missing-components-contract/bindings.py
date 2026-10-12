"""Owned immutable input/command binding; no execution at import."""
import hashlib
import json
import time
from pathlib import Path


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1048576), b''):
            value.update(block)
    return value.hexdigest()


def verify(path, expected_sha, deadline):
    if time.monotonic() >= deadline or digest(path) != expected_sha:
        raise AssertionError('bound plan identity/deadline changed')
    plan = json.loads(Path(path).read_text())
    for name, identity in plan['inputs'].items():
        if time.monotonic() >= deadline or digest(name) != identity:
            raise AssertionError('bound source/project/config/binary changed: ' + name)
    if time.monotonic() >= deadline:
        raise TimeoutError('binding verification crossed deadline')
    return plan
