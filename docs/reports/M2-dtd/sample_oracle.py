"""Independent fixed sample output oracle; never parse declarations to derive expectations."""
import json

def verify_output(text):
    rows = [json.loads(line) for line in text.splitlines()]
    expected_model = {
        'elements': [
            {'Name': 'r', 'kind': 'Sequence', 'occurrence': 'Once', 'children': [{'Name': 'a', 'occurrence': 'Once'}, {'Name': 'b', 'occurrence': 'Optional'}], 'IsExternal': False},
            {'Name': 'a', 'kind': 'Empty', 'occurrence': 'Once', 'children': [], 'IsExternal': True},
            {'Name': 'b', 'kind': 'Mixed', 'occurrence': 'Once', 'children': [], 'IsExternal': True}],
        'attributes': [
            {'ElementName': 'r', 'Name': 'local', 'type': 'NmTokens', 'kind': 'Value', 'DefaultValue': ' x  y ', 'IsExternal': False},
            {'ElementName': 'r', 'Name': 'inherited', 'type': 'CData', 'kind': 'Value', 'DefaultValue': '&e;&#9;', 'IsExternal': True}],
        'Defaults': ['x y', '中😀\t'], 'internalPrecedence': True}
    # Literal counts from the authored sample: reference suffix starts after 50 units;
    # child declaration starts after its 24-unit text declaration. Columns are scalars.
    expected_hook = {'request': 'granted', 'systemId': 'approved.dtd', 'consumed': 50,
        'declarations': [
            {'Name': 'child', 'IsExternal': True, 'uri': 'https://sample.invalid/final-approved.dtd', 'line': 1, 'column': 25, 'byteOffset': None},
            {'Name': 'parent', 'IsExternal': False, 'uri': 'https://sample.invalid/subset.dtd', 'line': 9, 'column': 57, 'byteOffset': None}]}
    assert rows == [expected_model, expected_hook], (rows, [expected_model, expected_hook])
