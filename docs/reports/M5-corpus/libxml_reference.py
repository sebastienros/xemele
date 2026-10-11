"""Fixed source-applicable C14N1.1 whole-document reference; no subset API claimed."""
import hashlib,json,pathlib,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[3]
BINARY='/usr/bin/xmllint'

def collect(rows,runner=subprocess.run):
    # xmllint --c14n11 uses XML_C14N_1_1=2 and includes comments. Product mode is 1.
    # Applicability derives from input/options only, before invoking either engine.
    applicable=[c for c in rows if c['mode']==1 and 'expression' not in c and (c.get('comments',False) or '<!--' not in c['xml'])]
    assert len(applicable)==6
    result=[]
    for c in applicable:
        row={'id':c['id'],'productMode':1,'referenceMode':2,'expectedUtf8Sha256':c['expectedUtf8Sha256']}
        try:
            run=runner([BINARY,'--nonet','--noent','--dtdattr','--c14n11','-'],input=c['xml'].encode(),capture_output=True,timeout=10)
            row.update(exit=run.returncode,actualUtf8Sha256=hashlib.sha256(run.stdout).hexdigest(),pass_=run.returncode==0 and run.stdout==c['expected'].encode(),diagnostic=run.stderr.decode(errors='replace'),infrastructureError=None)
        except (subprocess.TimeoutExpired,OSError) as error:
            row.update(exit=None,actualUtf8Sha256=None,pass_=False,diagnostic=str(error),infrastructureError=type(error).__name__)
        row['pass']=row.pop('pass_')
        result.append(row)
    return result

def main(rows=None,output=None,runner=subprocess.run,binary_digest=None):
    if rows is None:rows=json.loads((ROOT/'tests/corpora/c14n/vectors.json').read_bytes())
    result=collect(rows,runner)
    errors=[]
    try:
        digest=binary_digest() if binary_digest is not None else hashlib.sha256(pathlib.Path(BINARY).read_bytes()).hexdigest()
    except OSError as error:
        digest=None;errors.append({'stage':'binaryHash','type':type(error).__name__,'diagnostic':str(error)})
    try:
        run=runner([BINARY,'--version'],capture_output=True,text=True,timeout=10)
        version=run.stderr
        if run.returncode!=0:errors.append({'stage':'version','exit':run.returncode,'diagnostic':version})
    except (subprocess.TimeoutExpired,OSError) as error:
        version=None;errors.append({'stage':'version','type':type(error).__name__,'diagnostic':str(error)})
    if output is None:output=ROOT/'artifacts/M5-corpus/libxml.json'
    output=pathlib.Path(output);output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps({'binary':BINARY,'binarySha256':digest,'version':version,'infrastructureErrors':errors,'rows':result},indent=2)+'\n')
    print('C14N1.1 fixed reference',len(result),'passes',sum(r['pass'] for r in result))
    return 0 if not errors and all(r['pass'] for r in result) else 1

if __name__=='__main__':raise SystemExit(main())
