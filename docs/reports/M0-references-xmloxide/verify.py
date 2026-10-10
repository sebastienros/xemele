"""Actual pinned process checks and BCL comparison; no parser performance claims."""
import base64
import json
import os
from pathlib import Path
import shlex
import subprocess

ROOT = Path(__file__).resolve().parents[3]
CACHE = ROOT / 'artifacts/M0-references-xmloxide'
CACHE.mkdir(parents=True, exist_ok=True)
LOG = []
COUNT = 0
RUNNER = [str(CACHE / 'target/release/xemele-xmloxide-reference')]
BCL = ['dotnet', str(ROOT / 'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll')]


def run(args, env=None, timeout=300):
    p = subprocess.run(args, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                       text=True, timeout=timeout)
    entry = '$ ' + shlex.join(args) + '\n' + p.stdout + f'Exit: {p.returncode}\n'
    LOG.append(entry)
    print(entry, flush=True)
    assert p.returncode == 0, entry
    return p.stdout


def request(xml='<r/>', **kw):
    data = xml.encode() if isinstance(xml, str) else xml
    return dict(protocolVersion=1, caseId='fixture', operation='xml.events', profile='xml-1.0',
                inputBase64=base64.b64encode(data).decode(), **kw)


def wire(cases, command=RUNNER):
    data = b''.join((c if isinstance(c, bytes) else c.encode() if isinstance(c, str)
                    else json.dumps(c, ensure_ascii=False).encode()) + b'\n' for c in cases)
    p = subprocess.run(command, cwd=ROOT, input=data, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20)
    assert p.returncode == 0 and not p.stderr, (p.returncode, p.stderr)
    lines = p.stdout.decode('utf-8', errors='strict').splitlines()
    assert len(lines) == len(cases), (len(lines), len(cases))
    responses = []
    for line in lines:
        assert len(line.encode('utf-16-le')) // 2 <= 1048576
        r = json.loads(line)
        assert r['protocolVersion'] == 1
        assert all(isinstance(r['engine'][k], str) and r['engine'][k] for k in ['name','version','runtime','build'])
        if r['outcome'] == 'success': assert r['diagnostics'] == [] and isinstance(r['events'], list)
        else:
            assert r['outcome'] in ['parse-rejected','runner-failure'] and 'events' not in r
            assert r['diagnostics'] and all(d['category'] in ['xml','protocol','resource-denied','infrastructure','limit'] and d['message'] for d in r['diagnostics'])
        responses.append(r)
    global COUNT
    COUNT += len(cases)
    return responses


def expect(case, outcome, category=None):
    r = wire([case])[0]
    assert r['outcome'] == outcome, r
    if category: assert r['diagnostics'][0]['category'] == category, r
    return r


def compare(case):
    a, b = wire([case])[0], wire([case], BCL)[0]
    assert a['outcome'] == b['outcome'] == 'success', (a, b)
    assert a['events'] == b['events'], (a['events'], b['events'])
    assert a['engine']['name'] == 'xmloxide' and '555055f1ea6bf9944488fd96415b6f03b5b43696' in a['engine']['version']
    return a


