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
print(f'PASS SOURCE only: {len(list(HERE.glob("*.py")))} Python ASTs;6 identical supervisor functions;4 verified ancestors;2 exact approved copies;43 original metadata records/{len(pins)} pinned files;no builds/tests/controls/corpus children')
