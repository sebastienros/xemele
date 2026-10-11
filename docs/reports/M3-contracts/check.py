#!/usr/bin/env python3
"""Reproducible declaration/fake proof; no runtime streaming acceptance claimed."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[3]
report = root / 'artifacts/M3-contracts/contract-check.txt'
report.parent.mkdir(parents=True, exist_ok=True)
lines = []

def run(args, expected=0, diagnostic=None):
    command = ' '.join(args)
    result = subprocess.run(args, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    lines.extend(['$ ' + command, result.stdout, 'exit=' + str(result.returncode)])
    if (expected == 0 and result.returncode != 0) or (expected != 0 and result.returncode == 0) or (diagnostic and diagnostic not in result.stdout):
        report.write_text('\n'.join(lines))
        raise SystemExit('FAIL: ' + command + ' (see ' + str(report) + ')')
    if expected and diagnostic:
        # A fixture error must be the intended lifetime violation, not restore failure.
        errors = [line for line in result.stdout.splitlines() if ': error ' in line]
        if not errors or any(diagnostic not in line for line in errors):
            report.write_text('\n'.join(lines))
            raise SystemExit('Unexpected negative-probe error: ' + command)

for project in ('Sync', 'Async', 'Dom', 'InvalidLifetime', 'InvalidStorage'):
    path = f'docs/reports/M3-contracts/{project}/{project}.csproj'
    run(['dotnet', 'restore', path, '--ignore-failed-sources', '-p:NuGetAudit=false'])
    if project in ('Sync', 'Async'):
        run(['dotnet', 'run', '--project', path, '-c', 'Release', '--no-restore'])
    elif project == 'Dom':
        run(['dotnet', 'build', path, '-c', 'Release', '--no-restore'])
    else:
        run(['dotnet', 'build', path, '-c', 'Release', '--no-restore'], 1,
            'CS4007' if project == 'InvalidLifetime' else 'CS8345')
run(['python3', 'tools/planning/tasks.py', 'validate'])
report.write_text('\n'.join(lines))
print('PASS: independent Sync/Async consumers, DOM partials, CS4007/CS8345 negative probes, planning validator')
print(report)
