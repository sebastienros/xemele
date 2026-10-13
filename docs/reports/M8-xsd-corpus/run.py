#!/usr/bin/env python3
"""One bounded two-engine campaign. Exit 0 means harness completion, never conformance."""
import argparse
import collections
import hashlib
import importlib.util
import json
import os
import subprocess
import sys
import time
import uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CORPUS = ROOT / 'tests/corpora/xsd'
OUT = ROOT / 'artifacts/M8-xsd-corpus'
D = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
DLL = OUT / 'runner-bin/Release/net10.0/XsdCorpus.dll'
PIN = '7bc3365c652a322f3d762021b3879eb92dae7e30'
RUNTIME = '10.0.11'
CAMPAIGN_SECONDS = 3600
SET_WORK_SECONDS = 300
CLEANUP_SECONDS = 10
CAPS = dict(DocumentCharacters=67108864, ExternalBytes=67108864,
            Depth=1024, AttributesPerElement=4096, TokenCharacters=16777216,
            InternedNames=262144, ExternalResources=1024, InclusionDepth=128,
            Diagnostics=1024, EvaluationWork=100000000, SchemaCompilationMaxOperations=100000000,
            SchemaCompilationMaxComponents=1000000, ValidationMaxOperations=100000000, OperationSeconds=30)
INTROSPECTION = {'common/introspection.testSet#g0/i1:introspect-testSet-NIST2004-01-14-2':
                {'EvaluationWork': 20000000000, 'OperationSeconds': 60, 'ValidationMaxOperations': 20000000000},
                'common/introspection.testSet#g0/i20:introspect-testSet-MS-DataTypes2006-07-15-21':
                {'EvaluationWork': 20000000000, 'OperationSeconds': 60, 'ValidationMaxOperations': 20000000000},
                'common/introspection.testSet#g0/i28:introspect-testSet-MS-Regex2006-07-15-29':
                {'EvaluationWork': 20000000000, 'OperationSeconds': 60, 'ValidationMaxOperations': 20000000000}}


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


supervision = module('corpus_supervision', HERE / 'supervision.py')
nested = module('corpus_nested_ownership', HERE / 'nested_ownership.py')
reference_exception = module('corpus_reference_exception', HERE / 'reference_exception.py')


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as source:
        for block in iter(lambda: source.read(1048576), b''):
            value.update(block)
    return value.hexdigest()


def durable(path, value):
    """Exclusive file creation, flushed/fsynced content, then fsynced directory."""
    with Path(path).open('x') as stream:
        json.dump(value, stream, sort_keys=True, indent=2)
        stream.write('\n')
        stream.flush()
        os.fsync(stream.fileno())
    directory = os.open(Path(path).parent, os.O_RDONLY)
    try:
        os.fsync(directory)
    finally:
        os.close(directory)


def verify(rows, expected, code):
    assert {r['id'] for r in rows} == {r['id'] for r in expected}, 'case ID coverage differs'
    assert len(rows) == len({r['id'] for r in rows}), 'duplicate observations'
    return code == 0


def select_sets(selected, inventory):
    sets = [selected] if selected else inventory['testSets']
    assert all(s in inventory['testSets'] for s in sets), 'unknown selected test set'
    return sets


def expected_rows(inventory, testset):
    return [r for g in inventory['groups'] if g['metadata'] == testset
            for r in ([g['schema']] if g['schema'] else []) + g['instances'] + g['supplementalInstances']]


def inspect_rows(spec):
    rows = [json.loads(line) for line in Path(spec['journal']).read_text().splitlines()]
    verify(rows, spec['expected'], 0)
    originals = {row['id']: row for row in spec['expected']}
    for row in rows:
        expected = originals[row['id']]
        for field in ('classification', 'kind', 'expected'):
            assert row[field] == expected[field], f'{row["id"]}: changed {field}'
        assert row['engine'] == spec['engine'], 'engine identity differs'
        assert not row['actual'].startswith('harness-'), 'harness observation cannot complete a set'
        assert row['passed'] is None or type(row['passed']) is bool, 'invalid original result'
        if row['actual'] == 'reference-engine-exception':
            reference_exception.validate_observation(row, expected)
        if row['passed'] is True:
            assert row['actual'] in ('schema-valid', 'schema-invalid', 'instance-valid', 'instance-invalid'), 'refusal cannot be a validity pass'
            if row['expected'] in ('valid', 'invalid'):
                assert row['actual'] == row['kind'] + '-' + row['expected'], 'original outcome cannot be promoted'
    return rows


