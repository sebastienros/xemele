#!/usr/bin/env python3
"""Compare all task API rows to the runtime with the approved compiled extractor.
Corpus discovery is independent and is deliberately not run by this scoped check.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
parser = argparse.ArgumentParser()
parser.add_argument("--checker", required=True, type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[3]
rows = []
members = set()
for path in sorted((root / 'docs/api').glob('*.txt')):
    lines = path.read_text().splitlines()
    assert lines[0] == 'xemele-api-v1', path
    for line in lines[1:]:
        row = json.loads(line)
        assert set(row) == {'id', 'member', 'declaration'}, path
        assert line == json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(',', ':')), path
        assert row['member'] not in members, row['member']
        members.add(row['member'])
        rows.append(dict(row, id=row['member']))
rows.sort(key=lambda row: row['member'])
with tempfile.TemporaryDirectory(prefix='xemele-adapters-api-') as directory:
    union = Path(directory) / 'union.txt'
    union.write_text('xemele-api-v1\n' + ''.join(json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n' for row in rows))
    subprocess.run(['/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet', '--fx-version', '10.0.11',
        str(args.checker), 'compare',
        str(root / 'src/Xemele/bin/Release/net10.0/Xemele.dll'), str(union)], check=True)
