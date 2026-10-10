#!/usr/bin/env python3
"""Full original-byte probe. Keeps only concise observations; no child payloads."""
import base64, collections, hashlib, json, os, pathlib, subprocess, sys
from classify import ROOT, RESOURCES, BASE, verified
DOTNET=os.environ.get('XEMELE_DOTNET','/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
ledger=json.loads((ROOT/'tests/corpora/xml/m2-applicability.json').read_bytes())
files={f['path']:f for f in ledger['files']}
runner=ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
product=ROOT/'samples/M2-conformance-profile/bin/Release/net10.0/Xemele.Tests.dll'

def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def model(events):
    """Only document-element descendants; expanded names, ordinal attributes,
    adjacent text+CDATA coalesced. Comments/PI/declaration/doctype/prefix and
    outside-root whitespace are deliberately not xml.events semantic fields.
    Lexical behavior belongs to a separate model, never erased by this oracle.
    """
    if not isinstance(events,list): raise ValueError('missing semantic events')
    result=[];depth=0
    for e in events:
        if not isinstance(e,dict) or e.get('kind') not in ('start','end','text'): raise ValueError('malformed semantic event')
        if e['kind'] in ('start','end') and any(not isinstance(e.get(k),str) for k in ('namespaceUri','localName')): raise ValueError('malformed expanded name')
        if e['kind']=='text' and not isinstance(e.get('value'),str): raise ValueError('malformed text')
        if e['kind']=='start' and (not isinstance(e.get('attributes'),list) or any(not isinstance(a,dict) or any(not isinstance(a.get(k),str) for k in ('namespaceUri','localName','value')) for a in e['attributes'])): raise ValueError('malformed attributes')
        if e['kind']=='start': depth+=1;result.append(e)
        elif e['kind']=='end': result.append(e);depth-=1
        elif depth:
            if result and result[-1]['kind']=='text': result[-1]['value']+=e['value']
            else: result.append(e.copy())
        elif e['value'].strip(' \t\r\n'): raise ValueError('non-whitespace outside root')
    if depth: raise ValueError('unbalanced model')
    return result

provenance={'sdk':subprocess.check_output([DOTNET,'--version'],text=True).strip(),
    'runtimes':subprocess.check_output([DOTNET,'--list-runtimes'],text=True).strip().splitlines(),
    'bclSourceSha256':digest(ROOT/'tools/ReferenceRunners/Bcl/Program.cs'),'bclBinarySha256':digest(runner),
    'protocolSourceSha256':digest(ROOT/'tools/ReferenceRunners/Protocol/Contract.cs'),
    'productDriverSourceSha256':digest(ROOT/'samples/M2-conformance-profile/Program.cs'),
    'productDriverBinarySha256':digest(product),'productBinarySha256':digest(product.parent/'Xemele.dll')}
provenance['bclFullSourceInputs']={str(p):digest(ROOT/p) for p in ['tools/ReferenceRunners/Bcl/Program.cs','tools/ReferenceRunners/Bcl/BclReference.csproj','tools/ReferenceRunners/Protocol/Contract.cs','tools/ReferenceRunners/Protocol/ReferenceProtocol.csproj','Directory.Build.props','Directory.Packages.props','global.json']}
legacy=json.loads((ROOT/'docs/reports/M1-corpus/reference-limitations.json').read_bytes())
legacy_ids={c['id']:c['sha256'] for c in legacy['cases']}
legacy_observed=[]
counts=collections.Counter();rows=[];refgaps=[];engines=set()
children=[subprocess.Popen([DOTNET,str(p)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True) for p in (product,runner)]
try:
    for c in ledger['cases']:
        resources={BASE+path:base64.b64encode(verified(path,files[path]['sha256'])).decode() for path in c['offlinePaths']}
        request=json.dumps({'protocolVersion':1,'caseId':c['id'],'operation':'xml.events','profile':'xml-1.0',
            'inputBase64':base64.b64encode(verified(c['inputPath'],c['inputSha256'])).decode(),
            'options':{'sourceUri':c['sourceUri']},'offlineResources':resources},ensure_ascii=True,separators=(',',':'))
        if len(request)>1048576: raise ValueError('wire limit: '+c['id'])
        responses=[]
        for child in children:
            child.stdin.write(request+'\n');child.stdin.flush()
            response=json.loads(child.stdout.readline())
            if response.get('caseId')!=c['id'] or response.get('outcome') not in ('success','parse-rejected','runner-failure'): raise ValueError('identity/outcome mismatch')
            if child is children[1] and (response.get('protocolVersion')!=1 or response.get('operation')!='xml.events' or not isinstance(response.get('diagnostics'),list) or any(not isinstance(response.get('engine',{}).get(k),str) for k in ('name','version','runtime','build'))): raise ValueError('reference protocol mismatch')
            if response['outcome']=='success': model(response.get('events'))
            elif response.get('events') is not None: raise ValueError('failure carried semantic events')
            responses.append(response)
        p,r=responses
        if c['id'] in legacy_ids:
            if c['inputSha256']!=legacy_ids[c['id']] or r['outcome']!=legacy['referenceOutcome'] or any(r['engine'][k]!=v for k,v in legacy['engine'].items()): raise ValueError('exact legacy limitation changed')
            legacy_observed.append(c['id'])
        engines.add(json.dumps(r['engine'],sort_keys=True))
        expected=c['expectedOutcomes']; outcome={'success':'accepted','parse-rejected':'rejected'}.get(p['outcome'],'infrastructure-failure')
        ref={'success':'accepted','parse-rejected':'rejected'}.get(r['outcome'],'infrastructure-failure')
        counts['product:'+outcome]+=1;counts['reference:'+ref]+=1
        mismatch=outcome not in expected
        comparison='unavailable'
        if outcome=='accepted' and ref=='accepted':
            comparison='matched' if model(p['events'])==model(r['events']) else 'model-disagreement'
            counts[comparison]+=1
        row={'id':c['id'],'inputSha256':c['inputSha256'],'expectedOutcomes':expected,'productOutcome':outcome,
            'productDiagnostic':p.get('diagnostic'),'referenceOutcome':ref,'referenceCategory':r['diagnostics'][0]['category'] if r['diagnostics'] else None,
            'comparison':comparison}
        if mismatch or comparison=='model-disagreement': rows.append(row)
        if ref not in expected:
            refgaps.append(row | {'referenceObservation':r['diagnostics'][0]['message'][:240] if r['diagnostics'] else 'reference accepted'})
        counts['product-expectation-match' if not mismatch else 'product-expectation-gap']+=1
finally:
    for child in children:
        child.stdin.close();child.wait(timeout=10);child.stdout.close()
report={'schemaVersion':1,'provenance':provenance,'engines':[json.loads(e) for e in sorted(engines)],
    'legacyVersionSuffixLimitations':legacy_observed,'counts':dict(counts),'productGaps':rows,'referenceDisagreements':refgaps,
    'claim':'observations only; matching outcomes alone do not establish full product conformance'}
path=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'docs/reports/M2-conformance-profile/observations.json'
path.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report['counts'],sort_keys=True));print('reference disagreements:',len(refgaps),'product ledger:',len(rows))
