"""Freeze original-suite validating expectations independently of product/reference outcomes."""
import collections,hashlib,json,pathlib,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'docs/reports/M2-conformance-profile'))
from classify import verified,text

def build():
    prior_path=ROOT/'tests/corpora/xml/m2-applicability.json'
    prior=json.loads(prior_path.read_bytes());rows=[]
    for c in prior['cases']:
        source=text(verified(c['inputPath'],c['inputSha256']))
        # Comments/PIs cannot supply the source's DOCTYPE association.
        prolog=re.sub(r'<!--.*?-->|<\?.*?\?>','',source,flags=re.S)
        associated=bool(re.search(r'<!DOCTYPE\s',prolog))
        validity=c['sourceType'] in ('valid','invalid') and associated
        if validity:
            expected=['invalid' if c['sourceType']=='invalid' else 'valid']
            if c['expectedOutcomes']==['rejected']: expected=['rejected']
            scope='associated-dtd'
        else:
            expected=c['expectedOutcomes'];scope='prior-grammar-profile'
        rows.append({**{k:c[k] for k in ('id','inputPath','inputSha256','sourceUri','sourceType','rule','offlinePaths')},
                     'hasSourceDoctype':associated,'scope':scope,'expected':expected,
                     'basis':'Original TYPE plus source DOCTYPE and pinned effective XML10 grammar profile; no execution-derived exclusions.',
                     'priorGrammarExpected':c['expectedOutcomes']})
    return {'schemaVersion':1,'profile':'xml10-fifth-ns10-third-validating-associated-dtd-v1',
      'sourceUrl':'https://www.w3.org/XML/Test/xmlts20130923.tar.gz','archiveSha256':prior['archiveSha256'],
      'priorLedgerSha256':hashlib.sha256(prior_path.read_bytes()).hexdigest(),
      'normative':['https://www.w3.org/TR/2008/REC-xml-20081126/#sec-conformance','https://www.w3.org/TR/2008/REC-xml-20081126/#sec-prolog-dtd'],
      'files':prior['files'],'cases':rows,'counts':dict(collections.Counter(c['scope'] for c in rows))}
if __name__=='__main__':
    p=ROOT/'tests/corpora/dtd/applicability.json';value=build()
    if sys.argv[1:]==['--check']: assert json.loads(p.read_bytes())==value
    else:p.write_text(json.dumps(value,ensure_ascii=True,indent=2)+'\n')
    print(value['counts'])
