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
assert api['final_freeze_ready'] is False and hashlib.sha256(manifest.read_bytes()).hexdigest() == api['source_manifest_sha256'] == 'f9ee117ee6fbe4326c7260668ecf67a90d370d185d782acb130dc577c07601a8'
assert len(api['apiInputs']) == 54 and api['apiInputs'] == json.loads(manifest.read_text())['apiInputs']
assert set(api['apiInputs']) == {str(p.relative_to(ROOT)) for p in (ROOT / 'docs/api').glob('*.txt')}
for name, checksum in api['apiInputs'].items(): assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == checksum
import_task = json.loads((ROOT / 'docs/tasks/M8-xsd-import-visibility.json').read_text()); assert import_task['status'] == 'verified'
subprocess.run(['git','merge-base','--is-ancestor',api['verified_integration'],'HEAD'],cwd=ROOT,check=True,timeout=5)
assert hashlib.sha256((ROOT / 'tests/Xemele.Differential.Tests/Xsd/BclEngine.cs.fixture').read_bytes()).hexdigest() == 'b3000849833db1eac95b322f234a183c5e4cc3c1ec39945f9dcc95dedad1d664'
original_runner = subprocess.run(['git','show','f05c780328613d7b0fec89ba14462eea8a4998cc:tests/Xemele.Conformance.Tests/Xsd/Runner.cs.fixture'],cwd=ROOT,capture_output=True,text=True,check=True,timeout=5).stdout
current_runner = (ROOT / 'tests/Xemele.Conformance.Tests/Xsd/Runner.cs.fixture').read_text()
def method(source, name):
    start = source.index('    internal static bool ' + name + '('); brace = source.index('{', start); depth = 1; end = brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}'); end += 1
    return source[start:end]
for name in ['VerifiedOptionalMissing','VerifiedHintOrdering']: assert method(original_runner,name) == method(current_runner,name)
print('PASS follow-up SOURCE data/AST only: four exact approval copies/147 unique dispositions (11null);13 obligations/12active;2 XML groups/4original IDs+onlyaddB163 supplement;54 verified API fragments/membership;old4/7 methods and complete BCL fixture unchanged;all executable/new native controls UNRUN;final freeze held')
