#!/usr/bin/env python3
"""Separate finite profile recipe; SOURCE ONLY until root source review and explicit execution release."""
import json
import os
import subprocess
import time
import uuid
from pathlib import Path
import run

SECONDS = 900
WORK_SECONDS = 300
CLEANUP_SECONDS = 10
BIN = run.OUT / 'profiles-bin/Release/net10.0'
DLL = BIN / 'XsdProfiles.dll'


def commands(directory):
    return [[run.D, 'build', 'tests/Xemele.Conformance.Tests/Xsd/Corpus.Profiles.csproj', '-c', 'Release',
             '--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11',
             '-p:UseSharedCompilation=false', '-p:NuGetAudit=false'],
            [run.D, '--fx-version', run.RUNTIME, str(DLL), str(run.ROOT), 'supplement-controls', str(directory / 'profile-controls.json')],
            [run.D, '--fx-version', run.RUNTIME, str(DLL), str(run.ROOT), 'supplements', str(directory / 'supplements.json')]]


def identities(include_binaries):
    values = run.input_identities(include_binaries=False)
    if include_binaries:
        assert BIN.is_dir() and DLL.is_file() and (BIN / 'Xemele.dll').is_file(), 'fresh profile build outputs required'
        for file in BIN.rglob('*'):
            if file.is_file():
                values[str(file.relative_to(run.ROOT))] = run.digest(file)
    return values


def inspect_outputs(directory):
    control = json.loads((directory / 'profile-controls.json').read_text())
    assert control['actual'] == 'passed' and control['originalPassed'] is None
    assert control['countAsOriginalPass'] is False and control['countAsGatePass'] is False
    facts = json.loads((run.CORPUS / 'application-supplemental-inputs.json').read_text())
    expected = [name for group in facts['xml_groups'] for name in group['supplemental_ids']] + [facts['late_hint']['supplemental_id']]
    rows = json.loads((directory / 'supplements.json').read_text())
    assert len(rows) == len({row['id'] for row in rows}) == 5 and [row['id'] for row in rows] == expected
    for index, row in enumerate(rows):
        assert row['engine'] == 'product' and row['classification'] == 'supplemental'
        assert row['kind'] == ('supplemental-schema' if index < 4 and index % 2 == 0 else 'supplemental-instance')
        assert row['expected'] == ('invalid' if index == 4 else 'valid')
        assert row['originalPassed'] is None and row['countAsOriginalPass'] is False and row['countAsGatePass'] is False
        assert row['complete'] is True and row['profilePassed'] is True
        assert row['actual'] == ('instance-invalid' if index == 4 else 'schema-valid' if index % 2 == 0 else 'instance-valid')
        if index == 4:
            assert 'XSD.HintOrder' in row['codes'] and row['originalObservation'] == facts['late_hint']['original_observation']
    return {'xmlApplicationRows': 4, 'lateHintRows': 1, 'originalPassCredit': 0, 'gatePassCredit': 0}


def main():
    hard = time.monotonic() + SECONDS
    work = hard - CLEANUP_SECONDS
    run.verified_dependencies()
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=run.ROOT, text=True, timeout=5).strip(), 'accepted source must be clean'
    source = run.revision()
    before = identities(False)
    assert not run.mismatches(before)
    directory = run.OUT / (source + '-supplements-' + uuid.uuid4().hex)
    directory.mkdir(parents=True, exist_ok=False)
    environment = os.environ.copy()
    environment['PYTHONDONTWRITEBYTECODE'] = '1'
    recipe = commands(directory)
    commands_file = directory / 'commands.json'
    tested_file = directory / 'tested-inputs.json'
    run.durable(commands_file, dict(sourceRevision=source, commands=recipe, campaignSeconds=SECONDS,
                                   childWorkSeconds=WORK_SECONDS, cleanupSeconds=CLEANUP_SECONDS,
                                   scope='separate two XML groups/four original IDs and only addB163.v; zero original/gate credit'))
    commands_sha = run.digest(commands_file)
    run.supervision.ARTIFACTS = directory
    sequence = []
    failure = None
    tested_sha = None
    outputs = None

    def verify():
        assert run.digest(commands_file) == commands_sha and json.loads(commands_file.read_text())['commands'] == recipe
        assert run.revision() == source and tested_sha is not None and run.digest(tested_file) == tested_sha
        tested = json.loads(tested_file.read_text())
        assert tested['sourceRevision'] == source and tested['commandsSha256'] == commands_sha
        assert identities(True) == tested['identities'] and not run.mismatches(tested['identities'])

    try:
        for index, command in enumerate(recipe):
            assert run.digest(commands_file) == commands_sha
            if index:
                verify()
            now = time.monotonic()
            child_hard = min(now + WORK_SECONDS + CLEANUP_SECONDS, hard)
            record = run.nested.supervise_family_command(run.supervision, command, index, environment,
                min(now + WORK_SECONDS, work, child_hard - CLEANUP_SECONDS), child_hard)
            record['argv'] = command
            sequence.append(record)
            run.durable(directory / f'lifecycle-{index:03d}.json', record)
            if not run.supervision.workload_succeeded(record) or not record['nestedOwnership']['allOwnedGroupsAbsent']:
                break
            if index == 0:
                assert identities(False) == before and not run.mismatches(before)
                tested = identities(True)
                run.durable(tested_file, dict(sourceRevision=source, commandsSha256=commands_sha, identities=tested))
                tested_sha = run.digest(tested_file)
                verify()
        if len(sequence) == 3 and all(run.supervision.workload_succeeded(r) for r in sequence):
            verify()
            outputs = inspect_outputs(directory)
    except BaseException as error:
        failure = type(error).__name__ + ': ' + str(error)
    finally:
        try:
            verify()
        except BaseException as error:
            failure = type(error).__name__ + ': ' + str(error)
        summary = dict(sourceRevision=source, currentRevision=run.revision(), sequence=sequence,
                       completed=failure is None and outputs is not None and len(sequence) == 3 and all(
                           run.supervision.workload_succeeded(r) and r['nestedOwnership']['allOwnedGroupsAbsent'] for r in sequence),
                       failure=failure, outputs=outputs, deadlineExceeded=time.monotonic() > hard,
                       commandsSha256=commands_sha, testedInputsSha256=tested_sha)
        run.durable(directory / 'supplements-cleanup.json', summary)
    print('SUPPLEMENT ARTIFACTS', directory, flush=True)
    return 0 if summary['completed'] and not summary['deadlineExceeded'] and source == summary['currentRevision'] else 1


if __name__ == '__main__':
    with run.nested.interruptions():
        raise SystemExit(main())
