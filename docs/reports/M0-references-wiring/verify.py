"""Verify automatic runner wiring from clean outputs; caller selects pinned SDK."""
import json
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'docs/reports/M0-references-wiring/verification.txt'
log = []


def run(args):
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT)
    entry = '$ ' + shlex.join(args) + '\n' + result.stdout + f'Exit: {result.returncode}\n'
    log.append(entry)
    print(entry, flush=True)
    if result.returncode:
        raise RuntimeError(f'Command failed: {args}')
    return result.stdout


try:
    revision = run(['git', 'rev-parse', 'HEAD']).strip()
    assert not os.environ.get('XEMELE_BCL_RUNNER'), 'Runner override would hide wiring failure'
    assert not os.environ.get('XEMELE_MTP_SMOKE_FAIL'), 'Unexpected smoke failure override'
    assert run(['dotnet', '--version']).strip() == '10.0.400'
    # Traverse the actual solution's project-reference graph; remove only bin/obj.
    pending = [ROOT / p.attrib['Path'] for p in ET.parse(ROOT / 'Xemele.slnx').iter('Project')]
    projects = set()
    while pending:
        project = pending.pop().resolve()
        if project in projects:
            continue
        assert project.is_relative_to(ROOT)
        projects.add(project)
        pending.extend(project.parent / p.attrib['Include']
                       for p in ET.parse(project).iter('ProjectReference'))
    runner = ROOT / 'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
    for project in sorted(projects):
        for name in ('bin', 'obj'):
            output = project.parent / name
            if output.exists():
                shutil.rmtree(output)
                log.append(f'Removed generated output: {output.relative_to(ROOT)}\n')
    assert not runner.exists()
    log.append(f'Cleaned {len(projects)} solution/reference projects; runner absent before build.\n')
    # This existing verifier performs only ordinary solution restore/build before
    # executing the native MTP solution and controlled smoke failures.
    run([sys.executable, '-B', 'docs/reports/M0-build/verify.py'])
    assert runner.is_file()
    assert (runner.parent / 'ReferenceProtocol.dll').is_file()
    log.append('Ordinary solution build produced BclReference.dll and ReferenceProtocol.dll.\n')
    results = ROOT / 'artifacts/test-results'
    success = max(results.glob('mtp-*/success'), key=lambda p: p.stat().st_mtime)
    tests = [t for path in success.glob('*.xunit.xml') for t in ET.parse(path).iter('test')]
    protocol = [t for t in tests if '.Protocol.ReferenceProtocolTests.' in t.attrib['name']]
    smoke = [t for t in tests if '.Infrastructure.MtpRunnerSmokeTests.' in t.attrib['name']]
    assert len(protocol) == 51 and len(smoke) == 3 and len(tests) == 54
    assert all(t.attrib['result'] == 'Pass' for t in tests)
    log.append('Actual native MTP XML: 51 protocol tests + 3 smoke tests; 54 pass, 0 fail, 0 skip.\n')
    assets = json.loads((ROOT / 'src/Xemele/obj/project.assets.json').read_text())
    assert assets['libraries'] == {} and all(not t for t in assets['targets'].values())
    restore_frameworks = assets['project']['restore']['frameworks']
    assert all(not f.get('projectReferences') for f in restore_frameworks.values())
    log.append('Actual runtime restore graph: no package libraries, no project references, empty net10.0 target.\n')
    graph = run(['dotnet', 'msbuild', 'tests/Xemele.Differential.Tests/Xemele.Differential.Tests.csproj',
                 '-getItem:ProjectReference'])
    references = json.loads(graph)['Items']['ProjectReference']
    edge = [r for r in references if r['Identity'] == '../../tools/ReferenceRunners/Bcl/BclReference.csproj']
    assert len(edge) == 1 and edge[0]['ReferenceOutputAssembly'] == 'false'
    log.append(f'Automatic build-edge verification passed at {revision}; no runtime or performance claim.\n')
finally:
    REPORT.write_text('\n'.join(log))
