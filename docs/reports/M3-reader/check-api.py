"""Check compiled APIs using the unchanged approved M0 extractor."""
import json
from pathlib import Path
import subprocess
import tempfile
import sys
root = Path(__file__).resolve().parents[3]
dotnet = sys.argv[1]
checker = root / 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
def emit(assembly, target):
    subprocess.run([dotnet, str(checker), 'emit', str(root / assembly), str(target)], check=True)
    return [json.loads(line) for line in target.read_text().splitlines()[1:]]
def reader(rows):
    return [r for r in rows if r['member'].split(':', 1)[1].startswith(('Xemele.Streaming.XmlReader', 'Xemele.Streaming.XmlTokenKind'))]
with tempfile.TemporaryDirectory() as temporary:
    temporary = Path(temporary)
    actual = emit('src/Xemele/bin/Release/net10.0/Xemele.dll', temporary / 'actual.txt')
    contract = emit('docs/reports/M3-reader/Contract/bin/Release/net10.0/Contract.dll', temporary / 'contract.txt')
    baseline = [json.loads(line) for line in (root / 'docs/api/M3-reader.txt').read_text().splitlines()[1:]]
    assert reader(actual) == baseline == reader(contract)
    expected = [json.loads(line) for p in sorted((root / 'docs/api').glob('*.txt')) for line in p.read_text().splitlines()[1:]]
    assert len(expected) == len({r['member'] for r in expected})
    assert {r['member']: r['declaration'] for r in actual} == {r['member']: r['declaration'] for r in expected}
    print(f'PASS: {len(baseline)} frozen reader rows; {len(actual)} composed runtime API rows. Corpus discovery is a separate check.')
