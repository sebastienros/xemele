#!/usr/bin/env python3
"""Compile-time public API union and real extractor negative controls; no baseline edits."""
import hashlib, json, pathlib, subprocess, sys, tempfile
ROOT = pathlib.Path(__file__).resolve().parents[3]
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT/'tools/ApiBaseline'))
from compare import compose
from discover import DiscoveryError
HOST = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
CHECKER = ROOT/'tools/ApiBaseline/Checker/bin/Release/net10.0/ApiCheck.dll'
ASSEMBLY = ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll'
TOOL = ROOT/'src/Xemele.Tool/bin/Release/net10.0/Xemele.Tool.dll'
# Full official inventory includes all manifest checks and real API fragments.
canonical, inventory = compose(ROOT)
fragments = [{'path':str(source.relative_to(ROOT)), 'sha256':hashlib.sha256(source.read_bytes()).hexdigest()} for source in sorted((ROOT/'docs/api').glob('*.txt'))]
controls = []
def run(mode, assembly, fragment, code):
    result = subprocess.run([HOST,'--fx-version','10.0.11',str(CHECKER),mode,str(assembly),str(fragment)],capture_output=True,text=True,timeout=60)
    assert result.returncode == code, (mode, result.returncode, result.stdout, result.stderr)
    controls.append({'mode':mode,'assembly':assembly.name,'expected_exit':code,'output':(result.stdout+result.stderr).strip()})
with tempfile.TemporaryDirectory(prefix='xemele-m7-api-') as directory:
    d = pathlib.Path(directory); fragment = d/'union.txt'; fragment.write_text(canonical)
    run('compare',ASSEMBLY,fragment,0)
    rows = canonical.splitlines()
    changed = json.loads(rows[1]); changed['declaration'] += '; negative-control-change'
    changed_row = json.dumps(changed, ensure_ascii=False, sort_keys=True, separators=(',',':'))
    assert changed_row != rows[1]
    for name, lines in [('missing',rows[:-1]),('changed',[rows[0],changed_row,*rows[2:]]),('added',[*rows,json.dumps({'declaration':'public class Xemele.Unshipped','id':'T:Xemele.Unshipped','member':'T:Xemele.Unshipped'},sort_keys=True,separators=(',',':'))])]:
        fragment = d/(name+'.txt'); fragment.write_text('\n'.join(lines)+'\n');run('compare',ASSEMBLY,fragment,1)
    # The tool itself has no declared public member: its private registry is statically reached.
    fragment = d/'tool.txt'; run('emit',TOOL,fragment,0)
    assert fragment.read_text() == 'xemele-api-v1\n',fragment.read_text()
    for name, declaration in [('duplicate','public class A'),('conflicting','public class Changed')]:
        root=d/name; (root/'docs/api').mkdir(parents=True)
        for file, identity, decl in [('a','first','public class A'),('b','second',declaration)]:
            (root/'docs/api'/f'{file}.txt').write_text('xemele-api-v1\n'+json.dumps({'declaration':decl,'id':identity,'member':'T:A'},sort_keys=True,separators=(',',':'))+'\n')
        try: compose(root);raise AssertionError('invalid inventory accepted')
        except DiscoveryError as error:
            assert name+' API member' in str(error),error
            controls.append({'inventory':name,'expected':'rejected','output':str(error)})
print(json.dumps({'passed':len(controls),'library_members':len(inventory['provenance']),'tool_public_members':0,'api_fragments':fragments,'inventory':inventory['inventory'], 'controls':controls},indent=2))
