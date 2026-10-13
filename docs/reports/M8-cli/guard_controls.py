#!/usr/bin/env python3
"""Small bounded failure-injection controls for new phase guards/receipts, using real accepted cleanup."""
import sys
sys.dont_write_bytecode = True
import contextlib
import copy
import importlib.util
import io
import json
from pathlib import Path
import shutil
import time
from unittest import mock
import uuid

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('schema_cli_guard_subject', Path(__file__).with_name('checks.py'))
subject = importlib.util.module_from_spec(spec); spec.loader.exec_module(subject)
END = time.monotonic()+45
CASES = 0

def exercise(name, phase='prepare', change=None, fail_inspection=False, expected=False):
    global CASES
    assert time.monotonic() < END, 'guard total work deadline'
    directory = ROOT/'artifacts/M8-cli'/('guard-'+uuid.uuid4().hex); directory.mkdir(parents=True)
    inputs = {'source': {'sha256': 'original'}, 'runtime': {'sha256': 'runtime'}, 'environment': subject.ENV.copy(), 'revision': 'guard-input'}
    after = copy.deepcopy(inputs)
    if change is not None: change(after)
    calls = 0
    def identity():
        nonlocal calls
        calls += 1
        return copy.deepcopy(inputs if calls == 1 else after)
    binary = {'Tool': {'files': 2, 'sha256': 'complete-output'}}
    prepared = {'passed': True, 'inputs': copy.deepcopy(inputs), 'binaries': copy.deepcopy(binary)}
    if name == 'stale-prepared-binary': prepared['binaries']['Tool']['sha256'] = 'changed-dependency'
    (directory/'prepared.json').write_text(json.dumps(prepared))
    original_module = subject.module
    def module(module_name, path):
        if module_name == 'schema_cli_nested': path = Path(__file__).with_name('nested_ownership.py')
        return original_module(module_name, path)
    original_read = Path.read_text
    def read(path, *args, **kwargs):
        if fail_inspection and path.name.startswith('command-') and path.suffix == '.log': raise OSError('guard output inspection refusal')
        return original_read(path, *args, **kwargs)
    # A real small registered child gives terminal/raw evidence. It never loads a validator or counter.
    workload = [([sys.executable, '-c', "print('PASS bounded guard workload')"], min(3, END-time.monotonic()-10))]
    capture = io.StringIO()
    try:
        with mock.patch.object(subject, 'HERE', directory), mock.patch.object(subject, 'input_identities', identity), mock.patch.object(subject, 'binaries', lambda: copy.deepcopy(binary)), \
             mock.patch.object(subject, 'commands', lambda _: workload), mock.patch.object(subject, 'module', module), mock.patch.object(sys, 'argv', ['checks.py', phase]), \
             mock.patch.object(Path, 'read_text', read), contextlib.redirect_stdout(capture):
            try: subject.main()
            except SystemExit as outcome: assert outcome.code == (0 if expected else 1), (name, outcome.code, capture.getvalue())
        terminal = json.loads((directory/('prepared.json' if phase == 'prepare' else 'results.json')).read_text())
        assert terminal['passed'] is expected, (name, terminal)
        assert terminal['allOwnedGroupsAbsent'] and terminal['allDirectChildrenReaped'], (name, terminal)
        assert list(directory.glob('*-final.receipt.json')), 'durable final receipt required'
        if change is not None or phase == 'check': assert terminal['bindingFailure'], (name, terminal)
        if fail_inspection:
            assert not terminal['rawDeleted'] and not terminal['logsInspected'] and terminal['bindingFailure'], terminal
            retained = ROOT/'artifacts/M8-cli'/Path(terminal['terminalCleanup'][0]['registry']).parent.name
            assert retained.is_dir(), 'failed inspection must retain raw output'
            # The test itself now inspects the refused log AFTER the real group is absent, then deletes it.
            assert 'PASS bounded guard workload' in (retained/'command-0.log').read_text()
            shutil.rmtree(retained)
        elif expected or change is not None: assert terminal['rawDeleted'], terminal
        CASES += 1
    finally:
        shutil.rmtree(directory)

exercise('complete', expected=True)
exercise('source-drift', change=lambda x: x['source'].update(sha256='modified'))
exercise('runtime-drift', change=lambda x: x['runtime'].update(sha256='modified'))
exercise('environment-drift', change=lambda x: x['environment'].update(DOTNET_EnableDiagnostics='1'))
exercise('stale-prepared-binary', phase='check')
exercise('inspection-refusal', fail_inspection=True)
assert time.monotonic() < END, 'guard total work deadline'
print(f'PASS phase guards: {CASES} bounded success/failure controls; durable refusals, real registered child cleanup, raw retention/inspection/deletion')
