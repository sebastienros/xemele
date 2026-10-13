#!/usr/bin/env python3
"""SOURCE-only AST, immutable input hashes/metadata, and verified ancestor checks; no project imports/children."""
import ast
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
CORPUS = ROOT / 'tests/corpora/xsd'
for file in sorted(HERE.glob('*.py')):
    ast.parse(file.read_text())
accepted = ast.parse((ROOT / 'docs/reports/M8-xsd-particle-normalization/measure.py').read_text())
owned = ast.parse((HERE / 'supervision.py').read_text())
for name in ['members', 'kill_group', 'supervise', 'supervise_command', 'workload_succeeded', 'run_commands']:
    def function(tree):
        return next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == name)
    assert ast.dump(function(owned), include_attributes=False) == ast.dump(function(accepted), include_attributes=False)
for name in ['M8-schema-resource-outcomes', 'M8-xsd-type-relations', 'M8-xsd-particle-normalization', 'M8-xsd-optional-composition']:
    task = json.loads((ROOT / f'docs/tasks/{name}.json').read_text())
    assert task['status'] == 'verified'
    subprocess.run(['git', 'merge-base', '--is-ancestor', task['integration_revision'], 'HEAD'], cwd=ROOT, check=True, timeout=5)
catalog = {r['path']: r['sha256'] for r in json.loads((CORPUS / 'resources.json').read_text())}
inventory = json.loads((CORPUS / 'inventory.json').read_text())
originals = {r['id']: r for g in inventory['groups'] for r in ([g['schema']] if g['schema'] else []) + g['instances'] + g['supplementalInstances']}
assert inventory['revision'] == '7bc3365c652a322f3d762021b3879eb92dae7e30'
assert len(originals) == 41736 and len(inventory['testSets']) == 93
assert len(catalog) == 42270
records = []
for name, parent, sha in [
    ('approved-standards-dispositions.json', 'M8-xsd-standards-policy-pending.json', 'b44cec9757982c7d53cc0555bfc192e6c6172bda958e5a1b8765ca97c7cad4e0'),
    ('approved-input-limitations.json', 'M8-xsd-input-boundaries-policy-pending.json', 'e82338d5eabd52590a4b0b25e679720edc318795ca216cae99a65d3de51c4063')]:
    payload = (CORPUS / name).read_bytes()
    assert payload == (ROOT / 'docs/orchestration' / parent).read_bytes()
    assert hashlib.sha256(payload).hexdigest() == sha
    document = json.loads(payload)
    assert document['human_approval']['question_id'] == 'call_dcdabbc481104ee2b07f5e4a5f8ba308'
    for key in ['schema_cases', 'instance_cases', 'dependent_unassessed_instances', 'fatal_xml', 'malformed_hint_prerequisites', 'indeterminate_cycles']:
        records.extend(document.get(key, []))
    if 'namespace_guard' in document:
        records.append(document['namespace_guard'])
assert len(records) == len({r['id'] for r in records}) == 35
facts = json.loads((CORPUS / 'verified-optional-inputs.json').read_text())
assert hashlib.sha256((ROOT / facts['source_manifest']).read_bytes()).hexdigest() == facts['source_manifest_sha256']
assert len(facts['cases']) == 8 and len(facts['missing_requests']) == 4
records.extend(facts['cases'])
pins = dict(facts['available_neighbors'])
for record in records:
    row = originals[record['id']]
    assert row['classification'] == 'applicable' and row['expected'] == record['original_expected']
    assert row['currentStatus'] == record['original_status']
    assert set(row['documents']) == set(record['documents'])
    assert record.get('original_passed', record.get('baseline_passed')) is False
    for a, b in [('original_prior', 'priorStatus'), ('original_expected_entries', 'originalExpected')]:
        if a in record:
            assert record[a] == row[b]
    for key in ['documents', 'schema_documents', 'malformed_hint_schema']:
        for path, checksum in record.get(key, {}).items():
            assert path not in pins or pins[path] == checksum
            pins[path] = checksum
    if 'authored_hint' in record:
        hint = record['authored_hint']
        pins[hint['schema_path']] = hint['schema_sha256']
for path, checksum in pins.items():
    assert catalog[path] == checksum == hashlib.sha256((CORPUS / 'upstream' / path).read_bytes()).hexdigest()
for record in facts['missing_requests']:
    assert catalog[record['principal']] == record['principal_sha256']
