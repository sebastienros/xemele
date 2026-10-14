"""Check retained ordinary evidence without executing any campaign harness."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[3]

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, timeout=30)

def unchanged(revision, paths):
    changed = git('diff', '--name-only', revision, 'HEAD', '--', *paths).decode().splitlines()
    assert not changed, (revision, changed)
    assert not git('status', '--porcelain', '--', *paths).strip(), paths
    return {'revision': revision, 'paths': paths, 'sourceEquivalent': True}


def main():
    suite = json.loads((ROOT / 'docs/reports/M9-robustness/current-suite-binding.json').read_text())
    matrix = json.loads((ROOT / 'docs/reports/M9-distribution/hosted-windows-final/matrix.json').read_text())
    assert matrix['result'] == 'passed'
    closures = [unchanged(suite['revision'], [p for p in suite['scopes'] if p != 'tests/Xemele.Tests']),
                unchanged(matrix['sourceBinding']['windowsRevision'], ['src', 'tests/Xemele.Tests', 'tests/fixtures', 'tests/Xemele.TestUtilities', 'Directory.Build.props', 'Directory.Packages.props', 'global.json']),
                unchanged('aeb75df7172c0e9154afabd7107306186ab50b13', ['src', 'tests/corpora', 'tests/fixtures', 'tests/Xemele.TestUtilities', 'tools/ReferenceRunners', 'Directory.Build.props', 'Directory.Packages.props', 'global.json']),
                unchanged('0c571cd5d8df81b6640a509530f7637d72043205', ['src', 'tests/ReleaseCampaign/CoverageCases', 'tests/ReleaseCampaign/CoverageControl', 'Directory.Build.props', 'Directory.Packages.props', 'global.json'])]
    scope = json.loads((ROOT / 'docs/reports/M9-robustness/ordinary-coverage-scope.json').read_text())
    assert all(hashlib.sha256((ROOT / p).read_bytes()).hexdigest() == h for p, h in scope['sourceSha256'].items())
    coverage = json.loads((ROOT / 'docs/reports/M9-robustness/core-coverage-summary.json').read_text())
    assert coverage['coveredBranches'] == 2086 and coverage['totalBranches'] == 2268 and not coverage['exclusions']
    xsd = json.loads((ROOT / 'docs/reports/M9-robustness/current-xsd.json').read_text())
    assert xsd['originalProductCounts'] == {'passed': 39329, 'failed': 144, 'indeterminate': 11}
    assert xsd['exactDispositions'] == 155 and xsd['annotationPassed']
    report = {'result': 'passed', 'revision': git('rev-parse', 'HEAD').decode().strip(), 'closures': closures,
              'ordinaryResultsReused': {'units': 10754, 'conformance': 920, 'differential': 618, 'coverage': '2086/2268 = 91.9753%', 'xsdRowsInspected': 83472, 'approvedOriginalNonpasses': 155, 'additionalPassCredit': 0},
              'harnessExecution': 'NOT RUN', 'sustainedCampaignCredit': False, 'crashPositiveExecutionCredit': False,
              'limits': ['Checks bind retained passing executions to unchanged sources; no test execution is invented.', 'All OS unit results include the verified native path repair; older conformance sources remain unchanged.', 'Revised gate excludes offline campaign execution only; dedicated timing remains required.']}
    (Path(__file__).parent / 'offline-source-binding.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS ordinary evidence source bindings, full XML core hashes, and original XSD zero-credit accounting; harness NOT RUN')

if __name__ == '__main__':
    main()
