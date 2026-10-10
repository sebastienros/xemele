#!/usr/bin/env python3
"""Deterministic, product-independent applicability contract from pinned originals.
No reference outcome is an input to classification. --check regenerates in memory.
"""
import argparse, collections, hashlib, json, os, pathlib, re, urllib.parse
ROOT=pathlib.Path(__file__).resolve().parents[3]
RESOURCES=pathlib.Path(os.environ.get('XEMELE_CORPUS_RESOURCES','/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources'))
BASE='https://xemele.invalid/corpus/'
XML='https://www.w3.org/TR/2008/REC-xml-20081126/'
NS='https://www.w3.org/TR/2009/REC-xml-names-20091208/'
LABELS={'UTF-8','UTF-16','UTF-16LE','UTF-16BE','US-ASCII','ISO-8859-1','WINDOWS-1252'}

def verified(relative, digest):
    p=pathlib.PurePosixPath(relative)
    if p.is_absolute() or '..' in p.parts or '\\' in relative: raise ValueError('unsafe resource path')
    target=RESOURCES / relative
    for part in [RESOURCES,*target.parents,target]:
        if part.is_symlink(): raise ValueError('symlink resource')
    data=target.read_bytes()
    if hashlib.sha256(data).hexdigest()!=digest: raise ValueError('resource hash mismatch: '+relative)
    return data

def text(data):
    # This decodes only for contract analysis, never replaces the parser's bytes.
    if data.startswith((b'\xff\xfe\x00\x00',b'\x00\x00\xfe\xff')): return data.decode('utf-32',errors='replace')
    if data.startswith((b'\xff\xfe',b'\xfe\xff')): return data.decode('utf-16',errors='replace')
    if data.startswith(b'\x00<'): return data.decode('utf-16-be',errors='replace')
    if data.startswith(b'<\x00'): return data.decode('utf-16-le',errors='replace')
    m=re.match(br'(?:\xef\xbb\xbf)?<\?xml\s[^?]*encoding\s*=\s*[\'"]([^\'"]+)',data)
    label=m[1].decode('ascii') if m else 'utf-8'
    try: return data.decode(label,errors='strict')
    except (UnicodeError,LookupError): return data.decode('utf-8',errors='surrogateescape')

def xmlchar(c):
    return c in (9,10,13) or 0x20<=c<=0xd7ff or 0xe000<=c<=0xfffd or 0x10000<=c<=0x10ffff

