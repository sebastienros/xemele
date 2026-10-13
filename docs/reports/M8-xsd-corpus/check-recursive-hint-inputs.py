#!/usr/bin/env python3
"""Check immutable original/profile inputs independently of semantic execution."""
import hashlib, json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
CORPUS = ROOT / 'tests/corpora/xsd'
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    facts = json.loads((CORPUS / 'recursive-hint-overlay-inputs.json').read_text())
    inventory = json.loads((CORPUS / 'inventory.json').read_text())
    rows = {r['id']: r for g in inventory['groups'] for r in ([g['schema']] if g['schema'] else []) + g['instances']}
    resources = {r['path']: r['sha256'] for r in json.loads((CORPUS / 'resources.json').read_text())}
    for name, sha in facts['source_manifests'].items(): assert digest(ROOT / name) == sha
    recursive = json.loads((ROOT / 'tests/fixtures/M8-xsd-recursive-components/manifest.json').read_text())
    hint_path = ROOT / 'tests/fixtures/M8-xsd-instance-hint-ordering/provenance.json'
    hint = json.loads(hint_path.read_text())
    assert hint_path.read_bytes() == (CORPUS / 'verified-hint-ordering-profile.json').read_bytes()
    paths = {f['source'] for f in recursive['files'] if f['upstreamValidity'] is not None}
    recursive_ids = {id for id, r in rows.items() if len(r['documents']) == 1 and r['documents'][0] in paths}
    hint_ids = {c['id'] for c in hint['cases']}
    assert len(recursive_ids) == 4 and len(hint_ids) == 7
    assert len(facts['targets']) == 11 and {c['id'] for c in facts['targets']} == recursive_ids | hint_ids
    for case in facts['targets']:
        row = rows[case['id']]
        assert row['expected'] == case['original_expected'] and row['originalExpected'] == case['original_expected_entries']
        assert row['currentStatus'] == case['current_status'] and row['priorStatus'] == case['prior_status']
        assert set(row['documents']) == set(case['documents'])
        for name, sha in case['documents'].items(): assert digest(CORPUS / 'upstream' / name) == sha == resources[name]
        if case['id'] in hint_ids:
            approved = next(c for c in hint['cases'] if c['id'] == case['id'])
            assert case['documents'] == approved['documents'] and case['original_expected'] == 'invalid'
            first = case['first_schema_inputs']
            assert len(first) in [1, 2, 3]
            order = [(5, ''), (1, 'foo'), (3, 'bar')] if len(first) == 3 else [(1, 'foo'), (3, 'bar')][:len(first)]
            for item, (number, namespace) in zip(first, order):
                assert item['path'] == 'msData/additional/test93490_' + str(number) + '.xsd'
                assert item['namespace'] == namespace
                assert item['sha256'] == hint['authored_schema_pins'][item['path']]
    for name, sha in facts['authored_schema_pins'].items():
        assert sha == hint['authored_schema_pins'][name] == resources[name] == digest(CORPUS / 'upstream' / name)
        assert digest(ROOT / 'tests/fixtures/M8-xsd-instance-hint-ordering' / Path(name).name) == sha
    for file in recursive['files']:
        assert digest(ROOT / 'tests/fixtures/M8-xsd-recursive-components' / file['file']) == file['sha256'] == resources[file['source']]
    print('PASS exact 4 recursive/7 hint inputs, original statuses/oracles, pinned copied provenance and ordered first-schema hashes; no semantic execution.')
if __name__ == '__main__': main()
