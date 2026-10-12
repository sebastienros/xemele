#!/usr/bin/env python3
"""Independent original metadata, fixture hashes and exact authored callback source checks."""
import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CORPUS = ROOT / 'tests/corpora/xsd'
FACTS = json.loads((CORPUS / 'verified-optional-inputs.json').read_text())
INVENTORY = json.loads((CORPUS / 'inventory.json').read_text())
CATALOG = {r['path']: r['sha256'] for r in json.loads((CORPUS / 'resources.json').read_text())}
ROWS = {r['id']: r for g in INVENTORY['groups']
        for r in ([g['schema']] if g['schema'] else []) + g['instances'] + g['supplementalInstances']}
assert FACTS['upstream_revision'] == INVENTORY['revision'] == '7bc3365c652a322f3d762021b3879eb92dae7e30'
assert hashlib.sha256((ROOT / FACTS['source_manifest']).read_bytes()).hexdigest() == FACTS['source_manifest_sha256']
assert len(FACTS['cases']) == 8 and len(FACTS['missing_requests']) == 4
for original in FACTS['cases']:
    row = ROWS[original['id']]
    assert row['kind'] == original['kind'] and row['expected'] == original['original_expected']
    assert row['originalExpected'] == original['original_expected_entries']
    assert row['currentStatus'] == original['original_status'] and row['priorStatus'] == original['original_prior']
    assert set(row['documents']) == set(original['documents'])
    assert original['baseline_passed'] is False
    for name, sha in original['documents'].items():
        assert CATALOG[name] == sha == hashlib.sha256((CORPUS / 'upstream' / name).read_bytes()).hexdigest()
for missing in FACTS['missing_requests']:
    name = missing['principal']
    assert missing['principal_sha256'] == CATALOG[name]
    principal = ET.parse(CORPUS / 'upstream' / name).getroot()
    references = [child.attrib['schemaLocation'] for child in principal
                  if child.tag in ['{http://www.w3.org/2001/XMLSchema}'+kind for kind in ['include','import','redefine']]]
    assert references.count(missing['system_id']) == (3 if 'anyURI_' in name else 1)
    if missing['system_id'].startswith('http:'):
        externals = json.loads((CORPUS / 'external-resources.json').read_text())
        assert not any(missing['system_id'] in r['urls'] for r in externals)
    else:
        relative = str(Path(name).parent / missing['system_id'])
        assert relative not in CATALOG and not (CORPUS / 'upstream' / relative).exists()
for name, sha in FACTS['available_neighbors'].items():
    assert CATALOG[name] == sha == hashlib.sha256((CORPUS / 'upstream' / name).read_bytes()).hexdigest()
assert FACTS['derived_a004']['original_expected'] == 'invalid'
assert FACTS['derived_a004']['derived_expected'] == 'valid'
assert FACTS['derived_a004']['source'] == 'https://www.w3.org/Bugs/Public/show_bug.cgi?id=4126'
print('PASS exact8 original metadata/10 fixture hashes/4 authored absence bindings; original baselines/oracles retained')
