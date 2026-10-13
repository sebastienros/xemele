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
spec = importlib.util.spec_from_file_location('gate_guard_subject', Path(__file__).with_name('checks.py'))
subject = importlib.util.module_from_spec(spec); spec.loader.exec_module(subject)
END = time.monotonic()+45
CASES = 0
assert sys.argv[1:] in [['--synthetic'],['--archive']], 'Finite synthetic|archive control phase required'

def exercise(name, phase='prepare', change=None, fail_inspection=False, expected=False):
    global CASES
    assert time.monotonic() < END, 'guard total work deadline'
    directory = ROOT/'artifacts/M8-gate'/('guard-'+uuid.uuid4().hex); directory.mkdir(parents=True)
    inputs = {'source': {'sha256': 'original'}, 'authority':{'pin':'original'}, 'runtime': {'sha256': 'runtime'}, 'environment': subject.ENV.copy(), 'revision': 'guard-input'}
    after = copy.deepcopy(inputs)
    if change is not None: change(after)
    calls = 0
    def identity():
        nonlocal calls
        calls += 1
        return copy.deepcopy(inputs if calls == 1 else after)
    binary = {'Tool': {'files': 2, 'sha256': 'complete-output'}}
    binary_calls = 0
    def binary_identity():
        nonlocal binary_calls
        binary_calls += 1
        result = copy.deepcopy(binary)
        if name == 'post-binary-drift' and binary_calls > 1: result['Tool']['sha256'] = 'dependency-modified-during-phase'
        return result
    prepared = {'passed': True,'rawDeleted':True,'deadlineExceeded':False, 'inputs': copy.deepcopy(inputs), 'binaries': copy.deepcopy(binary)}
    if name == 'stale-prepared-binary': prepared['binaries']['Tool']['sha256'] = 'changed-dependency'
    (directory/'prepared.json').write_text(json.dumps(prepared))
    # Synthetic observer data is confined to failure-injection controls, never product preparation.
    outer_path = directory/'outer.receipt.json'
    outer_path.write_text(json.dumps({'exit': 0, 'reason': 'completed', 'directChildReaped': True, 'ownedGroupAbsent': True,
        'logClosed': True, 'nestedOwnership': {'allOwnedGroupsAbsent': True, 'pendingIntents': [], 'remainingGroups': {}},
        'deadlineExceeded': False, 'inputsMatched': True, 'logsInspected': True, 'rawDeleted': True}))
    certificate = directory/'root-certificate.json'
    certificate.write_text(json.dumps({'issuer': 'M8-root-coordinator', 'preparedSha256': subject.sha(directory/'prepared.json'),
        'outerReceiptPath': str(outer_path), 'outerReceiptSha256': subject.sha(outer_path)}))
    original_module = subject.module
    def module(module_name, path):
        if module_name == 'gate_nested': path = Path(__file__).with_name('nested_ownership.py')
        return original_module(module_name, path)
    original_read = Path.read_text
    def read(path, *args, **kwargs):
        if path == subject.HERE/'source-packet.json': return json.dumps({'sourceClosure':inputs['source']})
        if fail_inspection and path.name.startswith('command-') and path.suffix == '.log': raise OSError('guard output inspection refusal')
        return original_read(path, *args, **kwargs)
    # A real small registered child gives terminal/raw evidence. It never loads a validator or counter.
    workload = [([sys.executable, '-c', "print('PASS bounded guard workload')"], min(3, END-time.monotonic()-10))]
    capture = io.StringIO()
    clock_calls = 0
    def expired_clock():
        nonlocal clock_calls
        clock_calls += 1
        return 1 if clock_calls == 1 else 20000
    original_publish = subject.publish_phase
    publication_calls = 0
    late_publication = False
    original_clock = time.monotonic
    def publish(target, payload, identifier):
        nonlocal publication_calls, late_publication
        publication_calls += 1
        original_publish(target, payload, identifier)
        if publication_calls == 1:
            if name == 'late-final-publication': late_publication = True
            if name == 'interrupted-after-replace': raise KeyboardInterrupt('guard interruption after final replace/fsync')
    def publication_clock(): return original_clock()+1000000 if late_publication else original_clock()
    try:
        with contextlib.ExitStack() as controls, mock.patch.object(subject, 'PHASES', {'prepare':{'work':5,'reserve':15},'check':{'work':5,'reserve':15}}), mock.patch.object(subject, 'STATE', directory), mock.patch.object(subject, 'input_identities', identity), mock.patch.object(subject, 'artifact_snapshot', lambda: {}), mock.patch.object(subject, 'source_identity', lambda: inputs['source']), mock.patch.object(subject, 'authority_identity', lambda: inputs['authority']), mock.patch.object(subject, 'binaries', binary_identity), \
             mock.patch.object(subject, 'commands', lambda _: workload), mock.patch.object(subject, 'module', module), mock.patch.object(subject, 'publish_phase', publish), mock.patch.object(sys, 'argv', ['checks.py', phase]+([str(certificate)] if phase == 'check' else [])), \
             mock.patch.object(Path, 'read_text', read), contextlib.redirect_stdout(capture):
            if name == 'expired-total': controls.enter_context(mock.patch.object(subject.time, 'monotonic', expired_clock))
            if name == 'late-final-publication': controls.enter_context(mock.patch.object(subject.time, 'monotonic', publication_clock))
            try: subject.main()
            except SystemExit as outcome: assert outcome.code == (0 if expected else 1), (name, outcome.code, capture.getvalue())
        terminal = json.loads((directory/('prepared.json' if phase == 'prepare' else 'check.json')).read_text())
        assert terminal['passed'] is expected, (name, terminal)
        assert terminal['allOwnedGroupsAbsent'] and terminal['allDirectChildrenReaped'], (name, terminal)
        assert list(directory.glob('*-final.receipt.json')), 'durable final receipt required'
        if change is not None or phase == 'check': assert terminal['bindingFailure'], (name, terminal)
        if name in ['late-final-publication', 'interrupted-after-replace']:
            assert terminal['publicationFailure'] and publication_calls == 2 and not terminal['passed'] and terminal['rawDeleted'], terminal
            assert list(directory.glob('*-publication-failure.receipt.json')), 'durable publication failure'
            if name == 'late-final-publication': assert terminal['deadlineExceeded'], terminal
        if fail_inspection:
            assert not terminal['rawDeleted'] and not terminal['logsInspected'] and terminal['bindingFailure'], terminal
            retained = ROOT/'artifacts/M8-gate'/Path(terminal['terminalCleanup'][0]['registry']).parent.name
            assert retained.is_dir(), 'failed inspection must retain raw output'
            # The test itself now inspects the refused log AFTER the real group is absent, then deletes it.
            assert 'PASS bounded guard workload' in (retained/'command-0.log').read_text()
            shutil.rmtree(retained)
        elif name == 'expired-total':
            assert terminal['deadlineExceeded'] and not terminal['rawDeleted'] and not terminal['commands'], terminal
            # No workload launched; verify then remove the intentionally retained empty raw directory.
            retained = ROOT/'artifacts/M8-gate'/next(directory.glob('*-final.receipt.json')).name.removesuffix('-final.receipt.json')
            assert retained.is_dir() and not list(retained.iterdir()), terminal
            shutil.rmtree(retained)
        elif expected or change is not None: assert terminal['rawDeleted'], terminal
        CASES += 1
    finally:
        shutil.rmtree(directory)

