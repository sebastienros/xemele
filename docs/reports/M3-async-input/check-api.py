"""Compare the complete duplicate-checked task-fragment union to the runtime."""
import json
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
extractor = Path('/private/tmp/xemele-m3-integration/docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll')
rows = {}
members = set()
for fragment in sorted((root / 'docs/api').glob('*.txt')):
    lines = fragment.read_text().splitlines()
    assert lines[0] == 'xemele-api-v1', fragment
    for line in lines[1:]:
        row = json.loads(line)
        assert row['id'] not in rows, row['id']
        assert row['member'] not in members, row['member']
        rows[row['id']] = row
        members.add(row['member'])
with tempfile.TemporaryDirectory(prefix='async-input-api-') as directory:
    baseline = Path(directory) / 'union.txt'
    baseline.write_text('xemele-api-v1\n' + ''.join(json.dumps(rows[key], ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n' for key in sorted(rows)))
    subprocess.run(['dotnet', str(extractor), 'compare', str(root / 'src/Xemele/bin/Release/net10.0/Xemele.dll'), str(baseline)], check=True)
print(f'Duplicate-checked {len(rows)} IDs and member identities from current fragments.')
