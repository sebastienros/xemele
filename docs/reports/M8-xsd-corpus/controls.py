#!/usr/bin/env python3
"""Proposed bounded preparation controls; SOURCE only until explicitly accepted."""
import os
import sys
import subprocess
import time
import uuid
from pathlib import Path
import run


def main():
    run.verified_dependencies()
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=run.ROOT, text=True, timeout=5).strip(), 'accepted source must be clean'
    source = run.revision()
    directory = run.OUT / (source + '-controls-' + uuid.uuid4().hex)
    directory.mkdir(parents=True, exist_ok=False)
    environment = os.environ.copy()
    environment['PYTHONDONTWRITEBYTECODE'] = '1'
    build = [run.D, 'build', 'tests/Xemele.Conformance.Tests/Xsd/Corpus.Runner.csproj', '-c', 'Release',
             '--no-restore', '--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11',
             '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
    commands = [build,
                [run.D, '--fx-version', run.RUNTIME, str(run.DLL), '--controls'],
                [run.D, '--fx-version', run.RUNTIME, str(run.DLL), '--hint-profile-controls', str(run.CORPUS / 'upstream')],
                [run.D, '--fx-version', run.RUNTIME, str(run.DLL), '--optional-profile-controls', str(run.CORPUS / 'upstream')]]
    for script in ['tests/corpora/xsd/import.py', 'tests/corpora/xsd/test_import.py',
                   'docs/reports/M8-xsd-corpus/test_supervisor.py', 'docs/reports/M8-xsd-corpus/test_dispositions.py',
                   'docs/reports/M8-xsd-corpus/test_approved_dispositions.py',
                   'docs/reports/M8-xsd-corpus/check-optional-inputs.py']:
        commands.append([sys.executable, script] + (['--check'] if script.endswith('/import.py') else []))
    # Freeze the exact preparation commands and source before launching; no counters or original full campaign here.
    run.durable(directory / 'commands.json', {'sourceRevision': source, 'commands': commands,
                                             'campaignSeconds': 1800, 'childWorkSeconds': 300, 'cleanupSeconds': 10})
    hard = time.monotonic() + 1800
    work = hard - run.CLEANUP_SECONDS
    run.supervision.ARTIFACTS = directory
    sequence = []
    try:
        for index, command in enumerate(commands):
            now = time.monotonic()
            child_hard = min(now + 310, hard)
            record = run.supervision.supervise_command(command, index, environment, min(now + 300, work, child_hard - 10), child_hard)
            sequence.append(record)
            run.durable(directory / f'lifecycle-{index:03d}.json', record)
            if not run.supervision.workload_succeeded(record):
                break
    finally:
        summary = dict(sourceRevision=source, currentRevision=run.revision(), sequence=sequence,
                       completed=len(sequence) == len(commands) and all(run.supervision.workload_succeeded(r) for r in sequence),
                       deadlineExceeded=time.monotonic() > hard,
                       runnerSha256=run.digest(run.DLL) if run.DLL.is_file() else None)
        run.durable(directory / 'controls-cleanup.json', summary)
    print('CONTROLS ARTIFACTS', directory, flush=True)
    return 0 if summary['completed'] and not summary['deadlineExceeded'] and summary['sourceRevision'] == summary['currentRevision'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
