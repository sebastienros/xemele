"""Import every numbered historical libxml2 RNG regression with original predicates."""
import hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];CORPUS=ROOT/'tests/corpora/relaxng';REV='96498992efa48d52b0e8b83058bd88dbdaf153c1'
def main(cache):
 def git(*args):return subprocess.check_output(['git','--git-dir='+cache,*args])
 def original(path):return git('show',REV+':'+path)
 def save(path):
  data=original(path);out=CORPUS/'libxml2'/path;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(data)
  return {'path':str(out.relative_to(CORPUS)),'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
 paths=git('ls-tree','-r','--name-only',REV,'test/relaxng').decode().splitlines()
 selected=[p for p in paths if re.fullmatch(r'test/relaxng/[0-9]+(?:[-_].*)?\.(?:rng|xml)',p)]
 files={p:save(p) for p in selected};cases=[];resources=[]
 for p in selected:
  if not re.fullmatch(r'test/relaxng/[0-9]+\.rng',p):continue
  stem=Path(p).stem;instances=[]
  for ip in selected:
   if re.fullmatch(r'test/relaxng/'+stem+r'_\d.xml',ip):
    oraclePath='result/relaxng/'+Path(ip).stem+'.err';oracle=original(oraclePath)
    # Retain the exact original predicate source as an upstream oracle fixture,
    # never as generated execution logs. No result inferred from the product.
    of=save(oraclePath)
    text=oracle.decode();valid=text.endswith('./'+ip+' validates\n');invalid=text.endswith('./'+ip+' fails to validate\n')
    assert valid != invalid,oraclePath
    instances.append({'id':'libxml2-'+Path(ip).stem,'expected':'valid' if valid else 'invalid','oracle':of,**files[ip]})
  assert instances,p
  cases.append({'id':'libxml2-'+stem,'schemaExpected':'accepted','schema':files[p],'instances':instances,'classification':'applicable','reason':'Numbered historical RNG XML regression. Original runtest.c rngTest compiles this schema and checks authored result/relaxng instance predicates.'})
 used={c['schema']['path'] for c in cases}|{i['path'] for c in cases for i in c['instances']}
 resources=[f for f in files.values() if f['path'] not in used]
 inv={'suite':'libxml2 numbered historical RELAX NG regressions','upstream':'https://gitlab.gnome.org/GNOME/libxml2','revision':REV,'selection':'All top-level test/relaxng files with numeric bug-ID basename (number.rng, number_?.xml, and their number-suffixed resources); examples and OASIS suite are outside this source family, not feature exclusions.','license':'MIT','missingCases':0,'missingResources':0,'cases':cases,'resources':resources,'sources':[save('Copyright'),save('runtest.c')]}
 (CORPUS/'libxml2-inventory.json').write_text(json.dumps(inv,indent=2)+'\n')
 print(len(cases),'schemas',sum(len(c['instances']) for c in cases),'instances',len(resources),'resources')
if __name__=='__main__':main(sys.argv[1])
