"""Exact human-approved gate accounting; never alters or promotes an original outcome."""
import functools
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CORPUS = ROOT / 'tests/corpora/xsd'
PIN = '7bc3365c652a322f3d762021b3879eb92dae7e30'
TIME = 'msMeta/DataTypes_w3c.xml#g119/s:time_maxInclusive007_119'
APPROVAL = 'call_dcdabbc481104ee2b07f5e4a5f8ba308'
FILES = {
    'approved-standards-dispositions.json': 'b44cec9757982c7d53cc0555bfc192e6c6172bda958e5a1b8765ca97c7cad4e0',
    'approved-input-limitations.json': 'e82338d5eabd52590a4b0b25e679720edc318795ca216cae99a65d3de51c4063'}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


@functools.lru_cache(maxsize=1)
def approved_sources():
    inventory = json.loads((CORPUS / 'inventory.json').read_text())
    assert inventory['revision'] == PIN
    catalog = {r['path']: r['sha256'] for r in json.loads((CORPUS / 'resources.json').read_text())}
    originals = {r['id']: r for g in inventory['groups']
                 for r in ([g['schema']] if g['schema'] else []) + g['instances'] + g['supplementalInstances']}
    inputs = {}
    for name, checksum in FILES.items():
        assert sha(CORPUS / name) == checksum, f'approved document changed: {name}'
        document = json.loads((CORPUS / name).read_text())
        assert document['upstream_revision'] == PIN
        assert document['status'].startswith('HUMAN APPROVED exact documented ')
        assert document['human_approval']['question_id'] == APPROVAL
        assert document['human_approval']['scope'] == 'Exact prepared cases only; no original result changes or pass credit'
        inputs[name] = document
    standards = inputs['approved-standards-dispositions.json']
    limitations = inputs['approved-input-limitations.json']
    assert [len(standards[n]) for n in ('schema_cases', 'instance_cases', 'dependent_unassessed_instances')] == [13, 1, 9]
    assert [len(limitations[n]) for n in ('fatal_xml', 'malformed_hint_prerequisites', 'indeterminate_cycles')] == [3, 2, 6]
    records = {}

    def add(record, category, actual, codes=None):
        original = originals[record['id']]
        assert record['id'] not in records
        assert original['classification'] == 'applicable'
        assert original['expected'] == record['original_expected']
        assert original['currentStatus'] == record['original_status']
        assert set(original['documents']) == set(record['documents'])
        for key, value in [('original_prior', 'priorStatus'), ('original_expected_entries', 'originalExpected')]:
            if key in record:
                assert record[key] == original[value]
        assert record.get('original_passed', record.get('baseline_passed')) is False
        for key in ('documents', 'schema_documents', 'malformed_hint_schema'):
            for name, checksum in record.get(key, {}).items():
                assert catalog[name] == checksum and sha(CORPUS / 'upstream' / name) == checksum
        if 'authored_hint' in record:
            hint = record['authored_hint']
            assert catalog[hint['schema_path']] == hint['schema_sha256']
            assert sha(CORPUS / 'upstream' / hint['schema_path']) == hint['schema_sha256']
        records[record['id']] = dict(category=category, expected=record['original_expected'],
                                    kind=original['kind'], actual=actual, codes=codes)

    for name in ('schema_cases', 'instance_cases'):
        for record in standards[name]:
            add(record, 'standards-original-disagreement', record['current_product_outcome'],
                [] if record['current_product_outcome'].endswith('-valid') else ['XSD.SchemaRestriction'])
    for record in standards['dependent_unassessed_instances']:
        assert record['instance_assessed'] is False
        add(record, 'standards-dependent-unassessed', record['current_product_outcome'], ['XSD.SchemaRestriction'])
    for record in limitations['fatal_xml']:
        add(record, 'fatal-XML-unassessed', record['baseline_actual'], ['SCHEMA.Syntax'])
    record = limitations['namespace_guard']
    add(record, 'namespace-hint-unassessed', record['baseline_actual'], [])
    for record in limitations['malformed_hint_prerequisites']:
        add(record, 'malformed-hint-prerequisite-unassessed', record['baseline_actual'], record['baseline_codes'])
    for record in limitations['indeterminate_cycles']:
        add(record, 'indeterminate-resource-cycle' if record['kind'] == 'schema' else 'cycle-dependent-unassessed',
            record['baseline_actual'], record['baseline_codes'])
    assert len(records) == 35
    time_record = json.loads((CORPUS / 'standards-dispositions.json').read_text())['dispositions'][0]
    assert time_record['id'] == TIME and time_record['countAsPass'] is False
    assert time_record['approvalRequest'] == 'call_b88f280c431643d2b5b911ee5fb92e63'
    assert time_record['originalOracleMustRemainFailed'] is True
    assert originals[TIME]['documents'] == [time_record['originalFixture']]
    assert originals[TIME]['expected'] == time_record['originalExpected'] == 'invalid'
    assert catalog[time_record['originalFixture']] == time_record['fixtureSha256']
    assert sha(CORPUS / 'upstream' / time_record['originalFixture']) == time_record['fixtureSha256']
    records[TIME] = dict(category='time-exact-exception', expected='invalid', kind='schema', actual='schema-valid', codes=[])
    return records


def gate_disposition(row):
    if row.get('engine') != 'product' or row.get('classification') != 'applicable' or row.get('passed') is not False:
        return None
    record = approved_sources().get(row['id'])
    if record is None or any(row.get(k) != record[k] for k in ('expected', 'kind', 'actual')):
        return None
    codes = row.get('codes')
    if not isinstance(codes, list) or any(not isinstance(code, str) for code in codes):
        return None
    # All 23 preserved source80 standards witnesses use exactly [] or [XSD.SchemaRestriction].
    # Exact diagnostic binding also prevents quotas/resources/syntax/harness failures inheriting approval.
    if codes != record['codes']:
        return None
    return record['category']
