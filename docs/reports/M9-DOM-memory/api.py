"""Recheck the repaired public surface using the exact approved M9 extractor."""
import sys
sys.dont_write_bytecode = True
import hashlib, importlib.util, json, os, subprocess, tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
HOST=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
spec=importlib.util.spec_from_file_location('bindings',ROOT/'docs/reports/M8-xsd-resolved-availability/harness/bindings.py')
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
product=ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll'
checker=ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
env=dict(os.environ,DOTNET_ROOT=str(HOST.parent),DOTNET_ROLL_FORWARD='Disable')
with tempfile.TemporaryDirectory(prefix='xemele-dom-api-') as directory:
    temporary=Path(directory); union=temporary/'union.txt'; actual=temporary/'actual.txt'
    b.write_api_union(union); expected=b.rows(union); assert len(expected)==1430
    def call(mode,path):
        return subprocess.run([str(HOST),'--fx-version','10.0.11',str(checker),mode,str(product),str(path)],
                              env=env,capture_output=True,text=True,timeout=60)
    result=call('compare',union);assert result.returncode==0,result.stdout+result.stderr
    result=call('emit',actual);assert result.returncode==0,result.stdout+result.stderr
    compiled=b.rows(actual);assert compiled==expected
    old=json.loads((ROOT/'docs/reports/M1-gate/integration/runtime-api.json').read_bytes())
    assert len(old['provenance'])==222
    assert all(compiled[r['member']]['declaration']==r['declaration'] for r in old['provenance'])
    negative={}
    for mode in ['missing','changed']:
        lines=union.read_text().splitlines()
        if mode=='missing':lines.pop(1)
        else:
            row=json.loads(lines[1]);row['declaration']+=' [deliberate mismatch]'
            lines[1]=json.dumps(row,sort_keys=True,separators=(',',':'))
        path=temporary/(mode+'.txt');path.write_text('\n'.join(lines)+'\n')
        result=call('compare',path);assert result.returncode!=0
        negative[mode]=dict(exit=result.returncode,refused=True)
    report=dict(result='passed',matched=True,members=1430,fragments=55,firstPreviewUnchanged=222,
                removed=0,changed=0,negativeControls=negative,assemblySha256=hashlib.sha256(product.read_bytes()).hexdigest(),
                checkerSha256=hashlib.sha256(checker.read_bytes()).hexdigest(),
                approvedUnion='docs/reports/M8-xsd-resolved-availability/harness/bindings.py',
                approvedExtractor='docs/reports/M0-contracts/ApiCheck/Program.cs',
                revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip())
    (Path(__file__).parent/'api.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS1430 compiled members /55 fragments;222 preview members unchanged; two mismatch controls refused')