def execute_cases(cases, outdir, environment, work_deadline, hard_deadline):
    """Never inspect a journal before its durable cleanup receipt; stop on infrastructure failure."""
    supervision.ARTIFACTS = outdir
    result = {'status': 1, 'exitMeaning': 'harness completion only; no conformance pass',
              'sequence': [], 'countsByEngine': {}, 'inspectedCases': 0,
              'rawResultsInspected': 0, 'coverageFailure': None}
    try:
        for index, spec in enumerate(cases):
            now = time.monotonic()
            child_hard = min(now + spec.get('workSeconds', SET_WORK_SECONDS) + CLEANUP_SECONDS, hard_deadline)
            child_work = min(now + spec.get('workSeconds', SET_WORK_SECONDS), work_deadline, child_hard - CLEANUP_SECONDS)
            with nested.track_launches(supervision, environment, child_work):
                receipt = supervision.supervise_command(spec['argv'], index, environment, child_work, child_hard)
            receipt.update(engine=spec['engine'], testSet=spec['testSet'], journal=str(spec['journal']))
            result['sequence'].append(receipt)
            durable(outdir / f'lifecycle-{index:03d}.json', receipt)
            if not supervision.workload_succeeded(receipt):
                break
            try:
                rows = inspect_rows(spec)
            except Exception as error:
                result['coverageFailure'] = {'command': index, 'error': str(error), 'type': type(error).__name__}
                break
            result['inspectedCases'] += 1
            result['rawResultsInspected'] += len(rows)
            counts = collections.Counter('excluded' if r['classification'] == 'excluded' else
                                         'unresolved' if r['classification'] == 'unresolved' else
                                         'observationPassed' if r['passed'] is True else
                                         'observationNonpassed' if r['passed'] is False else
                                         'observationIndeterminate' for r in rows)
            total = collections.Counter(result['countsByEngine'].get(spec['engine'], {}))
            total.update(counts)
            result['countsByEngine'][spec['engine']] = dict(total)
            print(spec['engine'], spec['testSet'], 'completed', dict(counts), flush=True)
        if (result['inspectedCases'] == len(cases) and result['coverageFailure'] is None
                and all(supervision.workload_succeeded(r) for r in result['sequence'])):
            result['status'] = 0
        return result
    finally:
        result['allLaunchedChildrenClean'] = all(r['directChildReaped'] and r['ownedGroupAbsent'] and r['logClosed']
                                               for r in result['sequence'])
        durable(outdir / 'campaign-cleanup.json', result)


def verified_dependencies():
    own = json.loads((ROOT / 'docs/tasks/M8-xsd-corpus.json').read_text())
    ids = set(own['depends_on']) | {'M8-xsd-type-relations', 'M8-xsd-particle-normalization', 'M8-xsd-optional-composition'}
    statuses = {name: json.loads((ROOT / f'docs/tasks/{name}.json').read_text())['status'] for name in sorted(ids)}
    assert all(s == 'verified' for s in statuses.values()), f'unverified dependency: {statuses}'
    return statuses


def mismatches(identities):
    return [name for name, sha in identities.items() if not (ROOT / name).is_file() or digest(ROOT / name) != sha]


def revision():
    return subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True, timeout=5).strip()


