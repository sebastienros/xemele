"""Reproduce reference checks plus preserved bootstrap; no source mutations."""
import base64
import json
import os
from pathlib import Path
import shlex
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'artifacts/M0-references-protocol'
REPORT.mkdir(parents=True, exist_ok=True)
LOG = []


def run(command, timeout=300):
    result = subprocess.run(command, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, timeout=timeout)
    entry = '$ ' + shlex.join(command) + '\n' + result.stdout + f'Exit: {result.returncode}\n'
    LOG.append(entry)
    print(entry, flush=True)
    assert result.returncode == 0, entry
    return result.stdout


try:
    revision = run(['git', 'rev-parse', 'HEAD']).strip()
    flags = ['--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
    run(['dotnet', 'build', 'tools/ReferenceRunners/Bcl/BclReference.csproj', '-c', 'Release'] + flags)
    run(['dotnet', 'build', 'tests/Xemele.Differential.Tests/Xemele.Differential.Tests.csproj', '-c', 'Release'] + flags)
    runner = ROOT / 'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
    known = dict(protocolVersion=1, caseId='fixture', operation='xml.events',
                 profile='xml-1.0', inputBase64=base64.b64encode(b'<r a="1">ok</r>').decode())
    cases = [known, '{bad json', 'null', 'x' * 1048577,
             dict(known, inputBase64='invalid!'), dict(known, options={'unknown': True}),
             dict(known, profile='xml-1.1'), dict(known, encodingHint='latin1'),
             dict(known, offlineResources={'relative.dtd': 'AA=='}), known]
    large_identity = dict(known, caseId='x' * (1048576 - len(json.dumps(dict(known, caseId=''))) - 1))
    assert len(json.dumps(large_identity)) < 1048576
    cases.insert(-1, large_identity)
    wire = subprocess.run(['dotnet', str(runner)], cwd=ROOT, text=True,
                          input='\n'.join(c if isinstance(c, str) else json.dumps(c) for c in cases)+'\n',
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15)
    assert wire.returncode == 0 and not wire.stderr
    responses = [json.loads(line) for line in wire.stdout.splitlines()]
    assert len(responses) == len(cases)
    assert all(len(line) <= 1048576 for line in wire.stdout.splitlines())
    assert responses[0]['outcome'] == responses[-1]['outcome'] == 'success'
    assert all(r['outcome'] == 'runner-failure' for r in responses[1:-1])
    assert responses[0]['caseId'] == 'fixture'
    assert responses[0]['events'][1]['value'] == 'ok'
    (REPORT / 'wire-responses.jsonl').write_text(wire.stdout)
    LOG.append('11 direct wire cases passed; malformed/oversized requests and oversized response identity recovered; actual metadata/output: wire-responses.jsonl\n')
    run(['dotnet', 'test', '--project', 'tests/Xemele.Differential.Tests/Xemele.Differential.Tests.csproj',
         '-c', 'Release', '--no-build', '--no-restore', '--no-ansi', '--no-progress',
         '--filter-class', 'Xemele.Differential.Tests.Protocol.ReferenceProtocolTests'])
    run([sys.executable, '-B', 'docs/reports/M0-build/verify.py'])
    run([sys.executable, '-B', '-m', 'unittest', 'discover', '-s', 'tests/Xemele.TestUtilities/Build', '-p', 'test_*.py', '-v'])
    run([sys.executable, '-B', 'tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py'])
    run([sys.executable, '-B', 'tools/planning/tasks.py', 'validate'])
    LOG.append(f'Checks passed at {revision}; reference infrastructure only; no runtime conformance/platform/performance claim.\n')
finally:
    (REPORT / 'verification.txt').write_text('\n'.join(LOG))
