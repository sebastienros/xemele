"""Fixed source-applicable C14N1.1 whole-document reference; no subset API claimed."""
import hashlib,json,pathlib,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[3]
rows=json.loads((ROOT/'tests/corpora/c14n/vectors.json').read_bytes())
# xmllint --c14n11 uses XML_C14N_1_1=2 and includes comments. Product mode is1.
# Applicability derives from input/options only, before invoking either engine.
applicable=[c for c in rows if c['mode']==1 and 'expression' not in c and (c.get('comments',False) or '<!--' not in c['xml'])]
assert len(applicable)==6
result=[]
for c in applicable:
    run=subprocess.run(['/usr/bin/xmllint','--nonet','--noent','--dtdattr','--c14n11','-'],input=c['xml'].encode(),capture_output=True,timeout=10)
    result.append({'id':c['id'],'productMode':1,'referenceMode':2,'exit':run.returncode,'expectedUtf8Sha256':c['expectedUtf8Sha256'],'actualUtf8Sha256':hashlib.sha256(run.stdout).hexdigest(),'pass':run.returncode==0 and run.stdout==c['expected'].encode(),'diagnostic':run.stderr.decode(errors='replace')})
raw=ROOT/'artifacts/M5-corpus';raw.mkdir(parents=True,exist_ok=True)
(raw/'libxml.json').write_text(json.dumps({'binary':'/usr/bin/xmllint','binarySha256':hashlib.sha256(pathlib.Path('/usr/bin/xmllint').read_bytes()).hexdigest(),'version':subprocess.run(['/usr/bin/xmllint','--version'],capture_output=True,text=True,timeout=10).stderr,'rows':result},indent=2)+'\n')
print('C14N1.1fixedreference',len(result),'passes',sum(r['pass'] for r in result))
assert all(r['pass'] for r in result)
