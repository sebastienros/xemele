"""Actual process comparison/security checks; generated transcripts stay ignored."""
import base64
import json
import os
from pathlib import Path
import shlex
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'artifacts/M0-references-libxml2'
LOG = []
ENV = dict(os.environ, XEMELE_LIBXML2_BRIDGE=str(REPORT / 'native-build/libxemele_libxml2.dylib'))
NATIVE = ['dotnet', str(ROOT / 'tools/ReferenceRunners/Libxml2/bin/Release/net10.0/Libxml2Reference.dll')]
BCL = ['dotnet', str(ROOT / 'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll')]


def run(command, timeout=600):
    result = subprocess.run(command, cwd=ROOT, env=ENV, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, timeout=timeout)
    entry = '$ ' + shlex.join(command) + '\n' + result.stdout + f'Exit: {result.returncode}\n'
    LOG.append(entry)
    print(entry, flush=True)
    assert result.returncode == 0, entry
    return result.stdout


def request(xml=b'<r a="1">ok</r>', **kwargs):
    if isinstance(xml, str):
        xml = xml.encode('utf-8')
    return dict(protocolVersion=1, caseId='fixture', operation='xml.events', profile='xml-1.0',
                inputBase64=base64.b64encode(xml).decode(), **kwargs)


def wire(command, cases):
    payload = '\n'.join(c if isinstance(c, str) else json.dumps(c, ensure_ascii=False) for c in cases) + '\n'
    result = subprocess.run(command, cwd=ROOT, env=ENV, input=payload.encode('utf-8'),
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30)
    assert result.returncode == 0, (result.returncode, result.stderr)
    assert not result.stderr, result.stderr
    lines = result.stdout.decode('utf-8', errors='strict').splitlines()
    assert len(lines) == len(cases), (len(lines), len(cases))
    assert all(len(line.encode('utf-16-le')) // 2 <= 1048576 for line in lines)
    responses = [json.loads(line) for line in lines]
    for response in responses:
        assert response['protocolVersion'] == 1
        assert all(isinstance(response['engine'][f], str) and response['engine'][f] for f in ['name', 'version', 'runtime', 'build'])
        if response['outcome'] != 'success':
            assert 'events' not in response and response['diagnostics']
    return responses, result.stdout


try:
    revision = run(['git', 'rev-parse', 'HEAD']).strip()
    run([sys.executable, '-B', 'tools/ReferenceRunners/Libxml2/build.py'])
    flags = ['--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
    for project in ['Libxml2/Libxml2Reference', 'Bcl/BclReference']:
        run(['dotnet', 'build', f'tools/ReferenceRunners/{project}.csproj', '-c', 'Release'] + flags)
    rich = '<!DOCTYPE r [<!ENTITY e "entity">]><r xmlns="urn:r" xmlns:p="urn:p" p:z="2" a="x&#10;y">A\r\n<![CDATA[B]]><!--ignore--><?p i?>&e;<p:é α="v"/><empty/>tail</r>'
    # UTF-16 ordinal namespace ordering puts supplementary U+10000 before BMP U+E000.
    ordinal = '<r xmlns:a="urn:𐀀" xmlns:b="urn:\ue000" b:x="2" a:x="1" é="3" z="4"/>'
    external = '<!DOCTYPE r [<!ENTITY e SYSTEM "https://example.invalid/a/entity">]><r>&e;</r>'
    nested = '<!DOCTYPE r SYSTEM "https://example.invalid/a/root.dtd"><r>&e;</r>'
    good = [request(), request(rich), request(ordinal), request('<r/>'), request('<r> \t\r\n</r>'),
            request('<?xml version="1.0"?><r>é𐀀</r>', encodingHint='utf-8'),
            request('<?xml version="1.0" encoding="utf-16"?><r>é</r>'.encode('utf-16')),
            request(external, offlineResources={'https://example.invalid/a/entity': base64.b64encode(b'text\r\n<e/>tail').decode()}),
            request(nested, offlineResources={
                'https://example.invalid/a/root.dtd': base64.b64encode(b'<!ENTITY e SYSTEM "entity">').decode(),
                'https://example.invalid/a/entity': base64.b64encode(b'nested').decode()}),
            request(external, offlineResources={'https://EXAMPLE.INVALID:443/a/entity': base64.b64encode(b'canonical').decode()}),
            request('<!DOCTYPE r [<!ATTLIST r a CDATA "default">]><r/>')]
    n, payload = wire(NATIVE, good)
    b, bpayload = wire(BCL, good)
    for index, (native, bcl) in enumerate(zip(n, b)):
        assert native['outcome'] == bcl['outcome'] == 'success', (index, native, bcl)
        assert native['events'] == bcl['events'], (index, native, bcl)
        assert native['engine']['name'] == 'libxml2' and native['engine']['version'] == '2.15.4'
    (REPORT / 'normalized-native.jsonl').write_bytes(payload)
    (REPORT / 'normalized-bcl.jsonl').write_bytes(bpayload)
    LOG.append(f'{len(good)} BCL-normalized equality cases passed: fixture, namespaces, UTF-16 ordinal, entities, defaults, CRLF, text coalescing, empty nodes, UTF8/UTF16, canonical/offline/nested resources.\n')
    known = request()
    bad = ['{bad json', 'null', 'x' * 1048577, dict(known, inputBase64='bad!'),
           dict(known, inputBase64=None), dict(known, protocolVersion=2), dict(known, operation='query'),
           dict(known, profile='xml-1.1'), dict(known, encodingHint='latin1'), dict(known, options={'x': True}),
           dict(known, offlineResources={'relative': 'AA=='}), dict(known, offlineResources={'urn:x': '!'}),
           dict(known, protocolVersion=2147483648), dict(known, options=[])]
    responses, payload = wire(NATIVE, bad + [known])
    assert all(r['outcome'] == 'runner-failure' for r in responses[:-1])
    assert responses[-1]['outcome'] == 'success'
    (REPORT / 'invalid-requests.jsonl').write_bytes(payload)
    LOG.append(f'{len(bad)} malformed/unsupported/oversize requests failed and stream recovered.\n')
    rejected = [request('<r>'), request(b'<r>\xff</r>'), request(b'<r>\xff</r>', encodingHint='utf-8'),
                request('<r>&unknown;</r>'), request('<r a="1" a="2"/>'), request('<?xml version="1.1"?><r/>'),
                request('<!DOCTYPE r [<!ENTITY x "&x;">]><r>&x;</r>'),
                request('<p:r/>'), request('<r xmlns:p=""/>'), request('<r xmlns:xml="urn:bad"/>'),
                request('<r xmlns="http://www.w3.org/2000/xmlns/"/>'), request('<r>&#0;</r>'), request('<r/><s/>'), request(b'')]
    responses, payload = wire(NATIVE, rejected + [known])
    assert all(r['outcome'] == 'parse-rejected' and r['diagnostics'][0]['category'] == 'xml' for r in responses[:-1]), responses
    assert responses[-1]['outcome'] == 'success'
    (REPORT / 'parse-rejections.jsonl').write_bytes(payload)
    denied = [request(external), request('<!DOCTYPE r [<!ENTITY e SYSTEM "file:///etc/passwd">]><r>&e;</r>'),
              request('<!DOCTYPE r SYSTEM "https://example.invalid/no.dtd"><r/>'),
              request(external, offlineResources={'https://example.invalid/wrong': 'AA=='}), request(nested)]
    responses, payload = wire(NATIVE, denied + [good[7], denied[0], known] * 20)
    for index, response in enumerate(responses):
        if index < len(denied) or (index - len(denied)) % 3 == 1:
            assert response['outcome'] == 'runner-failure' and response['diagnostics'][0]['category'] == 'resource-denied', response
        else:
            assert response['outcome'] == 'success', response
    (REPORT / 'resource-isolation.jsonl').write_bytes(payload)
    LOG.append('14 XML rejections and 65 resolver lifecycle cases passed; mapped resources never leak to subsequent requests; no file/network fallback.\n')
    amplification = '<!DOCTYPE r [<!ENTITY a "' + 'a'*1000 + '"><!ENTITY b "' + '&a;'*1000 + '">]><r>' + '&b;'*10 + '</r>'
    quota = [request(amplification), request('<r>' + 'x' * 80000 + '</r>' * 0 + '<e/>' * 70000 + '</r>')]
    responses, payload = wire(NATIVE, quota + [known])
    assert all(r['outcome'] == 'runner-failure' and r['diagnostics'][0]['category'] == 'limit' for r in responses[:-1]), responses
    assert responses[-1]['outcome'] == 'success'
    (REPORT / 'quota.jsonl').write_bytes(payload)
    LOG.append('Native entity amplification and managed event quota are runner-failure/limit, stream recovers.\n')
    unsupported = request('<?xml version="1.0" encoding="shift_jis"?><r/>')
    response = wire(NATIVE, [unsupported])[0][0]
    assert response['outcome'] == 'runner-failure' and response['diagnostics'][0]['category'] == 'protocol', response
    # Valid wire with malformed XML is distinct from malformed UTF8 in the wire itself.
    invalid_wire = json.dumps(known).encode().replace(b'fixture', b'\xff') + b'\n'
    for bytes_ in [invalid_wire, b'\xef\xbb\xbf' + invalid_wire]:
        result = subprocess.run(NATIVE, env=ENV, input=bytes_, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15)
        assert result.returncode == 2 and not result.stdout
        assert result.stderr.decode().splitlines() == ['Invalid UTF-8 protocol input.']
    LOG.append('Unsupported nonbuilt-in encoding failed explicitly; malformed raw UTF8 wire exits2 without response.\n')
    large = dict(known, caseId='x' * (1048576 - len(json.dumps(dict(known, caseId=''))) - 1))
    responses, _ = wire(NATIVE, [large, known])
    assert responses[0]['outcome'] == 'runner-failure' and responses[0]['diagnostics'][0]['category'] == 'limit'
    assert 'caseId' not in responses[0] and responses[1]['outcome'] == 'success'
    LOG.append('Oversized response identity falls back to bounded identity-free limit failure.\n')
    run([sys.executable, '-B', 'docs/reports/M0-references-protocol/verify.py'])
    run([sys.executable, '-B', 'tools/planning/tasks.py', 'validate'])
    LOG.append(f'Passed at {revision}; macOS arm64 reference infrastructure only; no parser performance/allocation claim.\n')
finally:
    REPORT.mkdir(parents=True, exist_ok=True)
    (REPORT / 'verification.txt').write_text('\n'.join(LOG))