def preparation_commands():
    build = [D, 'build', 'tests/Xemele.Conformance.Tests/Xsd/Corpus.Runner.csproj', '-c', 'Release',
             '--no-restore', '--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11',
             '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
    commands = [build, [D, '--fx-version', RUNTIME, str(DLL), '--controls'],
                [D, '--fx-version', RUNTIME, str(DLL), '--hint-profile-controls', str(CORPUS / 'upstream')],
                [D, '--fx-version', RUNTIME, str(DLL), '--optional-profile-controls', str(CORPUS / 'upstream')]]
    for script in ['tests/corpora/xsd/import.py', 'tests/corpora/xsd/test_import.py',
                   'docs/reports/M8-xsd-corpus/test_supervisor.py', 'docs/reports/M8-xsd-corpus/test_dispositions.py',
                   'docs/reports/M8-xsd-corpus/test_approved_dispositions.py',
                   'docs/reports/M8-xsd-corpus/check-optional-inputs.py']:
        commands.append([sys.executable, script] + (['--check'] if script.endswith('/import.py') else []))
    return commands


def verified_api_inputs():
    """Current verified API preparation binding; root replaces only after next verified integration."""
    binding_path = HERE / 'verified-api-preparation.json'
    binding = json.loads(binding_path.read_text())
    assert binding['final_freeze_ready'] is True, 'final source/API freeze held pending root verified integration and accepted manifest'
    source = ROOT / binding['source_manifest']
    assert digest(source) == binding['source_manifest_sha256'], 'verified API manifest changed'
    assert binding['apiInputs'] == json.loads(source.read_text())['apiInputs'], 'accepted API membership changed'
    paths = {str(p.relative_to(ROOT)) for p in (ROOT / 'docs/api').glob('*.txt')}
    assert paths == set(binding['apiInputs']), 'complete docs/api fragment membership changed'
    for name, checksum in binding['apiInputs'].items():
        assert digest(ROOT / name) == checksum, 'verified API fragment changed: ' + name
    subprocess.run(['git', 'merge-base', '--is-ancestor', binding['verified_integration'], 'HEAD'],
                   cwd=ROOT, check=True, timeout=5)
    return {**binding['apiInputs'], binding['source_manifest']: binding['source_manifest_sha256']}


def verified_runtime_inputs():
    """Bind actual verified missing runtime, authoritative task, and nullable/additive API evidence."""
    binding = json.loads((HERE / 'verified-api-preparation.json').read_text())
    identities = dict(binding['runtime_production_inputs'])
    for path_key, sha_key in [('runtime_source_manifest', 'runtime_source_manifest_sha256'),
                              ('runtime_review_plan', 'runtime_review_plan_sha256'),
                              ('runtime_api_proof', 'runtime_api_proof_sha256'),
                              ('repair_proof', 'repair_proof_sha256'),
                              ('verified_task', 'verified_task_sha256'),
                              ('integration_record', 'integration_record_sha256')]:
        name = binding[path_key]
        assert digest(ROOT / name) == binding[sha_key], 'verified runtime binding changed: ' + name
        identities[name] = binding[sha_key]
    identities.update(binding['baselines_sha256'])
    identities.update(binding['reviewed_build_inputs'])
    for name, present in binding['root_configuration_presence'].items():
        assert (ROOT / name).exists() is present, 'verified root build configuration membership changed: ' + name
    task = json.loads((ROOT / binding['verified_task']).read_text())
    record = json.loads((ROOT / binding['integration_record']).read_text())
    proof = json.loads((ROOT / binding['runtime_api_proof']).read_text())
    assert task['status'] == 'verified' and task['integration_revision'] == binding['verified_integration'] == record['testedRevision']
    repair = json.loads((ROOT / binding['repair_proof']).read_text())
    assert binding['verified_task_sha256'] == repair['verified_task_sha256']
    assert repair['tested_integration'] == task['integration_revision'] and repair['integration_record_sha256'] == binding['integration_record_sha256']
    assert record['result'] == 'passed' and record['summary']['completed'] is True and record['commands'] == 24
    assert record['full93EqualVerified'] is True and repair['actual_full93_equal_verified'] is True
    assert record['summary']['sourceSha256'] == binding['source_manifest_sha256']
    assert record['originalDispositionPassCredit'] == record['gatePassCredit'] == 0
    assert repair['annotation_waiver'] is False and repair['original_annotation'] == record['originalAnnotation']
    assert repair['availability_source_sha256'] == binding['runtime_production_inputs']['src/Xemele/Validation/Xsd/Compilation/Availability/Availability.cs'] == 'c5ccdab54580f735bf030906951fe2b7ca02ae163036cf98963a53f2888db1e3'
    assert repair['prior_api_proof_sha256'] == binding['runtime_api_proof_sha256']
    assert record['summarySha256'] == repair['released_check_sha256']
    for name, checksum in [(repair['canonical_projection'],repair['canonical_projection_sha256']),
                           (repair['allocation_custody_record'],repair['allocation_custody_record_sha256'])]:
        assert digest(ROOT / name) == checksum, 'verified repair provenance changed: ' + name
        identities[name] = checksum
    assert proof['canonical_added_api_sha256'] == repair['canonical_projection_sha256'] == '33a4823adf25f638d0e52990918f859c2b64a35359d25aa59c318f4543a1812d'
    historical = binding['historical_missing_binding']
    for path_key, sha_key in [('source_manifest','source_manifest_sha256'),('runtime_source_manifest','runtime_source_manifest_sha256'),
                              ('runtime_review_plan','runtime_review_plan_sha256'),('verified_task','verified_task_sha256'),('integration_record','integration_record_sha256')]:
        name = historical[path_key]
        assert digest(ROOT / name) == historical[sha_key], 'historical verified API provenance changed: ' + name
        identities[name] = historical[sha_key]
    prior = json.loads((ROOT / historical['integration_record']).read_text())
    assert proof['canonical_added_projection'] == prior['summary']['addedInternalProjection']
    assert proof['canonical_added_api_sha256'] == prior['canonicalAddedApiSha256']
    assert proof['released_check_sha256'] == prior['receipts']['checks/checks-summary.json']['sha256']
    assert record['summary']['tests']['nonassessmentControls'] == 4
    assert record['summary']['tests']['nonassessmentGateCredit'] == record['summary']['tests']['nonassessmentOriginalPassCredit'] == 0
    assert (proof['public_rows'], proof['api_fragments'], proof['added_internal_rows'], proof['new_type_rows'], proof['new_types'], proof['authored_joins']) == (1430, 55, 93, 80, 11, 13)
    assert (record['summary']['publicRows'],record['summary']['xsdRows'],record['summary']['compilerRows'],record['summary']['addedInternalRows']) == (1430,615,257,93)
    actual = {str(p.relative_to(ROOT)) for p in (ROOT / 'src/Xemele').rglob('*') if p.is_file()
              and p.suffix in ('.cs', '.csproj', '.props', '.targets')
              and not {'bin', 'obj'}.intersection(p.relative_to(ROOT).parts)}
    assert actual == set(binding['runtime_production_inputs']), 'verified production source membership changed'
    for name, checksum in identities.items():
        assert digest(ROOT / name) == checksum, 'verified runtime/source/API baseline changed: ' + name
    return identities


def input_identities(include_binaries=True):
    """Declared complete source/input identities; runtime payload added only after fresh build."""
    dependencies = verified_dependencies()
    api_inputs = verified_api_inputs()
    runtime_inputs = verified_runtime_inputs()
    identities = {'tests/corpora/xsd/upstream/' + r['path']: r['sha256']
                  for r in json.loads((CORPUS / 'resources.json').read_text())}
    for folder in [CORPUS, HERE, ROOT / 'tests/Xemele.Conformance.Tests/Xsd', ROOT / 'tests/Xemele.Differential.Tests/Xsd']:
        for file in folder.iterdir():
            if file.is_file():
                identities[str(file.relative_to(ROOT))] = digest(file)
    for file in (ROOT / 'src').rglob('*'):
        if file.is_file() and file.suffix in ('.cs', '.csproj', '.props', '.targets') and not {'bin', 'obj'}.intersection(file.relative_to(ROOT).parts):
            identities[str(file.relative_to(ROOT))] = digest(file)
    for pattern in ('Directory.Build.*', 'global.json', 'NuGet.Config'):
        for file in ROOT.glob(pattern):
            if file.is_file():
                identities[str(file.relative_to(ROOT))] = digest(file)
    for name in list(dependencies) + ['M8-xsd-corpus']:
        file = ROOT / f'docs/tasks/{name}.json'
        identities[str(file.relative_to(ROOT))] = digest(file)
    identities.update(api_inputs)
    identities.update(runtime_inputs)
    if include_binaries:
        for folder in [OUT / 'runner-bin/Release/net10.0', ROOT / 'src/Xemele/bin/Release/net10.0']:
            assert folder.is_dir(), f'missing fresh build folder: {folder}'
            for file in folder.rglob('*'):
                if file.is_file():
                    identities[str(file.relative_to(ROOT))] = digest(file)
        assert str(DLL.relative_to(ROOT)) in identities
        assert str((DLL.parent / 'Xemele.dll').relative_to(ROOT)) in identities, 'tested product dependency absent'
    for file in (Path(D), Path(sys.executable)):
        identities[str(file)] = digest(file)
    runtime = Path(D).parent / 'shared/Microsoft.NETCore.App' / RUNTIME
    assert runtime.is_dir()
    for file in runtime.rglob('*'):
        if file.is_file():
            identities[str(file)] = digest(file)
    return identities


def verify_preparation_bindings(commands_file, tested_file, tested_sha):
    commands = json.loads(commands_file.read_text())
    assert commands['commands'] == preparation_commands(), 'exact required preparation commands changed'
    assert commands['sourceRevision'] == revision(), 'preparation source differs'
    assert (commands['campaignSeconds'], commands['childWorkSeconds'], commands['cleanupSeconds']) == (1800, 300, 10)
    assert tested_sha is not None and digest(tested_file) == tested_sha, 'frozen tested manifest changed'
    tested = json.loads(tested_file.read_text())
    assert tested['sourceRevision'] == revision() and tested['commandsSha256'] == digest(commands_file)
    current = input_identities()
    assert set(current) == set(tested['identities']), 'tested input membership changed'
    assert not mismatches(tested['identities']), 'tested dependency/runtime/source identity changed'
    return tested['identities']


def prepare(path, controls_receipt):
    """Use precisely the payload tested after fresh build; never freeze replacement dependencies."""
    dependencies = verified_dependencies()
    assert controls_receipt.name == 'controls-cleanup.json' and OUT.resolve() in controls_receipt.resolve().parents, 'owned actual preparation receipt required'
    controls = json.loads(controls_receipt.read_text())
    assert controls['completed'] is True and controls['deadlineExceeded'] is False
    assert controls['bindingFailure'] is None
    assert controls['sourceRevision'] == controls['currentRevision'] == revision()
    commands_file = controls_receipt.with_name('commands.json')
    tested_file = controls_receipt.with_name('tested-inputs.json')
    assert controls['testedInputsSha256'] == digest(tested_file), 'tested manifest replaced'
    assert controls['commandsSha256'] == digest(commands_file), 'required commands replaced'
    identities = dict(verify_preparation_bindings(commands_file, tested_file, controls['testedInputsSha256']))
    required = preparation_commands()
    assert len(controls['sequence']) == len(required)
    for index, record in enumerate(controls['sequence']):
        assert record['command'] == index and record['argv'] == required[index]
        assert supervision.workload_succeeded(record) and record['nestedOwnership']['allOwnedGroupsAbsent']
        lifecycle = controls_receipt.with_name(f'lifecycle-{index:03d}.json')
        assert json.loads(lifecycle.read_text()) == record, 'durable child receipt differs'
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True, timeout=5).strip(), 'source must be clean'
    inventory = json.loads((CORPUS / 'inventory.json').read_text())
    assert inventory['revision'] == PIN and len(inventory['testSets']) == 93
    assert sum(len(expected_rows(inventory, s)) for s in inventory['testSets']) == 41736
    module('corpus_dispositions', HERE / 'dispositions.py').approved_sources()
    receipts = [controls_receipt, commands_file, tested_file]
    receipts += [controls_receipt.with_name(f'lifecycle-{index:03d}.json') for index in range(len(required))]
    for registry in controls_receipt.parent.glob('nested-*'):
        receipts.extend(file for file in registry.iterdir() if file.is_file())
    for receipt in receipts:
        identities[str(receipt.relative_to(ROOT))] = digest(receipt)
    assert not mismatches(identities)
    manifest = dict(sourceRevision=revision(), upstreamRevision=PIN, runtime=RUNTIME,
                    engines=['product', 'bcl'], testSets=inventory['testSets'],
                    campaignSeconds=CAMPAIGN_SECONDS, setWorkSeconds=SET_WORK_SECONDS,
                    cleanupSeconds=CLEANUP_SECONDS, effectiveLimits=CAPS, introspectionOverrides=INTROSPECTION,
                    rowsPerEngine=41736, applicability=inventory['counts'],
                    verifiedDependencies=dependencies, identities=identities)
    assert OUT.resolve() in Path(path).resolve().parents, 'manifest belongs in ignored owned artifacts'
    durable(path, manifest)
    print(json.dumps({'manifest': str(path), 'sha256': digest(path)}, sort_keys=True), flush=True)


