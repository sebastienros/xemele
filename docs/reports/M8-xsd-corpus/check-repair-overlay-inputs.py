#!/usr/bin/env python3
"""Check exact integrated repair inputs and pinned source facts, without engines."""
import hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CORPUS = ROOT / 'tests/corpora/xsd'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    facts = json.loads((CORPUS / 'verified-repair-overlay-inputs.json').read_text())
    inventory = json.loads((CORPUS / 'inventory.json').read_text())
    originals = {r['id']: r for g in inventory['groups']
                 for r in ([g['schema']] if g['schema'] else []) + g['instances']}
    for name, sha in facts['source_manifests'].items():
        assert digest(ROOT / name) == sha, name
    mapping = json.loads((ROOT / 'tests/fixtures/M8-xsd-component-mapping/manifest.json').read_text())
    instance = json.loads((ROOT / 'tests/fixtures/M8-xsd-instance-assessment/provenance.json').read_text())
    matcher = json.loads((ROOT / 'tests/fixtures/M8-xsd-counted-matcher/originals.json').read_text())
    expected = {c['id'] for c in mapping['witnesses']}
    expected.update(f"{c['metadata']}#g{c['groupIndex']}/i{c['instanceIndex']}:{c['name']}" for c in instance['cases'])
    expected.update(c['id'] for g in matcher['originals'] for c in g['instances'])
    assert len(expected) == 22
    assert len(facts['targets']) == 22 and {c['id'] for c in facts['targets']} == expected
    assert len(facts['schema_prerequisites']) == 16
    cases = facts['targets'] + facts['schema_prerequisites']
    assert len({c['id'] for c in cases}) == 38
    resources = {r['path']: r['sha256'] for r in json.loads((CORPUS / 'resources.json').read_text())}
    for case in cases:
        row = originals[case['id']]
        assert row['kind'] == case['kind'] and row['expected'] == case['original_expected']
        assert row['originalExpected'] == case['original_expected_entries']
        assert row['currentStatus'] == case['current_status'] and row['priorStatus'] == case['prior_status']
        assert set(row['documents']) == set(case['documents'])
        for name, sha in case['documents'].items():
            assert digest(CORPUS / 'upstream' / name) == sha == resources[name], name
    for case in mapping['files']:
        assert digest(ROOT / 'tests/fixtures/M8-xsd-component-mapping' / case['file']) == case['sha256']
        assert resources[case['source']] == case['sha256']
    for case in instance['cases']:
        for file in case['files']:
            assert digest(ROOT / 'tests/fixtures/M8-xsd-instance-assessment' / file['local']) == file['sha256']
            assert resources[file['source']] == file['sha256']
    for group in matcher['originals']:
        for file in group['schema'] + group['instances']:
            assert digest(ROOT / 'tests/fixtures/M8-xsd-counted-matcher' / file['path']) == file['sha256']
            assert resources[file['path']] == file['sha256']
        for case in group['instances']:
            row = originals[case['id']]
            assert row['originalExpected'] == case['expected'] and row['currentStatus'] == case['current']
    profiles = {c['id']: c.get('derivedExpected', c.get('profileExpected', c['originalExpected'])) for c in mapping['witnesses']}
    for case in facts['targets']:
        assert case['profile_expected'] == profiles.get(case['id'], case['original_expected'])
    assert len({p for c in cases for p in c['documents']}) == 35
    print('PASS three integrated manifests, 22 exact targets/16 schema predicates, 35 pinned original files, original expectations/statuses and separate profile oracles.')

if __name__ == '__main__':
    main()
