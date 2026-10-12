"""Offline source/oracle integrity. Regenerate all Jing fixtures and expectations."""
import hashlib,importlib.util,json,shutil,tempfile,sys
sys.dont_write_bytecode=True
from collections import Counter
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];CORPUS=ROOT/'tests/corpora/relaxng'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def verified(f):
 relative=Path(f['path']);assert not relative.is_absolute() and '..' not in relative.parts
 path=CORPUS/relative
 for ancestor in [path,*path.parents]:
  assert not ancestor.is_symlink()
  if ancestor==CORPUS:break
 assert sha(path)==f['sha256'],str(path);assert path.stat().st_size==f['bytes']
def check():
 inventories=[json.loads((CORPUS/n).read_text()) for n in ['jing-inventory.json','libxml2-inventory.json','profile-inventory.json']]
 jing,xml,profile=inventories;assert len(profile['cases'])==17 and sum(len(c['instances']) for c in profile['cases'])==28;assert jing['revision']=='a6bc0041035988325dfbfe7823ef2c098fc56597';assert xml['revision']=='96498992efa48d52b0e8b83058bd88dbdaf153c1'
 assert len(jing['cases'])==385 and sum(len(c['instances']) for c in jing['cases'])==580
 assert len(xml['cases'])==7 and sum(len(c['instances']) for c in xml['cases'])==18
 for inv in inventories:
  assert inv['missingCases']==inv['missingResources']==0
  assert len({c['id'] for c in inv['cases']})==len(inv['cases'])
  for f in inv['resources']+inv['sources']:verified(f)
  for c in inv['cases']:
   assert c['classification'] in ['applicable','authored-required-profile'] and c['reason'];verified(c['schema'])
   for instance in c['instances']:
    verified(instance)
    if 'oracle' in instance:
     verified(instance['oracle']);original=(CORPUS/instance['oracle']['path']).read_text()
     assert original.endswith('./'+instance['path'].removeprefix('libxml2/')+(' validates\n' if instance['expected']=='valid' else ' fails to validate\n'))
 with tempfile.TemporaryDirectory(prefix='xemele-rng-oracle-') as temporary:
  upstream=Path(temporary)/'upstream';out=Path(temporary)/'split'
  for dst,src in [('mod/rng-validate/test/spectest.xml','spectest.xml'),('copying.txt','copying.txt'),('test/index.html','index.html'),('test/prep.xsl','prep.xsl')]:
   path=upstream/dst;path.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(CORPUS/'upstream/jing'/src,path)
  spec=importlib.util.spec_from_file_location('rng_split',HERE/'import_jing.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);module.split(upstream,out)
  assert json.loads((out/'jing-inventory.json').read_text())==jing,'Original fixture/oracle regeneration differs'
  assert {(str(p.relative_to(out)),sha(p)) for p in out.rglob('*') if p.is_file()}=={(str(p.relative_to(CORPUS)),sha(p)) for p in (CORPUS/'jing').rglob('*') if p.is_file()}|{(str(p.relative_to(CORPUS)),sha(p)) for p in (CORPUS/'upstream/jing').rglob('*') if p.is_file()}|{('jing-inventory.json',sha(out/'jing-inventory.json'))}
 limits=json.loads((CORPUS/'reference-limitations.json').read_text())['cases'];assert len(limits)==15
 cases={c['id']:c for inv in inventories for c in inv['cases']}
 for l in limits:
  c=cases[l['caseId']];f=c['schema'] if l['phase']=='schema' else c['instances'][int(l['phase'])]
  assert f['sha256']==l['sha256'] and c['schema']['sha256']==l['schemaSha256']
  assert l['referenceObserved']!=l['authoritativeExpected'];assert l['classification']=='reference-limitation-not-agreement';assert l['primarySpec'].startswith('https://') and l['reason']
 identities=json.loads((HERE/'reference-build.json').read_text())
 assert sha(HERE/'RngReference.java')==identities['jing']['sourceSha256'];assert sha(HERE/'libxml2_reference.c')==identities['libxml2']['sourceSha256']
 print('PASS:392 upstream+17 authored schemas,598 upstream+28 authored independent instance predicates,32 external resources;15 explicit reference limitations;0 missing or excluded cases. Full Jing fixture/oracle regeneration matches.')
 print('Schemas:',dict(Counter(c['schemaExpected'] for c in cases.values())),'Instances:',dict(Counter(i['expected'] for c in cases.values() for i in c['instances'])))
if __name__=='__main__':check()
