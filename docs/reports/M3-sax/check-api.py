#!/usr/bin/env python3
"""Compiled SAX freeze and combined public API preservation; no corpus discovery."""
import json
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
dotnet = os.environ.get('XEMELE_DOTNET', '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
checker = root / 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
runtime = root / 'src/Xemele/bin/Release/net10.0/Xemele.dll'
frozen = root / 'docs/reports/M3-contracts/Sync/bin/Release/net10.0/Sync.dll'
def read(path):
    lines = path.read_text().splitlines()
    assert lines[0] == 'xemele-api-v1'
    rows = [json.loads(line) for line in lines[1:]]
    assert rows == sorted(rows, key=lambda row: row['id'])
    return rows
with tempfile.TemporaryDirectory() as temporary:
    directory = Path(temporary)
    actual = directory / 'actual.txt'
    expected = directory / 'frozen.txt'
    subprocess.run([dotnet, str(checker), 'emit', str(runtime), str(actual)], check=True)
    subprocess.run([dotnet, str(checker), 'emit', str(frozen), str(expected)], check=True)
    names = ('Xemele.Streaming.XmlSaxParser', 'Xemele.Streaming.XmlSaxCompletion')
    def scoped(rows):
        return {row['member']: row['declaration'] for row in rows if any(row['member'] == 'T:' + name or row['member'].startswith('M:' + name + '.') for name in names)}
    actual_scope = scoped(read(actual))
    assert actual_scope == scoped(read(expected)) == scoped(read(root / 'docs/api/M3-sax.txt'))
    print(f'PASS: {len(actual_scope)} compiled SAX declarations equal exact frozen Sync fixture and task baseline')
    combined = {}
    for path in sorted((root / 'docs/api').glob('*.txt')):
        for row in read(path):
            member = row['member']
            assert member not in combined, (member, path)
            combined[member] = dict(row, id=member)
    fragment = directory / 'combined.txt'
    fragment.write_text('xemele-api-v1\n' + ''.join(json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n' for _, row in sorted(combined.items())))
    subprocess.run([dotnet, str(checker), 'compare', str(runtime), str(fragment)], check=True)
