#!/usr/bin/env python3
"""Verify original metadata and full hinted XML bytes, without a validity oracle."""
import hashlib, json, xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CORPUS = ROOT / 'tests/corpora/xsd'

def main():
    facts = json.loads((CORPUS / 'resource-prerequisite-triage.json').read_text())
    inventory = json.loads((CORPUS / 'inventory.json').read_text())
    originals = {r['id']: r for g in inventory['groups']
                 for r in ([g['schema']] if g['schema'] else []) + g['instances']}
    cycles = facts['indeterminate_cycles']
    malformed = facts['malformed_hint_prerequisites']
    gaps = facts['optional_nonresolution_gaps']
    cases = cycles + malformed + gaps
    assert (len(cycles), len(malformed), len(gaps)) == (6, 2, 8)
    assert len({c['id'] for c in cases}) == 16
    assert facts['upstream_revision'] == inventory['revision']
    for case in cases:
        row = originals[case['id']]
        assert row['expected'] == case['original_expected']
        assert row['originalExpected'] == case['original_expected_entries']
        assert row['currentStatus'] == case['original_status']
        assert row['priorStatus'] == case['original_prior']
        assert set(row['documents']) == set(case['documents'])
        assert case['baseline_passed'] is False
        for name, expected in (case['documents'] | case.get('malformed_hint_schema', {})).items():
            assert hashlib.sha256((CORPUS / 'upstream' / name).read_bytes()).hexdigest() == expected, name
    for case in cycles:
        assert case['baseline_codes'] == ['SCHEMA.ResourceCycle']
        assert case['baseline_actual'] == ('incomplete-refusal' if case['kind'] == 'schema' else 'schema-unavailable')
        if case['kind'] == 'schema':
            assert case['original_expected'] == 'indeterminate'
            assert any('4136' in s.get('bugzilla', '') for s in case['original_status'])
    for case in malformed:
        assert case['baseline_actual'] == 'schema-unavailable'
        path = CORPUS / 'upstream' / next(iter(case['malformed_hint_schema']))
        try:
            ET.parse(path)
        except ET.ParseError as failure:
            assert str(failure) == case['independent_syntax_failure']
            print(case['id'], 'full hinted-schema parse failure:', failure)
        else:
            raise AssertionError('Hinted original unexpectedly parsed: ' + case['id'])
    for case in gaps:
        assert case['baseline_codes'] == ['SCHEMA.ResourceDenied']
        assert case['baseline_actual'] == ('incomplete-refusal' if case['kind'] == 'schema' else 'schema-unavailable')
    print('PASS 16 original metadata/status/hash records, three indeterminate schemas, two full malformed hints; eight optional nonresolution gaps retain original nonpasses.')

if __name__ == '__main__':
    main()
