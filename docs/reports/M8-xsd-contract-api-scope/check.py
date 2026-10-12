"""Static publication boundary proof; launches no runtime or compiler."""
import hashlib
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
candidate=ROOT/'tests/Contracts/Fixtures/M8/MissingComponents/candidate.golden.txt'
old=ROOT/'tests/Contracts/Fixtures/M8/MissingComponents/old.golden.txt'
assert sha(candidate)=='4044ddc141402419452b9a9da0665e8d215fb51d9a9c0f2e0a2f0e51b726c181'
assert sha(old)=='11556ee679bd7a0f8b0c7eab182291e8d40a98d065c5bf4ff6f56196c6cfb181'
fragment=ROOT/'docs/api/M8-xsd-missing-components-contract.txt'
assert fragment.read_bytes()==b'xemele-api-v1\n'
members=set()
files=sorted((ROOT/'docs/api').glob('*.txt'))
for path in files:
    lines=path.read_text().splitlines()
    assert lines[0]=='xemele-api-v1'
    for line in lines[1:]:
        row=json.loads(line)
        assert set(row)=={'id','member','declaration'}
        assert line==json.dumps(row,ensure_ascii=False,sort_keys=True,separators=(',',':'))
        assert row['member'] not in members
        assert 'XsdElementDeclaration' not in row['member']
        members.add(row['member'])
manifest=json.loads((ROOT/'docs/reports/M8-xsd-missing-components-contract/source-manifest.json').read_text())
assert all(sha(ROOT/name)==identity for name,identity in manifest['files'].items())
print(json.dumps({'result':'passed','runtimeApiFiles':len(files),'runtimeApiMembers':len(members),'candidateProjectionUnchanged':True,'oldProjectionUnchanged':True,'manifestIdentities':len(manifest['files']),'runtimeOrCompilerLaunched':False}))