if sys.argv[1:] == ['--synthetic']:
    exercise('complete', expected=True)
    exercise('source-drift', change=lambda x: x['source'].update(sha256='modified'))
    exercise('runtime-drift', change=lambda x: x['runtime'].update(sha256='modified'))
    exercise('environment-drift', change=lambda x: x['environment'].update(DOTNET_EnableDiagnostics='1'))
    exercise('stale-prepared-binary', phase='check')
    exercise('post-binary-drift', phase='check')
    exercise('inspection-refusal', fail_inspection=True)
    exercise('expired-total')
    exercise('late-final-publication')
    exercise('interrupted-after-replace')
if sys.argv[1:] == ['--archive']:
    # Actual prepared package bytes: valid identity succeeds; mutation after freeze must refuse.
    import identities as archive_subject
    archive_directory=ROOT/'artifacts/M8-gate'/('archive-guard-'+uuid.uuid4().hex)
    try:
        (archive_directory/'feed').mkdir(parents=True)
        shutil.copyfile(archive_subject.WORK/'package-identity.json',archive_directory/'package-identity.json')
        archive=archive_directory/'feed/Xemele.0.8.0-integration.nupkg'
        shutil.copyfile(archive_subject.WORK/'feed/Xemele.0.8.0-integration.nupkg',archive)
        with mock.patch.object(archive_subject,'WORK',archive_directory):
            archive_subject.package_identity(); CASES += 1
            with archive.open('ab') as changed:changed.write(b'actual post-freeze mutation control')
            try:archive_subject.package_identity()
            except AssertionError as refusal:assert 'Actual nupkg archive mutated after freeze' in str(refusal);CASES += 1
            else:raise AssertionError('Mutated package archive was accepted')
            (archive_directory/'package-identity.json').unlink()
            try:archive_subject.package_identity()
            except FileNotFoundError:CASES += 1
            else:raise AssertionError('Missing frozen package record was accepted')
    finally:
        shutil.rmtree(archive_directory)
assert time.monotonic() < END, 'guard total work deadline'
print(f'PASS phase guards: {CASES} bounded success/failure controls; durable refusals, real registered child cleanup, raw retention/inspection/deletion')