assert facts['derived_a004']['original_expected'] == 'invalid' and facts['derived_a004']['derived_expected'] == 'valid'
scope_path = CORPUS / 'bcl-reference-exception-scope.json'
assert hashlib.sha256(scope_path.read_bytes()).hexdigest() == 'c5d23c50623b27486399fd46cabadc6d8ef525888a1368884147892ef1a9097c'
scope = json.loads(scope_path.read_text())
assert scope['upstream_revision'] == inventory['revision']
assert hashlib.sha256((CORPUS / 'inventory.json').read_bytes()).hexdigest() == scope['inventory_sha256']
assert hashlib.sha256((CORPUS / 'resources.json').read_bytes()).hexdigest() == scope['catalog_sha256']
assert len(scope['cases']) == len({c['row']['id'] for c in scope['cases']}) == 21
for case in scope['cases']:
    row = case['row']
    assert row == originals[row['id']]
    assert (row['kind'], row['classification'], row['expected']) == ('schema', 'applicable', 'invalid')
    assert len(row['documents']) == 1 and case['source_uri'] == 'https://w3c-xsd.test/source/' + row['documents'][0]
    assert case['historical_outcome'] == 'harness-exception' and case['historical_original_passed'] is False and case['historical_callbacks'] == 0
    for name, checksum in [(row['documents'][0], case['document_sha256']), (case['metadata'], case['metadata_sha256'])]:
        assert catalog[name] == checksum == hashlib.sha256((CORPUS / 'upstream' / name).read_bytes()).hexdigest()
    assert case['empty_id'] is True and b'id=""' in (CORPUS / 'upstream' / row['documents'][0]).read_bytes()
runtime_module = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/shared/Microsoft.NETCore.App/10.0.11/System.Private.Xml.dll')
assert hashlib.sha256(runtime_module.read_bytes()).hexdigest() == scope['bcl_module_sha256']
print(f'PASS SOURCE only: {len(list(HERE.glob("*.py")))} Python ASTs;6 identical supervisor functions;4 verified ancestors;2 exact approved copies;43 original metadata records/{len(pins)} pinned files;21 exact BCL scope rows/metadata/principal hashes/pinned module;no builds/tests/controls/corpus children')

# Independent stdlib source-data checks. Do not import report/runner code or run controls.
for name, parent, checksum in [
    ('approved-additional-profile-dispositions.json', 'M8-xsd-additional-profile-policy-pending.json', '46135607588c3421270179b7413ff3eae78b6c22a42afdd3c4dd6124fe33147e'),
    ('approved-residual-dispositions.json', 'M8-xsd-residual-policy-pending.json', 'd96a51b2dab9e776145e0f34f8eba516e1189ec77bf3130ff7a6c5c111efa2da')]:
    payload = (CORPUS / name).read_bytes()
    assert payload == (ROOT / 'docs/orchestration' / parent).read_bytes()
    assert hashlib.sha256(payload).hexdigest() == checksum
    stamp = json.loads(payload)['human_approval']
    assert stamp['question_call'] == 'call_e1d528b3cc424337ad718280d2ac189e'
additional = json.loads((CORPUS / 'approved-additional-profile-dispositions.json').read_text())
residual = json.loads((CORPUS / 'approved-residual-dispositions.json').read_text())
groups = {r['id']: g for g in inventory['groups'] for r in ([g['schema']] if g['schema'] else []) + g['instances'] + g['supplementalInstances']}
new = [r for k in ['interpretation_disagreements', 'dependent_unassessed', 'substrate_refusals', 'application_input_outcomes'] for r in additional[k]]
assert len(new) == 58 and len(residual['cases']) == 53
assert len({r['id'] for r in records[:35] + new + residual['cases']} | {'msMeta/DataTypes_w3c.xml#g119/s:time_maxInclusive007_119'}) == 147
for key, source in additional['source_records'].items():
    assert hashlib.sha256((ROOT / source['path']).read_bytes()).hexdigest() == source['sha256']

def checked_file(name, checksum):
    assert catalog[name] == checksum == hashlib.sha256((CORPUS / 'upstream' / name).read_bytes()).hexdigest()