try:
    revision = run(['git','rev-parse','HEAD']).strip()
    run(['python3','-B','tools/ReferenceRunners/Xmloxide/build.py','--offline'])
    env = dict(os.environ, CARGO_TARGET_DIR=str(CACHE / 'target'), XEMELE_BUILD_REVISION=revision)
    run(['cargo','+1.95.0','test','--release','--locked','--offline','--manifest-path','tools/ReferenceRunners/Xmloxide/Cargo.toml'],env)
    flags = ['--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
    run(['dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj','-c','Release'] + flags)
    fixtures = [
        "<p:r xmlns:p='urn:p' z='2' a='1'>a\r\nb<![CDATA[c]]>&amp;<p:e/></p:r>",
        "<?xml version='1.0'?><!--before--><r xmlns='urn:default' xmlns:p='urn:p' p:b='2' b='1'><n xmlns=''> x </n><?pi y?><!--after--></r>",
        "<!DOCTYPE r [<!ENTITY e '<n/>text'>]><r>a&e;b</r>",
        "<r a='a\nb\tc&#xA;d'>&#x1F600;<![CDATA[x\ny]]></r>",
        # Namespace URIs force UTF-16 ordering opposite Rust scalar/UTF-8 ordering.
        "<r xmlns:a='urn:\U00010000' xmlns:b='urn:\ue000' b:x='1' a:x='2' é='a' Ω='b'/>" ,
        "<!DOCTYPE r [<!-- % SYSTEM 'fake' --> <!ENTITY e '100&#37; SYSTEM'>]><r>&e;<![CDATA[<!DOCTYPE x SYSTEM 'file:///fake'>]]></r>",
        "<r>before<!--ignore--><?ignore x?>after</r>",
        "<r>\r\n<n/>\r\n</r>",
    ]
    outputs = [compare(request(xml)) for xml in fixtures]
    # Preserve and report actual engine divergence; no fabricated correction of upstream output.
    baseline = json.loads((Path(__file__).parent/'fixtures/normalization-baseline.json').read_text())
    divergence = request(baseline['xml'])
    a,b=wire([divergence])[0],wire([divergence],BCL)[0]
    assert a['outcome']==b['outcome']=='success'
    def standard_events_match(events):
        return events == baseline['expectedEvents']
    assert standard_events_match(b['events'])
    # Adversarial oracle regression: reference divergence is observation only.
    # It must never replace the independent expected events for a product result.
    assert not standard_events_match(a['events'])
    wrong = json.loads(json.dumps(b['events']))
    wrong[1]['value'] = '😀x\r\ny'
    assert not standard_events_match(wrong)
    LOG.append('Two adversarial standard-oracle checks: pinned wrong events and a mutation of correct BCL events both fail independent expected events. Zero Xemele parser tests executed.\n')
    assert a['events'][0]['attributes'][0]['value']=='a  b'
    assert b['events'][0]['attributes'][0]['value']=='a b'
    assert a['events'][1]['value']=='😀x\r\ny' and b['events'][1]['value']=='😀x\ny'
    LOG.append('Pinned engine divergence retained: literal CRLF attributes produce two spaces; CDATA CRLF retained. BCL produces one space and LF respectively.\n')
    (CACHE/'divergence-responses.json').write_text(json.dumps({'xmloxide':a,'bcl':b},ensure_ascii=False,indent=2)+'\n')
    a,b=wire([request('\r\n<r/>\r\n')])[0],wire([request('\r\n<r/>\r\n')],BCL)[0]
    assert a['events'][0]['kind']=='start' and b['events'][0]=={'kind':'text','value':'\n'}
    LOG.append('Pinned DOM drops leading prolog whitespace; retained actual output, no equality claim for that case.\n')
    compare(request(b'\xef\xbb\xbf<r>bom</r>'))
    compare(request('<?xml version="1.0" encoding="utf-16"?><r>é</r>'.encode('utf-16')))
    compare(request('<?xml version="1.0" encoding="iso-8859-1"?><r>é</r>'.encode('latin1')))
    compare(request('<r>😀</r>', encodingHint='utf-8'))
    resources = {'urn:offline:entity':base64.b64encode(b"<n xmlns='urn:n' a='1'/>mapped&amp;").decode()}
    offline = request("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:offline:entity'>]><r>a&e;b</r>", offlineResources=resources)
    outputs.append(compare(offline))
    per_request = wire([offline, request("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:offline:entity'>]><r>&e;</r>")])
    assert per_request[0]['outcome']=='success' and per_request[1]['diagnostics'][0]['category']=='resource-denied'
    for uri in ['file:///offline/entity.xml','https://offline.example/entity.xml']:
        compare(request(f"<!DOCTYPE r [<!ENTITY e SYSTEM '{uri}'>]><r>&e;</r>",offlineResources={uri:base64.b64encode(b'offline').decode()}))
    expect(request("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:malformed'>]><r>&e;</r>",offlineResources={'urn:malformed':base64.b64encode(b'\xff').decode()}),'parse-rejected','xml')
    # A resolver owns each request: a subsequent request gets no prior mappings.
    expect(request("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:offline:entity'>]><r>&e;</r>"),'runner-failure','resource-denied')
    expect(request("<!DOCTYPE r [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><r>&e;</r>"),'runner-failure','resource-denied')
    expect(request("<!DOCTYPE r [<!ENTITY e SYSTEM 'https://127.0.0.1:9/secret'>]><r>&e;</r>"),'runner-failure','resource-denied')
    # Explicit unsupported subsets are recognized from actual parser nodes, never matching text/comments.
    for xml in ["<!DOCTYPE r SYSTEM 'urn:offline:dtd'><r/>", "<!DOCTYPE r PUBLIC 'id' 'urn:offline:dtd'><r>&x;</r>",
                "<!DOCTYPE r SYSTEM 'file:///fake' [<!-- ' ] SYSTEM --> <!ENTITY e 'x'>]><r>&e;</r>",
                "<!DOCTYPE r [<!ENTITY % e 'stuff'> %e;]><r/>"]:
        expect(request(xml,offlineResources={'urn:offline:dtd':base64.b64encode(b"<!ENTITY x 'mapped'>").decode()}),'runner-failure','protocol')
    expect(request("<!DOCTYPE r [<!ENTITY e SYSTEM 'relative.ent'>]><r>&e;</r>"),'runner-failure','protocol')
    # Nested replacement calls the pinned engine resolver; it supplies no base URI.
    nested = request("<!DOCTYPE r [<!ENTITY a SYSTEM 'urn:offline:a'><!ENTITY b SYSTEM 'relative.ent'>]><r>&a;</r>",
                     offlineResources={'urn:offline:a':base64.b64encode(b'&b;').decode()})
    expect(nested,'runner-failure','protocol')
    for xml in ['<r>','<r>&missing;</r>',"<r a='1' a='2'/>",'<p:r/>','<r>\x00</r>', '<r>&maximum_entity_amplification_factor_exceeded;</r>']:
        expect(request(xml),'parse-rejected','xml')
    expect(request(b'<r>\xff</r>'),'parse-rejected','xml')
    expect(request(b'<r>\xff</r>',encodingHint='utf-8'),'parse-rejected','xml')
    for field,value in [('protocolVersion',2),('protocolVersion',2147483648),('protocolVersion',1.5),('protocolVersion','1'),
                        ('caseId',' '),('caseId',4),('operation','xml.tree'),('profile','xml-1.1'),
                        ('encodingHint','latin1'),('options',{'x':True}),('options',[]),('inputBase64','?'),
                        ('inputBase64',None),('offlineResources',{'relative':'AA=='}),('offlineResources',{'urn:x':'?'}),('offlineResources',[])]:
        expect(dict(request(),**{field:value}),'runner-failure','protocol')
    for raw in ['null','[]','{bad json','{"protocolVersion":1e999}']:
        expect(raw,'runner-failure','protocol')
    additive = dict(request(), extra={'future':'accepted'})
    expect(additive,'success')
    for xml in ['<!DOCTYPE r [<!ELEMENT r '+ '(' * 257 + 'r' + ')' * 257 + '>]><r/>', "<!DOCTYPE r [<!ENTITY e '<"+'a'*50001+"/>'>]><r>&e;</r>", '<r>'*257+'</r>'*257, '<'+'a'*50001+'/>', '<!DOCTYPE r [<!ENTITY e "x">]><r>'+'&e;'*10001+'</r>']:
        expect(request(xml),'runner-failure','limit')
    # Huge mapped resource is rejected at request framing, before resolver allocation.
    expect(request("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:e'>]><r>&e;</r>", offlineResources={'urn:e':base64.b64encode(b'x'*1048577).decode()}),'runner-failure','limit')
    recovery = wire(['x'*(4*1048576+1),request()])
    assert recovery[0]['outcome'] == 'runner-failure' and recovery[0]['diagnostics'][0]['category']=='limit' and recovery[1]['outcome']=='success'
    huge = dict(request(),caseId='x'*1048330)
    assert len(json.dumps(huge)) < 1048576
    expect(huge,'runner-failure','limit')
    for invalid in [b'{"caseId":"\xff"}', b'x'*(4*1048576)+b'\xff', b'\xe0\x80\x80', b'\xed\xa0\x80', b'\xf4\x90\x80\x80', b'\xf0\x9f', b'\xef\xbb\xbf'+json.dumps(request()).encode()]:
        p = subprocess.run(RUNNER,input=invalid+b'\n',stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=15)
        if invalid.startswith(b'\xef'): assert p.returncode==0 and json.loads(p.stdout)['outcome']=='runner-failure'
        else: assert p.returncode==2 and not p.stdout and p.stderr==b'Invalid UTF-8 protocol input.\n'
    expect(dict(request(),caseId='😀'*524288),'runner-failure','limit')
    # UTF-16 ordinal sorting also tested with supplementary local names if BCL supports them.
    supplemental = request("<r \U00010000='2' \uf900='1'/>")
    a,b=wire([supplemental])[0],wire([supplemental],BCL)[0]
    if b['outcome']=='success': assert a['events']==b['events']
    else: assert b['outcome']=='parse-rejected'; LOG.append('BCL rejects supplementary XML local name; no equality claim for that case. Namespace URI supplementary ordering comparison passed.\n')
    (CACHE/'fixture-responses.json').write_text(json.dumps(outputs,ensure_ascii=False,indent=2)+'\n')
    LOG.append(f'{COUNT} actual response checks passed; metadata and events in fixture-responses.json.\n')
    run(['python3','-B','docs/reports/M0-references-protocol/verify.py'])
    run(['python3','tools/planning/tasks.py','validate'])
finally:
    (CACHE/'verification.txt').write_text('\n'.join(LOG))
