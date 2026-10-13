#!/usr/bin/env python3
"""Proposed bounded preparation controls; SOURCE only until explicitly accepted."""
import os
import subprocess
import time
import uuid
import run


def main():
    hard = time.monotonic() + 1800
    work = hard - run.CLEANUP_SECONDS
    run.verified_dependencies()
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=run.ROOT, text=True, timeout=5).strip(), 'accepted source must be clean'
    source = run.revision()
    before = run.input_identities(include_binaries=False)
    assert not run.mismatches(before)
    directory = run.OUT / (source + '-controls-' + uuid.uuid4().hex)
    directory.mkdir(parents=True, exist_ok=False)
    environment = os.environ.copy()
    environment['PYTHONDONTWRITEBYTECODE'] = '1'
    commands = run.preparation_commands()
    commands_file = directory / 'commands.json'
    tested_file = directory / 'tested-inputs.json'
    run.durable(commands_file, {'sourceRevision': source, 'commands': commands,
                               'campaignSeconds': 1800, 'childWorkSeconds': 300, 'cleanupSeconds': 10})
    commands_sha = run.digest(commands_file)
    run.supervision.ARTIFACTS = directory
    sequence = []
    binding_failure = None
    tested_sha = None
    try:
        for index, command in enumerate(commands):
            assert run.digest(commands_file) == commands_sha
            if index:
                run.verify_preparation_bindings(commands_file, tested_file, tested_sha)
            now = time.monotonic()
            child_hard = min(now + 310, hard)
            record = run.nested.supervise_family_command(run.supervision, command, index, environment,
                min(now + 300, work, child_hard - 10), child_hard)
            record['argv'] = command
            sequence.append(record)
            run.durable(directory / f'lifecycle-{index:03d}.json', record)
            if not run.supervision.workload_succeeded(record) or not record['nestedOwnership']['allOwnedGroupsAbsent']:
                break
            if index == 0:
                # Build may change binaries, never the source/runtime that was presented for compilation.
                assert run.input_identities(include_binaries=False) == before and not run.mismatches(before)
                identities = run.input_identities()
                assert not run.mismatches(identities)
                run.durable(tested_file, {'sourceRevision': source, 'commandsSha256': commands_sha, 'identities': identities})
                tested_sha = run.digest(tested_file)
                run.verify_preparation_bindings(commands_file, tested_file, tested_sha)
    except BaseException as error:
        binding_failure = type(error).__name__ + ': ' + str(error)
    finally:
        try:
            assert run.digest(commands_file) == commands_sha
            run.verify_preparation_bindings(commands_file, tested_file, tested_sha)
        except BaseException as error:
            binding_failure = type(error).__name__ + ': ' + str(error)
        summary = dict(sourceRevision=source, currentRevision=run.revision(), sequence=sequence,
                       completed=binding_failure is None and len(sequence) == len(commands) and all(
                           run.supervision.workload_succeeded(r) and r['nestedOwnership']['allOwnedGroupsAbsent'] for r in sequence),
                       bindingFailure=binding_failure, deadlineExceeded=time.monotonic() > hard,
                       commandsSha256=commands_sha,
                       testedInputsSha256=tested_sha)
        run.durable(directory / 'controls-cleanup.json', summary)
    print('CONTROLS ARTIFACTS', directory, flush=True)
    return 0 if summary['completed'] and not summary['deadlineExceeded'] and summary['sourceRevision'] == summary['currentRevision'] else 1


if __name__ == '__main__':
    with run.nested.interruptions():
        raise SystemExit(main())