def build():
    inventory=json.loads((ROOT/'tests/corpora/xml/inventory.json').read_bytes())
    files={f['path']:f for f in inventory['files']}
    data={p:verified(p,f['sha256']) for p,f in files.items()}
    texts={p:text(b) for p,b in data.items()}
    rows=[]
    for c in inventory['cases']:
        a=c['attributes']; sid=c['sourceId']; p=c['inputPath']; t=texts[p]
        # Reachable lexical external identifiers are a provisioning superset;
        # actual resolver requests remain authoritative and undeclared opens fail.
        pending=[p]; seen=set(); missing=set()
        while pending:
            q=pending.pop()
            if q in seen: continue
            seen.add(q)
            cleaned=re.sub(r'<!--.*?-->','',texts[q],flags=re.S)
            for m in re.finditer(r'''\b(?:SYSTEM\s+|PUBLIC\s+(?:"[^"]*"|'[^']*')\s+)("[^"]*"|'[^']*')''',cleaned):
                uri=urllib.parse.urljoin(BASE+q,m[1][1:-1]); rel=urllib.parse.unquote(uri.removeprefix(BASE))
                if uri.startswith(BASE) and rel in files: pending.append(rel)
                else: missing.add(uri)
        if sid=='rmt-e2e-18': seen.update(['xmlconf/eduni/errata-2e/subdir1/E18-ent','xmlconf/eduni/errata-2e/E18-ent'])
        outcomes=['accepted'] if a['TYPE'] in ('valid','invalid') else ['rejected']
        reason='well-formedness' if a['TYPE']=='not-wf' else 'nonvalidating-validity' if a['TYPE']=='invalid' else 'valid-document'
        refs=[XML+'#sec-conformance', XML+'#sec-well-formed']
        rationale=c['description']+' Source sections: '+a.get('SECTIONS','unspecified')+'.'
        if a['TYPE']=='invalid': rationale+=' Validity alone is not a well-formedness failure; frozen nonvalidating profile does not validate DTD content/ID constraints.'
        if a['TYPE']=='error':
            outcomes=['accepted','rejected'];reason='optional-error';refs=[XML+'#dt-error']
            rationale+=' XML error (not fatal error): detection is optional; syntax/quota/infrastructure still distinguished.'
        if a.get('EDITION')=='1 2 3 4':
            outcomes=['accepted'];reason='fifth-edition-change';refs=[XML+'#NT-NameStartChar',XML+'#NT-VersionNum']
            rationale+=' Earlier-edition expectation superseded by Fifth Edition widened names/[26]; original bytes retained.'
        if a.get('VERSION')=='1.1' or a['RECOMMENDATION'] in ('XML1.1','NS1.1'):
            refs=[XML+'#sec-prolog-dtd',XML+'#charsets',XML+'#sec-line-ends',NS+'#nsc-NSDeclared']
            rationale+=' Apply effective 1.0 rules to original 1.x declaration; NEL/LSEP are not normalized to LF and are not S. Original XML1.1 output is not a 1.0 oracle.'
            reason='effective-xml10'
            # IBM C1 literal restrictions and external-version ordering are 1.1 rules.
            if 'ibm-1-1-not-wf-P02-' in sid:
                n=int(re.search(r'n(\d+)\.xml$',sid)[1])
                if 32<=n<=66: outcomes=['accepted']
            if 'ibm-1-1-not-wf-P77-' in sid:
                n=int(re.search(r'n(\d+)\.xml$',sid)[1])
                if n not in (14,16,17,18): outcomes=['accepted']
            if sid in ('rmt-001','rmt-002','rmt-003','rmt-004','rmt-005','rmt-011','rmt-013','rmt-041'): outcomes=['accepted']
            if sid in ('rmt-055','rmt-056','rmt-057','rmt-e2e-50'): outcomes=['rejected']
        # 1.0 Char applies to both literals and character references in every
        # parsed external resource; control references cannot bypass it.
        bad=[]; labels=[]
        for q in sorted(seen):
            s=texts[q]
            enc=re.match(r'\ufeff?<\?xml\s[^?]*encoding\s*=\s*[\'"]([^\'"]+)',s)
            if enc: labels.append({'path':q,'label':enc[1]})
            for ch in s:
                if not xmlchar(ord(ch)): bad.append({'path':q,'scalar':hex(ord(ch)),'kind':'literal'}); break
            cleaned=re.sub(r'<!--.*?-->|<!\[CDATA\[.*?\]\]>|<\?(?!xml\s).*?\?>','',s,flags=re.S)
            for m in re.finditer(r'&#(x[0-9A-Fa-f]+|[0-9]+);',cleaned):
                scalar=int(m[1][1:],16) if m[1].startswith('x') else int(m[1])
                if not xmlchar(scalar): bad.append({'path':q,'scalar':hex(scalar),'kind':'character-reference'});break
        # Some resources are declared but never parsed (unparsed/unused entity).
        # Fatal scalar evidence from such a resource needs explicit review rather
        # than pretending a lexical provisioning closure is the parse graph.
        if bad and (a.get('VERSION')=='1.1' or a['RECOMMENDATION']=='XML1.1'):
            outcomes=['rejected']; reason='xml10-char';refs=[XML+'#NT-Char',XML+'#wf-Legalchar']
        unsupported=[e for e in labels if e['label'].upper() not in LABELS]
        if unsupported:
            outcomes=['rejected'];reason='frozen-encoding';refs=[XML+'#charencoding','docs/decisions/M2/xml-resources.md#encoding-scope']
            rationale+=' Exact unsupported optional label(s): '+', '.join(e['label']+' in '+e['path'] for e in unsupported)+'. Profile rejection is required, not a corpus exclusion.'
        # Namespace-disabled provenance does not disable this product profile.
        # NMTOKENS colons are legal; all other ten namespace-no XML1.0 cases
        # have concrete QName/NCName violations in markup/DTD/PI/entity tokens.
        if a.get('NAMESPACE')=='no' and sid!='o-p08pass1':
            outcomes=['rejected'];reason='namespace-name';refs=[NS+'#ns-qualnames',NS+'#Conformance']
            rationale+=' Namespace-aware profile checks QName/NCName even when original test requested namespaces off.'
        if (c['metadataPath']=='xmlconf/sun/sun-invalid.xml' and re.fullmatch(r'inv-not-sa(?:0[1-9]|1[0-3])',sid)) or (c['metadataPath']=='xmlconf/ibm/ibm_oasis_invalid.xml' and 'P32-ibm32i' in sid):
            outcomes=['rejected'];reason='profile-standalone';refs=[XML+'#sec-rmd','docs/decisions/M2/xml-resources.md#core-and-metadata-integration']
            rationale+=' Frozen M2 profile explicitly enforces standalone dependencies, beyond optional nonvalidating validity reporting.'
        if a['RECOMMENDATION']=='NS1.1':
            if sid in ('rmt-ns11-003','rmt-ns11-004','rmt-ns11-005','ht-bh-ns11-007','ht-bh-ns11-008'):
                outcomes=['rejected'];reason='prefix-undeclaring';refs=[NS+'#nsc-NoPrefixUndecl']
            else: outcomes=['accepted'];reason='namespace-uri-optional-check';refs=[NS+'#Conformance']
        # Exact historical originals retain a second document element even
        # after their name character becomes legal. Edition metadata cannot
        # erase this independent [1] document violation.
        if c['metadataPath']=='xmlconf/ibm/ibm_oasis_not-wf.xml' and re.fullmatch(r'ibm-not-wf-P89-ibm89n(?:0[6-9]|1[0-2])\.xml',sid):
            outcomes=['rejected'];reason='historical-second-root';refs=[XML+'#NT-document']
            rationale+=' Inspected original has <animal/> followed by <IllegalExtender.../>: two document elements remain forbidden despite widened names.'
        namespace_evidence=[]
        for q in sorted(seen):
            cleaned=re.sub(r'<!--.*?-->|<!\[CDATA\[.*?\]\]>','',texts[q],flags=re.S)
            for m in re.finditer(r'<\?(?!xml\s)([^\s?]+)|<!ENTITY\s+(?:%\s+)?([^\s]+)|<!NOTATION\s+([^\s]+)',cleaned):
                name=next(g for g in m.groups() if g is not None)
                if ':' in name: namespace_evidence.append({'path':q,'name':name})
        if namespace_evidence:
            outcomes=['rejected'];reason='namespace-ncname';refs=[NS+'#Conformance']
            rationale+=' Exact colon-bearing PI/entity/notation Name tokens must be NCName: '+str(namespace_evidence)+'.'
        if sid in ('invalid-bo-7','invalid-bo-8','invalid-bo-9'):
            outcomes=['rejected'];reason='xml10-char';refs=[XML+'#NT-Char']
        rows.append({'id':c['id'],'inputPath':p,'inputSha256':c['inputSha256'],'sourceUri':BASE+urllib.parse.quote(p,safe='/'),
            'sourceType':a['TYPE'],'sourceRecommendation':a['RECOMMENDATION'],'sourceEdition':a.get('EDITION'),
            'sourceVersion':a.get('VERSION'),'sourceNamespace':a.get('NAMESPACE'),'expectedOutcomes':outcomes,
            'rule':reason,'normative':refs,'rationale':rationale,'encodingDeclarations':labels,'scalarEvidence':bad,
            'offlinePaths':sorted(seen-{p}),'unmappedLexicalIdentifiers':sorted(missing),'model':'effective-xml10-events-v1',
            'productStatus':'not-executed'})
    return {'schemaVersion':1,'profile':'xml10-fifth-ns10-third-nonvalidating-m2-encodings',
        'archiveSha256':inventory['archiveSha256'],'inventorySha256':hashlib.sha256((ROOT/'tests/corpora/xml/inventory.json').read_bytes()).hexdigest(),
        'optionalEncodingLabels':sorted(LABELS),'files':inventory['files'],'cases':rows,
        'summary':dict(collections.Counter(r['rule'] for r in rows))}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    result=build();path=ROOT/'tests/corpora/xml/m2-applicability.json'
    if args.check:
        if json.loads(path.read_bytes())!=result: raise SystemExit('classification drift')
    else: path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'cases':len(result['cases']),'resources':len(result['files']),'rules':result['summary']}))
