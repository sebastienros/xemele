"""Bind existing conformance results to unchanged integrated release inputs."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[3]
BASELINE = '6657e8e0ab0f34602a5040a04ee0d8fa197f70b1'
SCOPES = ['src', 'tests/corpora', 'tests/fixtures', 'tests/Xemele.Conformance.Tests',
          'tests/Xemele.Differential.Tests', 'tests/Xemele.TestUtilities',
          'tools/ReferenceRunners', 'Directory.Build.props', 'Directory.Packages.props', 'global.json']


def sha(payload):
    return hashlib.sha256(payload).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, timeout=30)


def main():
    assert not git('diff', '--name-only', BASELINE, 'HEAD', '--', *SCOPES).strip(), 'Conformance closure changed'
    assert not git('status', '--porcelain', '--', *SCOPES).strip(), 'Conformance closure has local edits'
    closure = git('ls-tree', '-r', '--full-tree', 'HEAD', '--', *SCOPES)
    freeze = json.loads((ROOT / 'docs/decisions/M9/release-inputs.json').read_bytes())
    protocol = {r['path']: r['sha256'] for r in freeze['protocolInputs']}
    assert all(sha((ROOT / name).read_bytes()) == expected for name, expected in protocol.items())
    old = json.loads((ROOT / 'docs/reports/M8-gate/summary.json').read_bytes())
    assert old['result'] == 'passed'
    groups = {r['group']: {k: r[k] for k in ['total', 'passed', 'failed', 'errors', 'skipped', 'not-run', 'identityIdsSha256']}
              for r in old['cumulative']['groups'] if r['group'] != 'Xemele.Tests'}
    assert all(g['total'] == g['passed'] and not any(g[k] for k in ['failed', 'errors', 'skipped', 'not-run']) for g in groups.values())
    xsd = old['schemaProfiles']['xsd']
    assert xsd['originalProduct'] == {'failed': 144, 'indeterminate': 11, 'passed': 39329}
    assert xsd['exactDispositions'] == 155 and xsd['gateCredit'] == xsd['originalCredit'] == 0
    assert xsd['annotation']['passed'] and xsd['annotation']['id'] == 'msMeta/Annotations_w3c.xml#g78/s:annotZ002'
    recent = json.loads((ROOT / 'docs/reports/M9-distribution/hosted-repair-preflight.json').read_bytes())
    assert recent['result'] == 'passed' and recent['unitCorrectness']['total'] == recent['unitCorrectness']['passed'] == 10741
    assert not git('diff', '--name-only', recent['revision'], 'HEAD', '--', 'src', 'tests/Xemele.Tests',
                   'tests/Xemele.TestUtilities', 'Directory.Build.props', 'Directory.Packages.props', 'global.json').strip()
    report = dict(result='passed', revision=git('rev-parse', 'HEAD').decode().strip(), baseline=BASELINE,
                  closureFiles=len(closure.splitlines()), closureSha256=sha(closure), scopes=SCOPES, protocol=protocol,
                  conformanceGroups=groups, schemaProfiles=old['schemaProfiles'],
                  freshLocalUnitEvidence='docs/reports/M9-distribution/hosted-repair-preflight.json',
                  unitCorrectness=recent['unitCorrectness'],
                  limitations=['Reuse applies to exact original supported profiles and denominators; reference limitations and approved zero-credit nonpasses retained.',
                               'Local macOS arm64 evidence only; no hosted Windows/Linux pass, sustained fuzz duration, branch percentage or current large-stream memory claim.'])
    (Path(__file__).parent / 'conformance-binding.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({k: report[k] for k in ['result', 'closureFiles', 'closureSha256', 'conformanceGroups', 'unitCorrectness']}))


if __name__ == '__main__':
    main()
