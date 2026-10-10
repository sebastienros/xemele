"""Bounded identity proof for reusing verified runs; executes no corpus models."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
OWN = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / 'docs/reports/M2-conformance-grammar'))
from preserve_grammar import unchanged, CORRECTNESS, PERFORMANCE, git
sys.path.insert(0, str(ROOT / 'docs/reports/M2-conformance-profile'))
from classify import verified


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    full_path = ROOT / 'docs/reports/M2-conformance-grammar/integration-full-grammar.json'
    full = json.loads(full_path.read_bytes())
    summary = json.loads((ROOT / 'docs/reports/M2-conformance-grammar/grammar-integration-summary.json').read_bytes())
    assert full['result'] == summary['result'] == 'passed'
    assert sha(full_path) == summary['originalByteModels']['reportSha256']
    task = json.loads((ROOT / 'docs/tasks/M2-conformance.json').read_bytes())
    dependencies = {}
    for name in task['depends_on']:
        dep = json.loads((ROOT / 'docs/tasks' / (name + '.json')).read_bytes())
        assert dep['status'] == 'verified'
        subprocess.run(['git', 'merge-base', '--is-ancestor', dep['integration_revision'], 'HEAD'], cwd=ROOT, check=True)
        dependencies[name] = dep['integration_revision']
    for revision in [full['provenance']['revision'], CORRECTNESS, PERFORMANCE]:
        subprocess.run(['git', 'merge-base', '--is-ancestor', revision, 'HEAD'], cwd=ROOT, check=True)
    # Historical binaries retain their original identity/revision. Fresh informational
    # commit versions do not create a new corpus/package/measurement result.
    identities = {}
    for group in ['sourceInputs', 'runtimeClosure']:
        expected = full['provenance'][group]
        for path, digest in expected.items():
            assert sha(ROOT / path) == digest, path
        identities[group] = {'paths': len(expected), 'sortedPathSha256Map': hashlib.sha256(json.dumps(expected, sort_keys=True, separators=(',', ':')).encode()).hexdigest()}
    dotnet = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
    assert sha(dotnet) == full['provenance']['dotnetExecutableSha256']
    assert subprocess.check_output([str(dotnet), '--version'], text=True).strip() == '10.0.400'
    inventory = json.loads((ROOT / 'tests/corpora/xml/inventory.json').read_bytes())
    ledger = json.loads((ROOT / 'tests/corpora/xml/m2-applicability.json').read_bytes())
    assert {c['id'] for c in inventory['cases']} == {c['id'] for c in ledger['cases']}
    assert len(ledger['cases']) == 2585 and len(ledger['files']) == 3386
    assert inventory['files'] == ledger['files']
    for file in ledger['files']:
        verified(file['path'], file['sha256'])
    common = ['src', 'docs/api', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'global.json', 'NuGet.config']
    api = unchanged(CORRECTNESS, common + ['tests/Contracts', 'docs/reports/M2-contracts/Projects', 'samples/M2-undeclared-contract', 'samples/M2-undeclared-attributes-runtime', 'docs/reports/M0-contracts/ApiCheck', 'tools/ApiBaseline'])
    measured = unchanged(PERFORMANCE, common + ['benchmarks', 'docs/reports/M2-undeclared-attributes-runtime/measure.py', 'docs/reports/M2-undeclared-attributes-runtime/MeasurementDriver.cs', 'docs/reports/M2-dtd-entity-base', 'tests/Xemele.Tests/Syntax', 'tests/Xemele.Tests/PiTargets'])
    prior = json.loads((ROOT / 'docs/reports/M2-conformance-grammar/grammar-preservation-cd98149.json').read_bytes())
    assert api == prior['apiPackageSourceIdentity'] and measured == prior['measuredInputIdentity']
    old_tests = git('ls-tree', '-r', '--name-only', CORRECTNESS, '--', 'tests/Xemele.Tests').decode().splitlines()
    for path in old_tests:
        assert (ROOT / path).read_bytes() == git('show', CORRECTNESS + ':' + path), path
    existing_closure = ['src', 'tools/ReferenceRunners', 'samples/M2-conformance-profile', 'samples/M2-conformance-grammar', 'tests/corpora/xml', 'docs/reports/M2-conformance-profile', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'global.json', 'NuGet.config']
    # Reports and the preservation helper were added after the actual corpus run.
    # Its recorded 89 executable/source inputs are checked individually above.
    closure = unchanged(full['provenance']['revision'], existing_closure)
    assert not git('ls-files', '--others', '--exclude-standard', '--', *existing_closure)
    report = {'result': 'passed', 'revision': git('rev-parse', 'HEAD').decode().strip(), 'verifiedDependencies': dependencies,
              'inheritedCorpusReport': str(full_path.relative_to(ROOT)), 'inheritedCorpusReportSha256': sha(full_path),
              'inheritedCorpusTestedRevision': full['provenance']['revision'], 'sourceAndRuntimeIdentity': identities,
              'unchangedCorpusOracleRuntimeConfigurationClosure': closure,
              'originalIds': 2585, 'originalResourceHashesVerified': 3386, 'counts': full['counts'],
              'caseModelHashMapSha256': hashlib.sha256(json.dumps(full['caseModelHashes'], sort_keys=True).encode()).hexdigest(),
              'referenceResolutionMapSha256': hashlib.sha256(json.dumps(full['referenceResolutions'], sort_keys=True).encode()).hexdigest(),
              'apiPackageSourceIdentity': api, 'measuredInputIdentity': measured, 'unchangedEarlierRuntimeTests': len(old_tests),
              'preservedCorrectness': 2157, 'preservedApiMembers': 372, 'preservedAdditiveMembers': 82, 'preservedBorrowedBytes': 0,
              'runtimeChanged': False, 'newMeasurements': False,
              'scope': summary['projectionScope'],
              'limits': 'Nonvalidating XML 1.0 Fifth Edition / Namespaces 1.0 Third Edition; declarations with compatible lexical 1.x versions do not activate XML 1.1. Corpus/models/package/performance results retain original tested revisions. Arm64 timing is informational; combined M2 gate and cross-architecture release checks remain separate obligations.'}
    (OWN / 'join-identity.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'result': 'passed', 'originalIds': 2585, 'resourceHashes': 3386, 'sourceInputs': len(full['provenance']['sourceInputs']), 'runtimeInputs': len(full['provenance']['runtimeClosure'])}))


if __name__ == '__main__':
    main()
