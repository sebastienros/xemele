#!/usr/bin/env python3
"""Reproduce original W3C inventory; never consult a validation engine."""
import argparse, collections, hashlib, json, shutil, subprocess
from pathlib import Path
from urllib.parse import urljoin, unquote, urlparse
import xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent
REV='7bc3365c652a322f3d762021b3879eb92dae7e30'
NS='{http://www.w3.org/XML/2004/xml-schema-test-suite/}'
HREF='{http://www.w3.org/1999/xlink}href'
BASE='{http://www.w3.org/XML/1998/namespace}base'
# XSD 1.0 Second Edition and its XML 1.0 editions 1-4 datatype grammar.
# Unicode version-specific gates/oracles remain unresolved without an independently justified profile.
TOKENS={'1.0','1.0-2e','XML-1.0','XML-1.0-1e-4e','CTR-all-compile'}
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=True,separators=(',',':'))+'\n')
def scopes(nodes):return [{'element':n.tag.removeprefix(NS),'version':n.get('version',''),'edition':n.get('edition','')} for n in nodes]
def classify(nodes):
 for n in nodes:
  ts=n.get('version','').split()
  if ts and not TOKENS.intersection(ts):
   known={'1.1','1.0-1e','XML-1.1','XML-1.0-5e','full-xpath-in-CTA','restricted-xpath-in-CTA','CTR-all-runtime','CTR-all-idep','comments-and-PIs-excluded','comments-and-PIs-included'}
   if set(ts)<=known:return 'excluded',f"{n.tag.removeprefix(NS)} version={n.get('version')}: none supported"
   return 'unresolved',f"{n.tag.removeprefix(NS)} version={n.get('version')}: profile token requires independent justification"
  if n.get('edition') and n.get('edition') not in ('2','2e','1.0-2e'):return 'excluded',f"edition={n.get('edition')}: not Second Edition"
 return 'applicable','each enclosing scope accepts at least one supported token (or has no restriction)'
def record(test,nodes,p,kind,index,base):
 ex=[dict(e.attrib) for e in test.findall(NS+'expected')]
 selected=[e for e in ex if set(e.get('version','').split())<=TOKENS]
 cl,reason=classify(nodes+[test])
 if cl=='applicable' and len(selected)!=1:cl='unresolved';reason=f'{len(selected)} applicable expected outcomes'
 refs=[]
 for e in test.findall(NS+('schemaDocument' if kind=='schema' else 'instanceDocument')):
  u=urljoin(urljoin(base,test.get(BASE,'')),e.get(BASE,''));u=urljoin(u,e.get(HREF,''));pu=urlparse(u)
  refs.append(unquote(pu.path).removeprefix('/source/') if pu.scheme=='https' and pu.netloc=='w3c-xsd.test' else u)
 return {'id':f'{p}#{index}:{test.get("name","")}', 'name':test.get('name',''),'kind':kind,'classification':cl,'reason':reason,'scopes':scopes(nodes+[test]),'expected':selected[0]['validity'] if len(selected)==1 else None,'originalExpected':ex,'documents':refs,'documentMetadata':[dict(e.attrib) for e in test.findall(NS+('schemaDocument' if kind=='schema' else 'instanceDocument'))],'attributes':dict(test.attrib),'currentStatus':[dict(e.attrib) for e in test.findall(NS+'current')],'priorStatus':[dict(e.attrib) for e in test.findall(NS+'prior')]}
def generate(root):
 suite=ET.parse(root/'suite.xml').getroot();groups=[];stats=collections.Counter();tokens=collections.Counter();sets=[]
 for ref in suite.findall(NS+'testSetRef'):
  p=ref.get(HREF);sets.append(p);testset=ET.parse(root/p).getroot()
  b=urljoin(urljoin('https://w3c-xsd.test/source/suite.xml',suite.get(BASE,'')),ref.get(BASE,''));b=urljoin(b,p);b=urljoin(b,testset.get(BASE,''))
  for gi,g in enumerate(testset.findall(NS+'testGroup')):
   nodes=[suite,testset,g];gb=urljoin(b,g.get(BASE,''));sc=g.find(NS+'schemaTest');s=None
   if sc is not None:s=record(sc,nodes,p,'schema',f'g{gi}/s',gb)
   instances=[record(t,nodes,p,'instance',f'g{gi}/i{ii}',gb) for ii,t in enumerate(g.findall(NS+'instanceTest'))]
   groups.append({'id':f'{p}#g{gi}:{g.get("name","")}', 'metadata':p,'name':g.get('name',''),'attributes':dict(g.attrib),'schema':s,'instances':instances})
   stats['groups']+=1
   for row in ([s] if s else [])+instances:
    stats[row['kind']+'Tests']+=1;stats[row['kind']+'.'+row['classification']]+=1
    if row['classification']=='applicable':stats[row['kind']+'.expected.'+str(row['expected'])]+=1
    for v in row['scopes']:tokens.update(v['version'].split())
   if s and len(s['documents'])>1:stats['multiDocumentSchemas']+=1
 stats['sets']=len(sets)
 missing=[{'id':r['id'],'classification':r['classification'],'path':d} for g in groups for r in ([g['schema']] if g['schema'] else [])+g['instances'] for d in r['documents'] if not (root/d).is_file()]
 return {'missingReferences':missing,'revision':REV,'supportedTokens':sorted(TOKENS),'counts':dict(sorted(stats.items())),'testSets':sets,'groups':groups}
def main():
 a=argparse.ArgumentParser();a.add_argument('--source',type=Path);a.add_argument('--check',action='store_true');args=a.parse_args()
 if args.check:
  root=HERE/'upstream';actual=generate(root);old=json.loads((HERE/'inventory.json').read_text());assert actual==old,'inventory drift'
  status_count=sum(len(r['currentStatus']) for g in actual['groups'] for r in ([g['schema']] if g['schema'] else [])+g['instances'])
  original_count=sum(len(ET.parse(root/p).getroot().findall('.//'+NS+'current')) for p in actual['testSets'])
  assert status_count==original_count and status_count>0,('status retention',status_count,original_count)
  files=json.loads((HERE/'resources.json').read_text());missing=[]
  for f in files:
   p=root/f['path']
   if not p.is_file():missing.append(f['path']);continue
   assert p.stat().st_size==f['bytes'] and digest(p)==f['sha256'],f['path']
  assert not missing,missing
  refs=[x for g in actual['groups'] for r in ([g['schema']] if g['schema'] else [])+g['instances'] for x in r['documents']]
  missing=actual['missingReferences'];assert all(m['classification']=='excluded' for m in missing),missing
  print(json.dumps(actual['counts'],sort_keys=True));print(f'PASS reproducible metadata, {len(files)} original file hashes, {len(refs)} ordered document references; missing original references={len(missing)} (all excluded), applicable missing=0');return
 assert args.source and subprocess.check_output(['git','-C',str(args.source),'rev-parse','HEAD'],text=True).strip()==REV
 assert not subprocess.check_output(['git','-C',str(args.source),'status','--porcelain'],text=True).strip()
 root=HERE/'upstream';files=[]
 for name in subprocess.check_output(['git','-C',str(args.source),'ls-files','-z']).decode().split('\0'):
  if not name:continue
  source=args.source/name;target=root/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
  files.append({'path':name,'bytes':source.stat().st_size,'sha256':digest(source)})
 write(HERE/'resources.json',files);inv=generate(root);write(HERE/'inventory.json',inv);print(json.dumps(inv['counts'],sort_keys=True));print(len(files),'original files imported')
if __name__=='__main__':main()
