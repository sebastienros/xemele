"""Exact BCL nonassessment evidence guard. It never changes the original oracle."""
import hashlib
import json
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCOPE = ROOT / 'tests/corpora/xsd/bcl-reference-exception-scope.json'
SCOPE_SHA = 'c5d23c50623b27486399fd46cabadc6d8ef525888a1368884147892ef1a9097c'


def facts():
    payload = SCOPE.read_bytes()
    assert hashlib.sha256(payload).hexdigest() == SCOPE_SHA, 'changed reference exception scope'
    scope = json.loads(payload)
    assert len(scope['cases']) == len({c['row']['id'] for c in scope['cases']}) == 21
    return scope


def validate_observation(row, expected):
    scope = facts()
    cases = {case['row']['id']: case for case in scope['cases']}
    assert row['id'] in cases, 'unknown reference exception ID'
    case = cases[row['id']]
    assert expected == case['row'], 'changed original reference metadata/order/status/oracle'
    assert (row['engine'], row['kind'], row['classification'], row['expected'], row['actual']) == (
        'bcl', 'schema', 'applicable', 'invalid', 'reference-engine-exception')
    assert row['passed'] is False and row['codes'] == [] and row['resources'] == []
    assert row.get('hintOrderingProfile', False) is False
    detail = json.loads(row['detail'])
    for key, value in {
        'attribution': 'pinned-bcl-empty-id-preprocessor', 'purpose': 'schema-compilation',
        'exceptionType': 'System.ArgumentException', 'parameter': 'name',
        'runtime': scope['runtime'], 'scopeSha256': SCOPE_SHA,
        'moduleSha256': scope['bcl_module_sha256'],
        'documentSha256': case['document_sha256'], 'sourceUri': case['source_uri'],
    }.items():
        assert detail[key] == value, 'changed reference exception ' + key
    operation = detail['operation']
    assert operation in ('Add', 'Compile')
    mvid = str(uuid.UUID(detail['moduleMvid']))
    assert mvid == detail['moduleMvid'] and uuid.UUID(mvid).int != 0
    frames = detail['frames']
    assert frames and all(type(f['Token']) is int and f['Token'] > 0 and
                          isinstance(f['Parameters'], list) and uuid.UUID(f['Mvid']).int != 0 for f in frames)
    index = 0
    while index < len(frames) and frames[index]['Module'] == 'System.Private.CoreLib.dll' and frames[index]['Type'] == 'System.ArgumentException' and frames[index]['Method'] in ('ThrowIfNullOrEmpty', 'ThrowNullOrEmptyException'):
        index += 1

    def bcl(frame):
        return frame['Module'] == scope['bcl_module'] and frame['Mvid'] == mvid

    first = index
    while index < len(frames) and bcl(frames[index]) and (frames[index]['Type'], frames[index]['Method']) == ('System.Xml.XmlConvert', 'VerifyNCName'):
        index += 1
    assert index > first and index < len(frames), 'no actual BCL VerifyNCName frame'
    assert bcl(frames[index]) and (frames[index]['Type'], frames[index]['Method']) == ('System.Xml.Schema.Preprocessor', 'ValidateIdAttribute'), 'wrong empty-id origin'
    api = False
    parameters = ['System.String', 'System.Xml.XmlReader'] if operation == 'Add' else []
    while index < len(frames) and bcl(frames[index]):
        frame = frames[index]
        api |= (frame['Type'], frame['Method'], frame['Parameters']) == ('System.Xml.Schema.XmlSchemaSet', operation, parameters)
        index += 1
    assert api and index < len(frames), 'no literal schema API operation'
    boundary = frames[index]
    assert (boundary['Type'], boundary['Method'], boundary['Module'], boundary['Parameters']) == (
        'XsdCorpus.BclEngine', operation + 'Boundary', 'XsdCorpus.dll',
        ['System.Xml.Schema.XmlSchemaSet', 'System.Xml.XmlReader'] if operation == 'Add' else ['System.Xml.Schema.XmlSchemaSet']), 'caller/stream/resolver or wrong boundary'
    return detail
