#!/usr/bin/env python3
"""Compare the entire current public assembly to canonical task fragments, plus negative controls."""
import pathlib,json,subprocess,tempfile
root=pathlib.Path(__file__).resolve().parents[3]
checker=root/'docs/reports/M7-tokenizer/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
assembly=root/'src/Xemele/bin/Release/net10.0/Xemele.dll'
dotnet='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
rows=[]
for fragment in sorted((root/'docs/api').glob('*.txt')):
    rows.extend(json.loads(line) for line in fragment.read_text().splitlines()[1:])
rows.sort(key=lambda row:row['member'])
assert len({r['member'] for r in rows})==len(rows)
with tempfile.TemporaryDirectory(prefix='xemele-tokenizer-api-') as directory:
    path=pathlib.Path(directory)/'union.txt'
    def check(values):
        path.write_text('xemele-api-v1\n'+'\n'.join(json.dumps(dict(declaration=r['declaration'],id=r['member'],member=r['member']),ensure_ascii=False,sort_keys=True,separators=(',',':')) for r in sorted(values,key=lambda r:r['member']))+'\n')
        return subprocess.run([dotnet,'--fx-version','10.0.11',str(checker),'compare',str(assembly),str(path)],cwd=root,capture_output=True,text=True)
    result=check(rows);print(result.stdout.strip());assert result.returncode==0,result.stderr
    assert check(rows[:-1]).returncode!=0,'Missing API accepted'
    assert check(rows+[dict(declaration='invalid',member='T:Xemele.Fake')]).returncode!=0,'Added API accepted'
    changed=[dict(r) for r in rows];changed[0]['declaration']+=' changed';assert check(changed).returncode!=0,'Changed API accepted'
print(f'All {len(rows)} actual public members matched; missing/added/changed controls rejected; bootstrap exemption absent.')
