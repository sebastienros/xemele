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
    assert sha(CORPUS / 'inventory.json') == '931c80a3e041073d6162112037ceff22adb0fa1a405873ea7fd3b003beba811e'
    assert sha(CORPUS / 'resources.json') == 'd4e769732145c735ca3e9217975d2e355ad5c7a80b20df2a5de97449025f78af'
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
                                    kind=original['kind'], actual=actual, codes=codes, passed=False)

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
    records[TIME] = dict(category='time-exact-exception', expected='invalid', kind='schema', actual='schema-valid', codes=[], passed=False)
    groups = {r['id']: g for g in inventory['groups']
              for r in ([g['schema']] if g['schema'] else []) + g['instances'] + g['supplementalInstances']}

    def pin(file):
        name, checksum = file['path'], file['sha256']
        assert catalog[name] == checksum == sha(CORPUS / 'upstream' / name)
        if 'bytes' in file:
            assert (CORPUS / 'upstream' / name).stat().st_size == file['bytes']

    def whole_row(record):
        row = originals[record['id']]
        encoding = json.dumps(row, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode('utf-8')
        assert hashlib.sha256(encoding).hexdigest() == record['inventory_record_sha256']
        assert record['metadata']['path'] == groups[record['id']]['metadata']
        pin(record['metadata'])
        return row

    def tuple_record(record, row, observed, category):
        assert record['id'] not in records
        assert observed['classification'] == row['classification'] == 'applicable'
        assert observed['kind'] == row['kind'] and observed['expected'] == row['expected']
        assert observed['passed'] is False or observed['passed'] is None
        assert isinstance(observed['codes'], list) and all(isinstance(c, str) for c in observed['codes'])
        records[record['id']] = dict(category=category, expected=row['expected'], kind=row['kind'],
                                     actual=observed['actual'], codes=observed['codes'], passed=observed['passed'])

    documents = {}
    for name, checksum, index, count, prepared in [
        ('approved-additional-profile-dispositions.json', '46135607588c3421270179b7413ff3eae78b6c22a42afdd3c4dd6124fe33147e', 0, 58, 'd92e3f420fa54505ec1a209683348e207965e60a16d2d4b4fd568e63e40d775d'),
        ('approved-residual-dispositions.json', 'd96a51b2dab9e776145e0f34f8eba516e1189ec77bf3130ff7a6c5c111efa2da', 1, 53, '04c38b304180d0ea34df9f35ded28cc684c7297b4bea51455f76439e445e50b3')]:
        assert sha(CORPUS / name) == checksum, f'approved document changed: {name}'
        document = json.loads((CORPUS / name).read_text())
        assert document['upstream_revision'] == PIN
        assert document['status'] == 'HUMAN APPROVED exact documented nonblocking dispositions; originals retained, never passes'
        approval = document['human_approval']
        assert approval['question_call'] == 'call_e1d528b3cc424337ad718280d2ac189e'
        assert approval['question_index'] == index and approval['answer'] == f'Allow these exact {count} documented dispositions'
        assert approval['approved_prepared_record_sha256'] == prepared
        assert approval['scope'] == 'Only exact named cases; original outcomes unchanged; zero original/gate pass credit; no broader waiver'
        documents[name] = document
    additional = documents['approved-additional-profile-dispositions.json']
    source_records = {}
    for key, source in additional['source_records'].items():
        assert sha(ROOT / source['path']) == source['sha256']
        source_records[key] = json.loads((ROOT / source['path']).read_text())
    additional_count = 0
    for key in ('interpretation_disagreements', 'dependent_unassessed', 'substrate_refusals', 'application_input_outcomes'):
        for record in additional[key]:
            row = whole_row(record)
            assert row['expected'] == record['original_expected']
            assert row['originalExpected'] == record['original_expected_entries']
            assert row['currentStatus'] == record['original_status'] and row['priorStatus'] == record['original_prior']
            assert [scope for scope in row['scopes'] if scope.get('version') or scope.get('edition')] == record['original_scopes']
            assert row['documents'] == list(record['documents'])
            schema = groups[record['id']]['schema']
            assert (schema['documents'] if schema else []) == list(record['schema_documents'])
            for field in ('documents', 'schema_documents'):
                for name, checksum in record[field].items():
                    pin(dict(path=name, sha256=checksum))
            source = record['source_record']
            value = source_records[source['key']]
            for segment in source['json_pointer'].split('/')[1:]:
                segment = segment.replace('~1', '/').replace('~0', '~')
                value = value[int(segment)] if isinstance(value, list) else value[segment]
            assert record['count_as_original_pass'] is False and record['count_as_gate_pass'] is False
            assert record['observed']['engine'] == 'product' and record['observed']['passed'] is False
            tuple_record(record, row, record['observed'], 'approved-additional-' + key)
            additional_count += 1
    assert additional_count == 58
    residual = documents['approved-residual-dispositions.json']
    assert len(residual['cases']) == 53
    for record in residual['cases']:
        row = whole_row(record)
        for key, value in record['original'].items():
            actual = [scope for scope in row['scopes'] if scope.get('version') or scope.get('edition')] if key == 'version_edition_scopes' else row[key]
            assert actual == value
        assert row['documents'] == [f['path'] for f in record['ordered_inputs']]
        schema = groups[record['id']]['schema']
        assert (schema['documents'] if schema else []) == [f['path'] for f in record['ordered_schema_inputs']]
        for field in ('ordered_inputs', 'ordered_schema_inputs', 'referenced_inputs'):
            for file in record[field]:
                pin(file)
        assert record['original_pass_credit'] is False and record['gate_pass_credit'] is False
        tuple_record(record, row, record['observation']['summary'], 'approved-residual-' + record['category'])
    assert len(records) == 147 and sum(r['passed'] is None for r in records.values()) == 11
    provenance_path = CORPUS / 'approved-missing-observation-provenance.json'
    assert sha(provenance_path) == '686da8bbf6913f791b10aee7909643c90be884b122e3e9d95bb4ede8c72c8284'
    provenance = json.loads(provenance_path.read_text())
    assert provenance['sourceRevision'] == '91fbc8473ff43c8229acc1387f597645b6215175'
    assert provenance['frozenManifestSha256'] == '404bc79b127746fed960270717a994df123b06f9856704f9cc0c6d21ac8b6145'
    assert provenance['actualChildren'] == 186 and provenance['actualRows'] == 83472
    assert provenance['zeroOriginalAndGatePassCreditForAllDispositions'] is True
    historical_uncovered = {r['id']:r for r in provenance['uncoveredApplicableProductOriginals']}
    for approval_name, prepared_name, approval_sha, prepared_sha, question, answer, status in [
        ('approved-missing-nonassessment-dispositions.json', 'prepared-missing-nonassessment-dispositions.json', '4b1cb646d5386a08b6ca2c259039b61497bc6e1e88472c5f56aea3c239c1d659', '06a4851a1c1e4653c7a87ab9018046e1d14a8be0085aeeb9cb6567d7d93f6f7d', 'call_2d4e05aaef41499293e682fe147c9db5', 'Allow these exact four documented limitations', 'HUMAN APPROVED exact four documented assessment limitations'),
        ('approved-missing-schema-dispositions.json', 'prepared-missing-schema-dispositions.json', 'f8593c8becc78682aca37ec4b1560b0f03a757948ecf5bc14a41257f12300688', '42b8a89542e7f94b7c1b9f2ad71671ef6c688a3ca92a0aa2e2f8c367e4420ddb', 'call_d75eafbe87064aa3a95b17175cf85308', 'Allow these exact four documented dispositions', 'HUMAN APPROVED exact four documented standards dispositions')]:
        assert sha(CORPUS / approval_name) == approval_sha and sha(CORPUS / prepared_name) == prepared_sha
        approval = json.loads((CORPUS / approval_name).read_text()); prepared = json.loads((CORPUS / prepared_name).read_text())
        assert approval['status'] == status and approval['approvalQuestionId'] == question and approval['humanAnswer'] == answer
        assert approval['approvedPreparedRecordSha256'] == prepared_sha
        assert approval['counts']['originalFalse'] == 4 and approval['counts']['originalPassCredit'] == approval['counts']['gatePassCredit'] == 0
        assert prepared['upstreamRevision'] == PIN and prepared['sourceRevision'] == provenance['sourceRevision']
        assert prepared['frozenCampaignManifestSha256'] == provenance['frozenManifestSha256']
        assert len(prepared['originals']) == 4
        for record in prepared['originals']:
            row = whole_row(dict(id=record['id'], inventory_record_sha256=record['originalMetadataSha256'],
                                  metadata=dict(path=record['metadataFile'], sha256=record['metadataFileSha256'])))
            assert row['expected'] == record['originalExpected'] and record['originalPassCredit'] is False and record['gatePassCredit'] is False
            files = record.get('schemaAndInstanceSha256', record.get('schemaDocumentsSha256'))
            for name, checksum in files.items(): pin(dict(path=name, sha256=checksum))
            if row['kind'] == 'instance':
                schema = groups[record['id']]['schema']
                assert list(files) == schema['documents'] + row['documents']
                assert record['observation']['actual'] == 'incomplete-refusal' and record['observation']['codes'] == ['XSD.MissingComponent']
                assert prepared['completedSetLifecycle'] == dict(exit=0, directChildReaped=True, logsClosed=True, ownedGroupAbsent=True)
                assert provenance['matchedNewMissingApproval'] == 4
            else:
                assert [name for name in files if name in row['documents']] == row['documents']
                assert row['currentStatus'] == record['originalCurrentStatus']
                assert record['observation']['actual'] == 'schema-valid' and record['observation']['codes'] == []
                assert all(record['observation'][key] == historical_uncovered[record['id']][key] for key in ('expected','actual','passed','codes'))
            assert record['observation']['engine'] == 'product' and record['observation']['passed'] is False
            tuple_record(record, row, record['observation'], 'approved-' + record['category'])
    assert len(records) == 155 and sum(r['passed'] is None for r in records.values()) == 11
    assert sum(r['passed'] is False for r in records.values()) == 144
    assert 'msMeta/Annotations_w3c.xml#g78/s:annotZ002' not in records
    return records


def gate_disposition(row):
    if not all(k in row for k in ('id', 'engine', 'classification', 'kind', 'expected', 'actual', 'codes', 'passed')) or not isinstance(row.get('id'), str):
        return None
    if row.get('engine') != 'product' or row.get('classification') != 'applicable':
        return None
    record = approved_sources().get(row.get('id'))
    if record is None or row['passed'] is not record['passed'] or any(row.get(k) != record[k] for k in ('expected', 'kind', 'actual')):
        return None
    codes = row.get('codes')
    if not isinstance(codes, list) or any(not isinstance(code, str) for code in codes):
        return None
    # Approval matches the complete original outcome tuple, including explicit false/null
    # and ordered diagnostics. Only exact approved resource/cycle signatures can match.
    if codes != record['codes']:
        return None
    return record['category']
