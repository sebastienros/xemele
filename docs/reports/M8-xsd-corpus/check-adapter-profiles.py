#!/usr/bin/env python3
"""Independent exact-byte and metadata checks; no engine output derives validity."""
import hashlib,json,xml.etree.ElementTree as E
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];CORPUS=ROOT/'tests/corpora/xsd';UPSTREAM=CORPUS/'upstream'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
 inventory=json.loads((CORPUS/'inventory.json').read_text());originals={r['id']:r for g in inventory['groups'] for r in ([g['schema']] if g['schema'] else [])+g['instances']}
 substitution=json.loads((CORPUS/'substitution-oracle-adjudication.json').read_text())['case'];r=originals[substitution['id']]
 assert r['expected']==substitution['original_expected'] and r['currentStatus']==[substitution['original_status']]
 assert r['documents']==[substitution['path']] and digest(UPSTREAM/substitution['path'])==substitution['sha256']
 supply=json.loads((CORPUS/'xml-namespace-profile.json').read_text());external=json.loads((CORPUS/'external-resources.json').read_text());resource=next(r for r in external if supply['resource']['url'] in r['urls'])
 assert resource['sha256']==supply['resource']['sha256'] and resource['bytes']==supply['resource']['bytes']
 assert digest(CORPUS/resource['path'])==resource['sha256'] and (CORPUS/resource['path']).stat().st_size==resource['bytes']
 assert not any(Path(r['path']).name.lower()=='xml.xsd' for r in json.loads((CORPUS/'resources.json').read_text()))
 for c in supply['cases']:
  schema=originals[c['group']+'/s:'+c['schema']];instance=originals[c['group']+'/i0:'+c['instance']]
  assert schema['expected']==instance['expected']=='valid'
  assert set(schema['documents']+instance['documents'])==set(c['documents'])
  for name,sha in c['documents'].items():assert digest(UPSTREAM/name)==sha,name
  imports=E.parse(UPSTREAM/schema['documents'][0]).findall('{http://www.w3.org/2001/XMLSchema}import')
  assert any(n.get('namespace')=='http://www.w3.org/XML/1998/namespace' and n.get('schemaLocation') is None for n in imports)
 adapter=json.loads((CORPUS/'adapter-boundaries.json').read_text());cases=adapter['late_hints']+[adapter['namespace_guard']]+adapter['fatal_xml']+[adapter['namechar_case']]
 assert len(adapter['late_hints'])==7 and len(adapter['fatal_xml'])==3 and len({c['id'] for c in cases})==12
 for c in cases:
  r=originals[c['id']];assert r['expected']==c['original_expected'] and r['currentStatus']==c['original_status'] and r['priorStatus']==c['original_prior']
  assert r['originalExpected']==c['original_expected_entries']
  assert set(r['documents'])==set(c['documents']) and c['baseline_passed'] is False
  for name,sha in (c['documents']|c['schema_documents']).items():assert digest(UPSTREAM/name)==sha,name
 for c in adapter['fatal_xml']:
  try:E.parse(UPSTREAM/next(iter(c['documents'])))
  except E.ParseError as e:print(c['id'],'independent malformed XML:',e)
  else:raise AssertionError('original unexpectedly parsed: '+c['id'])
 guard=adapter['namespace_guard'];tree=E.parse(UPSTREAM/next(iter(guard['documents'])));xsi='{http://www.w3.org/2001/XMLSchema-instance}'
 hints=[n.get(xsi+'schemaLocation') for n in tree.iter() if n.get(xsi+'schemaLocation')]
 assert 'ns-b schA8_b.xsd' in hints
 authored=guard['authored_hint'];assert authored['namespace']=='ns-b' and authored['location']=='schA8_b.xsd'
 assert digest(UPSTREAM/authored['schema_path'])==authored['schema_sha256']
 assert E.parse(UPSTREAM/authored['schema_path']).getroot().get('targetNamespace')==authored['actual_target_namespace']=='foo'
 namechar=adapter['namechar_case'];source=UPSTREAM/next(iter(namechar['documents']));assert digest(source)=='9065237d2161c4b02963afbc245e504459b2ce50317221a7120ff6bc65713915'
 assert source.read_text().splitlines()[34733]=='<value>&#x0346;</value>'
 assert E.parse(UPSTREAM/next(iter(namechar['schema_documents']))).find('.//{http://www.w3.org/2001/XMLSchema}pattern').get('value')==r'[\c]'
 assert any(n.text=='\u0346' for n in E.parse(source).getroot().iter('value'))
 print('PASS substitution/source status and hashes; explicit XML resource/catalog/originals;12 adapter boundary statuses/hashes;3 fatal XML inputs; authored namespace pair and exact SecondEdition NameChar0346 witness')
if __name__=='__main__':main()