def campaign(manifest_path, expected_hash):
    started = time.monotonic()
    hard = started + CAMPAIGN_SECONDS
    work = hard - CLEANUP_SECONDS
    assert digest(manifest_path) == expected_hash, 'frozen manifest changed'
    manifest = json.loads(Path(manifest_path).read_text())
    inventory = json.loads((CORPUS / 'inventory.json').read_text())
    for key, value in dict(campaignSeconds=CAMPAIGN_SECONDS, setWorkSeconds=SET_WORK_SECONDS,
                           cleanupSeconds=CLEANUP_SECONDS, effectiveLimits=CAPS,
                           introspectionOverrides=INTROSPECTION, runtime=RUNTIME, upstreamRevision=PIN,
                           engines=['product', 'bcl'], testSets=inventory['testSets'],
                           rowsPerEngine=41736, applicability=inventory['counts'],
                           verifiedDependencies=verified_dependencies(), sourceRevision=revision()).items():
        assert manifest[key] == value, f'frozen {key} differs'
    assert len(manifest['testSets']) == 93 and inventory['revision'] == PIN
    assert sum(len(expected_rows(inventory, s)) for s in inventory['testSets']) == 41736
    assert not mismatches(manifest['identities']), 'input identity changed before execution'
    outdir = OUT / (manifest['sourceRevision'] + '-full-' + uuid.uuid4().hex)
    outdir.mkdir(parents=True, exist_ok=False)
    cases = []
    for engine in manifest['engines']:
        for testset in manifest['testSets']:
            journal = outdir / (engine + '-' + testset.replace('/', '_') + '.jsonl')
            cases.append(dict(engine=engine, testSet=testset, journal=journal,
                              expected=expected_rows(inventory, testset),
                              argv=[D, '--fx-version', RUNTIME, str(DLL), str(CORPUS / 'upstream'),
                                    str(CORPUS / 'inventory.json'), testset, engine, str(journal)]))
    durable(outdir / 'frozen-campaign.json', {'manifest': str(manifest_path), 'sha256': expected_hash,
                                            'commands': [c['argv'] for c in cases], 'sourceRevision': manifest['sourceRevision']})
    result = None
    try:
        result = execute_cases(cases, outdir, os.environ.copy(), work, hard)
    finally:
        after = mismatches(manifest['identities'])
        manifest_changed = digest(manifest_path) != expected_hash
        exceeded = time.monotonic() > hard
        durable(outdir / 'final-identity-cleanup.json', {'result': result, 'identityChanges': after,
                                                       'manifestChanged': manifest_changed,
                                                       'totalDeadlineExceeded': exceeded})
    print('ARTIFACTS', outdir, 'EXIT 0 MEANS HARNESS COMPLETION ONLY', flush=True)
    return 0 if result['status'] == 0 and not after and not manifest_changed and not exceeded else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--prepare', type=Path)
    group.add_argument('--manifest', type=Path)
    parser.add_argument('--manifest-sha256')
    parser.add_argument('--controls-receipt', type=Path)
    args = parser.parse_args()
    if args.prepare:
        assert args.manifest_sha256 is None
        assert args.controls_receipt, 'actual bounded preparation receipt required'
        prepare(args.prepare.resolve(), args.controls_receipt.resolve())
        return 0
    assert args.controls_receipt is None
    assert args.manifest_sha256, 'explicit frozen manifest hash required'
    return campaign(args.manifest, args.manifest_sha256)


if __name__ == '__main__':
    raise SystemExit(main())
