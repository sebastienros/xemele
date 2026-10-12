#!/usr/bin/env python3
"""Offline provenance integrity checks; no downloads, engine runs or measurements."""
import hashlib,json
from collections import Counter
from pathlib import Path
root=Path(__file__).resolve().parents[3]
corpus=root/'tests/corpora/schematron'
def check():
 inventory=json.loads((corpus/'inventory.json').read_text());ledger=json.loads((corpus/'applicability.json').read_text());cases=inventory['cases']
 assert inventory['revision']=='1d73a16524814e934eb76a18ee5c47b2c1727d11'
 assert len(cases)==50 and sum(c['schemaVariants'] for c in cases)==51
 assert {c['path'] for c in cases}=={str(f.relative_to(corpus)) for f in (corpus/'upstream').glob('*/*.xml')}
 assert len({c['id'] for c in cases})==50
 for c in cases+inventory['resources']:
  assert hashlib.sha256((corpus/c['path']).read_bytes()).hexdigest()==c['sha256'],c['id']
 assert {c['id'] for c in cases}=={c['id'] for c in ledger['cases']}
 assert ledger['missingCases']==ledger['missingVariants']==0 and not ledger['fullUpstreamConformanceClaim']
 for c in ledger['cases']:
  assert c['reason'] and c['classification']
  original=next(x for x in cases if x['id']==c['id']);assert c['upstreamExpect']==original['upstreamExpect']
 limitations=json.loads((corpus/'reference-limitations.json').read_text())['cases'];assert len(limitations)==1
 limitation=limitations[0];assert limitation['referenceValue']=='5' and limitation['normativeValue']=='4'
 assert hashlib.sha256(limitation['input'].encode()).hexdigest()==limitation['inputSha256']
 profile=json.loads((corpus/'profile.json').read_text());assert len(profile['cases'])==19
 assert len({c['id'] for c in profile['cases']})==19
 print('Pinned integrity:50 original cases/51 variants;19 authored cases;0 missing resources.',flush=True)
 return Counter(c['classification'] for c in ledger['cases'])
if __name__=='__main__':print(dict(check()))
