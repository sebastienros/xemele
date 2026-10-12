#!/usr/bin/env python3
"""Reproduce pinned input/category facts independently of either XSD engine."""
import bisect, hashlib, json, re, xml.etree.ElementTree as E
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
CORPUS=ROOT/'tests/corpora/xsd';UPSTREAM=CORPUS/'upstream'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
 supplementary=json.loads((CORPUS/'supplementary-category-adjudication.json').read_text())
 lexical=json.loads((CORPUS/'lexical-oracle-adjudication.json').read_text())
 assert len(supplementary['cases'])==18 and len(lexical['cases'])==4
 ucd=CORPUS/'unicode-profile/UnicodeData.txt';assert digest(ucd)==supplementary['ucd_sha256']
 assert digest(UPSTREAM/'msMeta/Regex_w3c.xml')==supplementary['metadata_sha256']
 points={};ranges=[];start=None
 for line in ucd.read_text().splitlines():
  fields=line.split(';');cp=int(fields[0],16);name,category=fields[1:3]
  if name.endswith(', First>'):start=(cp,category)
  elif name.endswith(', Last>'):
   assert start and start[1]==category;ranges.append((start[0],cp,category));start=None
  else:points[cp]=category
 assert start is None
 ranges.sort();beginnings=[r[0] for r in ranges]
 def classify(cp):
  if cp in points:return points[cp]
  i=bisect.bisect_right(beginnings,cp)-1
  return ranges[i][2] if i>=0 and cp<=ranges[i][1] else 'Cn'
 inventory=json.loads((CORPUS/'inventory.json').read_text());originals={r['id']:(r,g) for g in inventory['groups'] for r in g['instances']}
 cases=supplementary['cases']+lexical['cases'];assert len({c['id'] for c in cases})==22
 hashes={};scalars=0
 for case in cases:
  original,group=originals[case['id']]
  assert original['expected']==case['original_expected'] and original['currentStatus']==case['original_status'],case['id']
  assert original['classification']=='applicable' and group['schema']['documents']==list(case['schema_documents']),case['id']
  assert original['documents']==list(case['documents']),case['id']
  for name,sha in (case['documents']|case['schema_documents']).items():
   assert digest(UPSTREAM/name)==sha,name;hashes[name]=sha
  if case not in supplementary['cases']:continue
  patterns=E.parse(UPSTREAM/next(iter(case['schema_documents']))).findall('.//{http://www.w3.org/2001/XMLSchema}pattern')
  assert len(patterns)==1 and patterns[0].get('value')==case['pattern'],case['id']
  elem=E.parse(UPSTREAM/next(iter(case['documents']))).find('.//elem');assert elem is not None
  value=elem.get('att') if case['value_location']=='elem/@att' else elem.text
  assert value is not None
  decoded=[{'codepoint':f'U+{ord(c):04X}','category':classify(ord(c))} for c in value]
  assert decoded==case['scalars'],case['id'];scalars+=len(decoded)
  pattern=case['pattern']
  if pattern in [r'\d',r'\D']:
   assert len(value)==1 and ((decoded[0]['category']=='Nd')==(pattern==r'\d')),case['id']
  else:
   category=re.fullmatch(r'\(?\\p\{([A-Z][a-z]?)\}\)?\*',pattern)
   assert category and all(c['category'].startswith(category[1]) for c in decoded),case['id']
  assert case['profile_expected']=='valid'
 print(f'PASS 22 original oracle/status/applicability records, {len(hashes)} unchanged schema/instance hashes, pinned metadata/UCD hashes; independently decoded/classified {scalars} scalars across18 exact patterns')
 print('Lexical expected validity is coordinator source adjudication; this check reproduces original input facts, not standards interpretation or product outcomes.')
if __name__=='__main__':main()