for record in new + residual['cases']:
    row = originals[record['id']]
    assert hashlib.sha256(json.dumps(row, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()).hexdigest() == record['inventory_record_sha256']
    metadata = record['metadata']; assert metadata['path'] == groups[record['id']]['metadata']
    checked_file(metadata['path'], metadata['sha256'])
    if 'observed' in record:
        assert row['expected'] == record['original_expected'] and row['currentStatus'] == record['original_status'] and row['priorStatus'] == record['original_prior']
        assert row['originalExpected'] == record['original_expected_entries'] and [scope for scope in row['scopes'] if scope.get('version') or scope.get('edition')] == record['original_scopes']
        assert row['documents'] == list(record['documents'])
        schema = groups[record['id']]['schema']; assert (schema['documents'] if schema else []) == list(record['schema_documents'])
        for key in ['documents', 'schema_documents']:
            for name, checksum in record[key].items(): checked_file(name, checksum)
        source = record['source_record']; value = json.loads((ROOT / additional['source_records'][source['key']]['path']).read_text())
        for segment in source['json_pointer'].split('/')[1:]:
            segment = segment.replace('~1', '/').replace('~0', '~'); value = value[int(segment)] if isinstance(value, list) else value[segment]
        assert record['count_as_original_pass'] is False and record['count_as_gate_pass'] is False
        observed = record['observed']; assert observed['passed'] is False
    else:
        for key, value in record['original'].items():
            actual = [scope for scope in row['scopes'] if scope.get('version') or scope.get('edition')] if key == 'version_edition_scopes' else row[key]
            assert actual == value
        assert row['documents'] == [r['path'] for r in record['ordered_inputs']]
        schema = groups[record['id']]['schema']; assert (schema['documents'] if schema else []) == [r['path'] for r in record['ordered_schema_inputs']]
        for key in ['ordered_inputs', 'ordered_schema_inputs', 'referenced_inputs']:
            for file in record[key]:
                checked_file(file['path'], file['sha256']); assert (CORPUS / 'upstream' / file['path']).stat().st_size == file['bytes']
        assert record['original_pass_credit'] is False and record['gate_pass_credit'] is False
        observed = record['observation']['summary']; assert observed['passed'] is False or observed['passed'] is None
    assert observed['classification'] == row['classification'] == 'applicable'
    assert observed['kind'] == row['kind'] and observed['expected'] == row['expected']
assert sum(r['observation']['summary']['passed'] is None for r in residual['cases']) == 11
import xml.etree.ElementTree as ET
obligations = json.loads((CORPUS / 'optional-adapter-obligations.json').read_text())
assert hashlib.sha256((CORPUS / 'optional-adapter-obligations.json').read_bytes()).hexdigest() == '818049ca3ef5f29cb83eb8817a1442906a35757342cbf855185b4801f869212f'
assert hashlib.sha256((ROOT / obligations['source_manifest']).read_bytes()).hexdigest() == obligations['source_manifest_sha256']
assert len(obligations['cases']) == 13 and sum(c['mapping_active'] for c in obligations['cases']) == 12
for case in obligations['cases']:
    assert case['schema_row'] == originals[case['id']]
    checked_file(case['metadata'], case['metadata_sha256'])
    for name, checksum in case['documents'].items(): checked_file(name, checksum)
    assert case['mapping_active'] == ('anyURI_a001_1336' not in case['id'])
    for callback in case['candidate_absence_bindings']:
        checked_file(callback['principal'], callback['principal_sha256'])
        assert callback['referrer'] == 'https://w3c-xsd.test/source/' + callback['principal'] and callback['kind'] == 'Schema'
        elements = ET.parse(CORPUS / 'upstream' / callback['principal']).getroot()
        kinds = ['import', 'redefine'] if callback['directive'] == 'import and empty redefine' else [callback['directive']]
        if len(kinds) == 2: assert case['mapping_active'] is False
        directives = [e for e in elements if e.tag in ['{http://www.w3.org/2001/XMLSchema}' + kind for kind in kinds] and e.get('schemaLocation') == callback['systemId']]
        assert len(directives) == len(kinds) and callback['substantiveRedefine'] is False
        for directive in directives:
            if directive.tag.endswith('}redefine'): assert all(e.tag == '{http://www.w3.org/2001/XMLSchema}annotation' for e in directive)
        if callback['resolvedUri'].startswith('https://w3c-xsd.test/source/'):
            from urllib.parse import unquote
            name = unquote(callback['resolvedUri'][len('https://w3c-xsd.test/source/'):]); assert name not in catalog
            folder = CORPUS / 'upstream' / Path(name).parent
            assert Path(name).name not in {p.name for p in folder.iterdir()}
supplements = json.loads((CORPUS / 'application-supplemental-inputs.json').read_text())
assert hashlib.sha256((CORPUS / 'application-supplemental-inputs.json').read_bytes()).hexdigest() == 'dd4d7c529119a5ab0f914f332eccd7c778e48fecb204f76113edb16ccdafa1d4'
assert len(supplements['xml_groups']) == 2 and sum(len(g['rows']) for g in supplements['xml_groups']) == 4
for group in supplements['xml_groups']:
    for row in group['rows']: assert row == originals[row['id']]
    checked_file(group['metadata']['path'], group['metadata']['sha256'])
    for name, checksum in group['documents'].items(): checked_file(name, checksum)
late = supplements['late_hint']; assert late['original_row'] == originals['msMeta/Additional_w3c.xml#g166/i0:addB163.v']
assert late['original_row']['expected'] == 'valid' and late['original_observation']['passed'] is False
assert [Path(f['path']).name for f in late['ordered_schema_inputs']] == ['test93490_5.xsd', 'test93490_1.xsd', 'test93490_3.xsd']
for file in late['ordered_schema_inputs'] + [late['instance']]: checked_file(file['path'], file['sha256'])
xml = supplements['xml_resource']; assert hashlib.sha256((CORPUS / xml['path']).read_bytes()).hexdigest() == xml['sha256']
api = json.loads((HERE / 'verified-api-preparation.json').read_text()); manifest = ROOT / api['source_manifest']
assert api['final_freeze_ready'] is True and api['verified_bindings_ready'] is True
assert api['execution_authorization'].startswith('HOLD;')
assert hashlib.sha256(manifest.read_bytes()).hexdigest() == api['source_manifest_sha256'] == '5ae428bf0ac9f8c60df880322546c7995b52039807db9802a6d57d4ed170f255'
assert len(api['apiInputs']) == 55 and api['apiInputs'] == json.loads(manifest.read_text())['apiInputs']
assert set(api['apiInputs']) == {str(p.relative_to(ROOT)) for p in (ROOT / 'docs/api').glob('*.txt')}
public = {}
for name, checksum in api['apiInputs'].items():
    assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == checksum
    for line in (ROOT / name).read_text().splitlines()[1:]:
        if line:
            row = json.loads(line); assert row['id'] not in public or public[row['id']] == row; public[row['id']] = row
assert len(public) == 1430 and (ROOT / 'docs/api/M8-xsd-missing-components.txt').read_bytes() == b'xemele-api-v1\n'
for file, key in [('runtime_source_manifest','runtime_source_manifest_sha256'),('runtime_review_plan','runtime_review_plan_sha256'),('runtime_api_proof','runtime_api_proof_sha256'),('repair_proof','repair_proof_sha256'),('verified_task','verified_task_sha256'),('integration_record','integration_record_sha256')]:
    assert hashlib.sha256((ROOT / api[file]).read_bytes()).hexdigest() == api[key]
proof = json.loads((ROOT / api['runtime_api_proof']).read_text()); record = json.loads((ROOT / api['integration_record']).read_text())
repair = json.loads((ROOT / api['repair_proof']).read_text())
assert record['result'] == 'passed' and record['summary']['completed'] is True and record['commands'] == 24
assert record['summary']['sourceSha256'] == api['source_manifest_sha256']
assert record['full93EqualVerified'] is True and repair['actual_full93_equal_verified'] is True
assert repair['annotation_waiver'] is False and record['originalAnnotation'] == repair['original_annotation']
assert record['summary']['tests']['coverage']['observedMethods'] == 704 and record['summary']['tests']['coverage']['observedCases'] == 2222
assert repair['verified_task_sha256'] == api['verified_task_sha256'] == 'e4fbcc6f346f2aef2d40156a611e0ef4b4b329dab9629aed8448da20a9f08990'
assert repair['integration_record_sha256'] == api['integration_record_sha256'] == '1e0c31ebe946e50bb5166a4682249561d2b3645d0680d3d80a722e8c4c19206a'
assert api['runtime_production_inputs']['src/Xemele/Validation/Xsd/Compilation/Availability/Availability.cs'] == 'c5ccdab54580f735bf030906951fe2b7ca02ae163036cf98963a53f2888db1e3'
for name, checksum in [(repair['canonical_projection'],repair['canonical_projection_sha256']),(repair['allocation_custody_record'],repair['allocation_custody_record_sha256'])]: assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == checksum
historical = api['historical_missing_binding']
for path_key, sha_key in [('source_manifest','source_manifest_sha256'),('runtime_source_manifest','runtime_source_manifest_sha256'),('runtime_review_plan','runtime_review_plan_sha256'),('verified_task','verified_task_sha256'),('integration_record','integration_record_sha256')]: assert hashlib.sha256((ROOT / historical[path_key]).read_bytes()).hexdigest() == historical[sha_key]
prior_record = json.loads((ROOT / historical['integration_record']).read_text())
assert proof['canonical_added_projection'] == prior_record['summary']['addedInternalProjection']
canonical = 'xemele-api-v1\n' + ''.join(json.dumps(proof['canonical_added_projection'][k], ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n' for k in sorted(proof['canonical_added_projection']))
assert hashlib.sha256(canonical.encode()).hexdigest() == proof['canonical_added_api_sha256'] == '33a4823adf25f638d0e52990918f859c2b64a35359d25aa59c318f4543a1812d'
assert proof['released_check_sha256'] == prior_record['receipts']['checks/checks-summary.json']['sha256'] == 'c516fdbae022bf65148a2c420b0412fcd4f1418adacde540bbc48eb0dee95105'
projection = proof['canonical_added_projection']; types = {name[2:] for name in projection if name.startswith('T:')}
type_rows = {k:v for k,v in projection.items() if any(k[2:] == name or k[2:].startswith(name + '.') for name in types)}
joins = {k:v for k,v in projection.items() if k not in type_rows}
assert len(types) == 11 and len(type_rows) == 80 and len(joins) == 13 and len(projection) == 93
assert joins == {r['id']:r for r in map(json.loads,(ROOT / 'docs/reports/M8-xsd-missing-components/added-joins.golden.txt').read_text().splitlines()[1:])}
for name, checksum in api['baselines_sha256'].items(): assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == checksum
for baseline in proof['nullable_baselines']:
    before = subprocess.run(['git','show',baseline['before_revision'] + ':' + baseline['path']], cwd=ROOT, capture_output=True, check=True, timeout=5).stdout
    assert hashlib.sha256(before).hexdigest() == baseline['before_sha256']
    old = {r['id']:r for r in map(json.loads,before.decode().splitlines()[1:])}
    new = {r['id']:r for r in map(json.loads,(ROOT / baseline['path']).read_text().splitlines()[1:])}
    assert len(new) == len(old) == baseline['rows'] and baseline['rows'] in (615,257)
    assert {k:v for k,v in old.items() if new.get(k) != v} == baseline['before_changes']
    assert {k:v for k,v in new.items() if old.get(k) != v} == baseline['after_changes']
    assert len(baseline['before_changes']) == len(baseline['after_changes']) == 3
    assert all('XsdElementDeclaration' in k and ('.#ctor(' in k or '.Deconstruct(' in k or k.endswith('.Type')) for k in baseline['after_changes'])
assert set(api['runtime_production_inputs']) == {str(p.relative_to(ROOT)) for p in (ROOT / 'src/Xemele').rglob('*') if p.is_file() and p.suffix in ('.cs','.csproj','.props','.targets') and not {'bin','obj'}.intersection(p.relative_to(ROOT).parts)}
for name, checksum in api['runtime_production_inputs'].items(): assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == checksum
for name, present in api['root_configuration_presence'].items(): assert (ROOT / name).exists() is present
for name, checksum in api['reviewed_build_inputs'].items(): assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == checksum
missing_task = json.loads((ROOT / api['verified_task']).read_text()); assert missing_task['status'] == 'verified' and missing_task['integration_revision'] == api['verified_integration'] == record['testedRevision']
assert record['originalDispositionPassCredit'] == record['gatePassCredit'] == 0 and repair['annotation_waiver'] is False
assert proof['actual_root_tests']['nonassessmentControls'] == 4 and proof['actual_root_tests']['nonassessmentGateCredit'] == proof['actual_root_tests']['nonassessmentOriginalPassCredit'] == 0
subprocess.run(['git','merge-base','--is-ancestor',api['verified_integration'],'HEAD'],cwd=ROOT,check=True,timeout=5)
import_task = json.loads((ROOT / 'docs/tasks/M8-xsd-import-visibility.json').read_text()); assert import_task['status'] == 'verified'
subprocess.run(['git','merge-base','--is-ancestor',import_task['integration_revision'],'HEAD'],cwd=ROOT,check=True,timeout=5)
root_task = subprocess.run(['git','show','6da00ef1b9a1f16aef7a8768f230ce9ca2c8bc91:docs/tasks/M8-xsd-corpus.json'], cwd=ROOT, capture_output=True,check=True,timeout=5).stdout
assert (ROOT / 'docs/tasks/M8-xsd-corpus.json').read_bytes() == root_task
assert hashlib.sha256((ROOT / 'tests/Xemele.Differential.Tests/Xsd/BclEngine.cs.fixture').read_bytes()).hexdigest() == 'b3000849833db1eac95b322f234a183c5e4cc3c1ec39945f9dcc95dedad1d664'
original_runner = subprocess.run(['git','show','f05c780328613d7b0fec89ba14462eea8a4998cc:tests/Xemele.Conformance.Tests/Xsd/Runner.cs.fixture'],cwd=ROOT,capture_output=True,text=True,check=True,timeout=5).stdout
current_runner = (ROOT / 'tests/Xemele.Conformance.Tests/Xsd/Runner.cs.fixture').read_text()
def method(source, name):
    start = source.index('    internal static bool ' + name + '('); brace = source.index('{', start); depth = 1; end = brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}'); end += 1
    return source[start:end]
for name in ['VerifiedOptionalMissing','VerifiedHintOrdering']: assert method(original_runner,name) == method(current_runner,name)
print('PASS follow-up SOURCE data/AST only: four exact approval copies/147 unique dispositions (11null);13 obligations/12active;2 XML groups/4original IDs+onlyaddB163 supplement;55 verified API fragments/public1430/93added exact projection/3nullable rows each/238production bindings;old4/7 methods and complete BCL fixture unchanged;all executable/new native controls UNRUN;root source review/execution release held')

accepted_run = ast.parse(subprocess.run(['git','show','8b383532239f22be19a809acc95dea50a2b8fbc1:docs/reports/M8-xsd-corpus/run.py'],cwd=ROOT,capture_output=True,text=True,check=True,timeout=5).stdout)
current_run = ast.parse((HERE / 'run.py').read_text())
for name in ['preparation_commands','verify_preparation_bindings','prepare','campaign','execute_cases']:
    assert ast.dump(function(current_run),include_attributes=False) == ast.dump(function(accepted_run),include_attributes=False)
plan = json.loads((HERE / 'final-execution-recipes.json').read_text())
assert len(plan['preparation']['children']) == plan['preparation']['expected_children'] == 10
assert len(plan['full_campaign']['children']) == plan['full_campaign']['expected_children'] == 186
assert len(plan['supplements']['children']) == plan['supplements']['expected_children'] == 3
assert (plan['preparation']['intrinsic_seconds'],plan['full_campaign']['intrinsic_seconds'],plan['supplements']['intrinsic_seconds']) == (1800,3600,900)
for index, (engine,testset) in enumerate((e,t) for e in ['product','bcl'] for t in inventory['testSets']):
    child = plan['full_campaign']['children'][index]; assert child[-3:-1] == [testset,engine]
assert sum(len(([g['schema']] if g['schema'] else []) + g['instances'] + g['supplementalInstances']) for g in inventory['groups']) == plan['full_campaign']['rows_per_engine'] == 41736
caps_node = next(n.value for n in current_run.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='CAPS' for t in n.targets))
assert plan['full_campaign']['effective_limits'] == {k.arg:ast.literal_eval(k.value) for k in caps_node.keywords}
assert hashlib.sha256((HERE / 'controls.py').read_bytes()).hexdigest() == 'e63bbe537edf275a0ce64ffdb75e8716a5c972ddeee6b68bbfc23ef9cae8b69b'
assert hashlib.sha256((HERE / 'nested_ownership.py').read_bytes()).hexdigest() == 'fe1db7d70cc4b10a04aaa5ac8107cd69a95f7a16e21a0888892ef70c5f05a128'
print('PASS final SOURCE packet: unchanged ten-child preparation/freeze/campaign/execute functions and caps; exact10+186+3 recipes;root task bytes unchanged;new runtime/API/supplement controls all UNRUN;no project imports or launches')

latest_ids = []
provenance = json.loads((CORPUS / 'approved-missing-observation-provenance.json').read_text())
assert hashlib.sha256((CORPUS / 'approved-missing-observation-provenance.json').read_bytes()).hexdigest() == '686da8bbf6913f791b10aee7909643c90be884b122e3e9d95bb4ede8c72c8284'
for approval, prepared, question, approval_sha, prepared_sha, parent in [
    ('approved-missing-nonassessment-dispositions.json','prepared-missing-nonassessment-dispositions.json','call_2d4e05aaef41499293e682fe147c9db5','4b1cb646d5386a08b6ca2c259039b61497bc6e1e88472c5f56aea3c239c1d659','06a4851a1c1e4653c7a87ab9018046e1d14a8be0085aeeb9cb6567d7d93f6f7d','M8-xsd-missing-nonassessment-policy'),
    ('approved-missing-schema-dispositions.json','prepared-missing-schema-dispositions.json','call_d75eafbe87064aa3a95b17175cf85308','f8593c8becc78682aca37ec4b1560b0f03a757948ecf5bc14a41257f12300688','42b8a89542e7f94b7c1b9f2ad71671ef6c688a3ca92a0aa2e2f8c367e4420ddb','M8-xsd-missing-schema-oracles-policy')]:
    assert (CORPUS / approval).read_bytes() == (ROOT / 'docs/orchestration' / (parent + '-approved.json')).read_bytes()
    assert (CORPUS / prepared).read_bytes() == (ROOT / 'docs/orchestration' / (parent + '-pending.json')).read_bytes()
    assert hashlib.sha256((CORPUS / approval).read_bytes()).hexdigest() == approval_sha
    assert hashlib.sha256((CORPUS / prepared).read_bytes()).hexdigest() == prepared_sha
    stamp = json.loads((CORPUS / approval).read_text()); prepared_doc = json.loads((CORPUS / prepared).read_text())
    assert stamp['approvalQuestionId'] == question and stamp['approvedPreparedRecordSha256'] == prepared_sha
    assert stamp['counts']['originalFalse'] == 4 and stamp['counts']['originalPassCredit'] == stamp['counts']['gatePassCredit'] == 0
    assert prepared_doc['sourceRevision'] == provenance['sourceRevision'] == '91fbc8473ff43c8229acc1387f597645b6215175'
    assert prepared_doc['frozenCampaignManifestSha256'] == provenance['frozenManifestSha256'] == '404bc79b127746fed960270717a994df123b06f9856704f9cc0c6d21ac8b6145'
    assert len(prepared_doc['originals']) == 4
    for record in prepared_doc['originals']:
        row = originals[record['id']]; latest_ids.append(record['id'])
        assert hashlib.sha256(json.dumps(row,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest() == record['originalMetadataSha256']
        assert groups[record['id']]['metadata'] == record['metadataFile']; checked_file(record['metadataFile'],record['metadataFileSha256'])
        assert record['originalExpected'] == row['expected'] and record['originalPassCredit'] is False and record['gatePassCredit'] is False
        files = record.get('schemaAndInstanceSha256',record.get('schemaDocumentsSha256'))
        for name, checksum in files.items(): checked_file(name,checksum)
        if row['kind'] == 'instance': assert list(files) == groups[record['id']]['schema']['documents'] + row['documents']
        else: assert [name for name in files if name in row['documents']] == row['documents'] and row['currentStatus'] == record['originalCurrentStatus']
        observed = record['observation']; assert observed['engine'] == 'product' and observed['classification'] == 'applicable' and observed['passed'] is False
        assert observed['kind'] == row['kind'] and observed['expected'] == row['expected']
assert len(set(latest_ids)) == 8
latest_additional = [r for key in ['interpretation_disagreements','dependent_unassessed','substrate_refusals','application_input_outcomes'] for r in additional[key]]
all_approved_ids = {r['id'] for r in records[:35] + latest_additional + residual['cases']} | {'msMeta/DataTypes_w3c.xml#g119/s:time_maxInclusive007_119'} | set(latest_ids)
assert len(all_approved_ids) == 155 and 'msMeta/Annotations_w3c.xml#g78/s:annotZ002' not in all_approved_ids
assert 136 + len(latest_ids) == 144 and sum(r['observation']['summary']['passed'] is None for r in residual['cases']) == 11
print('PASS current rebind SOURCE only:155 exact scopes/144false+11null;four new authority/prepared copies and historical provenance pinned;annotZ002 UNWAIVED/current verified ordinary-cap repair;55/full93/nullable baselines preserved;all execution UNRUN')
