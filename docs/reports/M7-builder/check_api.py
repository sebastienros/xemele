"""Offline official API composition, immutable cache materialization and controls."""
import hashlib,json,pathlib,subprocess,sys,tempfile
root=pathlib.Path(__file__).resolve().parents[3]
sys.path.insert(0,str(root/'tools/ApiBaseline'))
from compare import compose
cache=pathlib.Path('/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources')
manifest=root/'tests/corpora/xml/manifest.json'
copied=0
for row in json.loads(manifest.read_text())['fixtures']:
    destination=manifest.parent/row['path']
    candidates=[cache/row['path'],cache/row['path'].removeprefix('xmlconf/')]
    if not destination.is_file():
        source=next(p for p in candidates if p.is_file())
        data=source.read_bytes()
        assert hashlib.sha256(data).hexdigest()==row['sha256'],str(source)
        destination.parent.mkdir(parents=True,exist_ok=True);destination.write_bytes(data);copied+=1
    assert hashlib.sha256(destination.read_bytes()).hexdigest()==row['sha256'],str(destination)
canonical,report=compose(root)
assert report['inventory']['fixtures']==3388 and not report['inventory']['bootstrapEmptyApi']
dotnet='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
checker=root/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
assembly=root/'src/Xemele/bin/Release/net10.0/Xemele.dll'
with tempfile.TemporaryDirectory(prefix='xemele-builder-api-') as directory:
    union=pathlib.Path(directory)/'union.txt'
    def check(text):
        union.write_text(text)
        return subprocess.run([dotnet,'--fx-version','10.0.11',str(checker),'compare',str(assembly),str(union)],cwd=root,capture_output=True,text=True,timeout=60)
    result=check(canonical);assert result.returncode==0,result.stdout+result.stderr
    rows=canonical.splitlines()
    assert check('\n'.join(rows[:-1])+'\n').returncode!=0
    assert check(canonical+json.dumps(dict(declaration='invalid',id='T:Xemele.Fake',member='T:Xemele.Fake'))+'\n').returncode!=0
    first=json.loads(rows[1]);first['declaration']+=' changed';rows[1]=json.dumps(first)
    assert check('\n'.join(rows)+'\n').returncode!=0
assert len(report['provenance'])==1235
print(result.stdout.strip())
print(f"Official API1235/fixtures3388/bootstrapFalse; missing/added/changed controls rejected; {copied} immutable cache files copied and all hashes verified.")
