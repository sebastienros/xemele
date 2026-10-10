"""Preserve existing integrated checks by exact source/configuration identity.

No benchmarks, allocation counters, package builds or correctness replay. The
reported commands/results retain their original measured/tested revisions.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
OWN = Path(__file__).resolve().parent
CORRECTNESS = '8b77daddc975093bed1fcf0cec28008201707ae2'
PERFORMANCE = '8d894a96b1ad99799fb3732683bc7f6dd0066141'
SDK = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def unchanged(revision, roots):
    assert not git('diff', '--name-only', revision, 'HEAD', '--', *roots)
    paths = git('ls-tree', '-r', '--name-only', revision, '--', *roots).decode().splitlines()
    identities = {}
    for path in paths:
        content = (ROOT / path).read_bytes()
        assert content == git('show', revision + ':' + path), 'source differs: ' + path
        identities[path] = hashlib.sha256(content).hexdigest()
    return {'revision': revision, 'paths': len(paths),
            'sortedPathSha256Map': hashlib.sha256(json.dumps(identities, sort_keys=True, separators=(',', ':')).encode()).hexdigest()}


def main():
    path = OWN / (sys.argv[1] + '.json')
    assert not path.exists()
    inherited = json.loads((ROOT / 'docs/reports/M2-undeclared-attributes-runtime/integration-summary.json').read_bytes())
    performance = json.loads((ROOT / 'docs/reports/M2-undeclared-attributes-runtime/coordinator-performance-summary.json').read_bytes())
    assert inherited['result'] == inherited['apiPackage']['result'] == performance['result'] == 'passed'
    assert inherited['focusedCorrectness']['total'] == 2157
    assert inherited['apiPackage']['api']['allMembers'] == 372 and inherited['apiPackage']['api']['additiveMembers'] == 82
    assert inherited['apiPackage']['sourceConsumer'] == inherited['apiPackage']['packageConsumer']
    common = ['src', 'docs/api', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'global.json', 'NuGet.config']
    api = unchanged(CORRECTNESS, common + ['tests/Contracts', 'docs/reports/M2-contracts/Projects',
                                        'samples/M2-undeclared-contract', 'samples/M2-undeclared-attributes-runtime',
                                        'docs/reports/M0-contracts/ApiCheck', 'tools/ApiBaseline'])
    measured = unchanged(PERFORMANCE, common + ['benchmarks', 'docs/reports/M2-undeclared-attributes-runtime/measure.py',
                                               'docs/reports/M2-undeclared-attributes-runtime/MeasurementDriver.cs',
                                               'docs/reports/M2-dtd-entity-base', 'tests/Xemele.Tests/Syntax', 'tests/Xemele.Tests/PiTargets'])
    test_delta = git('diff', '--name-only', CORRECTNESS, 'HEAD', '--', 'tests/Xemele.Tests').decode().splitlines()
    assert test_delta == ['tests/Xemele.Tests/M2-conformance/EffectiveGrammarTests.cs']
    unchanged_tests = git('ls-tree', '-r', '--name-only', CORRECTNESS, '--', 'tests/Xemele.Tests').decode().splitlines()
    for source in unchanged_tests:
        assert (ROOT / source).read_bytes() == git('show', CORRECTNESS + ':' + source), source
    runtime = SDK / 'shared/Microsoft.NETCore.App/10.0.11/System.Private.CoreLib.dll'
    assert digest(runtime) == '9ed74c2e2efeee87f1f2d7ffe1736b7e9164ce248516ea39402a725eedc87af6'
    assert subprocess.check_output([str(SDK / 'dotnet'), '--version'], text=True).strip() == '10.0.400'
    assert performance['borrowedAllocation']['validation'] == 'passed'
    assert performance['borrowedAllocation']['records'][0]['bytes'] == 0
    report = {'result': 'passed', 'head': git('rev-parse', 'HEAD').decode().strip(), 'apiPackageSourceIdentity': api,
              'measuredInputIdentity': measured, 'existingRuntimeTestFilesUnchanged': len(unchanged_tests),
              'addedRuntimeTestPath': test_delta, 'sdk': '10.0.400', 'runtime': '10.0.11', 'runtimeSha256': digest(runtime),
              'preservedFocusedCorrectness': inherited['focusedCorrectness'], 'preservedApiPackage': inherited['apiPackage'],
              'preservedPerformance': performance,
              'reason': 'No runtime, API, shared build/package configuration, approved models, existing consumer, benchmark or existing runtime test source changed. New tests and independent corpus preparation/projection were executed separately. Informational commit versions change compiled bytes; this report preserves original runs at their original binary/source revisions and does not claim a fresh package or measurement at grammar HEAD.',
              'runtimeChange': False, 'newMeasurementsRequired': False, 'remainingGate': 'Comprehensive combined M2 gate checks and cross-architecture release validation remain their owning gate obligations.'}
    path.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'result': 'passed', 'preservedCorrectness': 2157, 'api': 372, 'additive': 82, 'borrowedBytes': 0}))


if __name__ == '__main__':
    main()
