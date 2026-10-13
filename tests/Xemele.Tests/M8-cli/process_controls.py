#!/usr/bin/env python3
"""Small real CLI controls; children inherit the accepted outer supervisor's owned group."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[3]
HOST = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
PREFIX = [HOST, '--fx-version', '10.0.11', str(ROOT/'src/Xemele.Tool/bin/Release/net10.0/Xemele.Tool.dll')]
END = time.monotonic()+105
CASES = 0

def run(args, data=b'', code=0, exact=None, contains=None):
    global CASES
    remaining = END-time.monotonic()
    if remaining <= 0: raise TimeoutError('CLI controls total deadline')
    child = subprocess.Popen(PREFIX+args, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    try:
        out, err = child.communicate(data, timeout=min(15, remaining))
    finally:
        if child.poll() is None: child.kill()
        child.wait(timeout=3)
        for stream in [child.stdin, child.stdout, child.stderr]:
            if stream is not None: stream.close()
    assert child.returncode == code, (args, child.returncode, out, err)
    if exact is not None: assert out == exact, (args, out)
    if contains is not None: assert contains in out, (args, out)
    if code: assert err, args
    CASES += 1
    return out, err

with tempfile.TemporaryDirectory(prefix='xemele-m8-cli-controls-') as directory:
    folder = Path(directory)
    for command in ['validate-rng', 'validate-xsd', 'validate-schematron']:
        run([command, '--help'], contains=b'Usage:')
    # Actual CLI/library expected outcomes agree on these separately authored positiveInteger cases.
    sources = {
        'rng': "<element xmlns='http://relaxng.org/ns/structure/1.0' name='root' datatypeLibrary='http://www.w3.org/2001/XMLSchema-datatypes'><data type='positiveInteger'/></element>",
        'xsd': "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:element name='root' type='xs:positiveInteger'/></xs:schema>",
        'schematron': "<schema xmlns='http://purl.oclc.org/dsdl/schematron'><pattern><rule context='root'><assert test='number(.) &gt; 0'>positive required</assert></rule></pattern></schema>"}
    for language, source in sources.items():
        path = folder/(language+'.xml'); path.write_text(source)
        args = ['validate-'+language, '--schema', str(path)]
        run(args, b'<root>7</root>', exact=b'')
        out, err = run(args+['--diagnostics','json'], b'<root>0</root>', code=1, exact=b'')
        diagnostics = json.loads(err)
        assert diagnostics['isComplete'] and not diagnostics['isValid'] and diagnostics['diagnostics']
        run(args, b'<root>', code=1, exact=b'')
        run(args+['--compile-max-operations','1'], b'<root>7</root>', code=1, exact=b'')
    path = folder/'schematron.xml'; report = folder/'report.xml'; report.write_text('preserve')
    args = ['validate-schematron', '--schema', str(path)]
    run(args+['--svrl',str(report)], b'<root>0</root>', code=1, exact=b''); assert report.read_text() == 'preserve'
    run(args+['--svrl',str(report)], b'<root>7</root>', exact=b''); assert 'schematron-output' in report.read_text()
    run(args+['--svrl','-'], b'<root>7</root>', contains=b'schematron-output')
    run(args+['--phase','missing'], b'<root>7</root>', code=2, exact=b'')
    # Closed stdout may fail after some bytes; the documented exit is unexpected failure3.
    reader, writer = os.pipe(); os.close(reader)
    child = subprocess.Popen(PREFIX+args+['--svrl','-'], stdin=subprocess.PIPE, stdout=writer, stderr=subprocess.PIPE)
    os.close(writer)
    try: _, error = child.communicate(b'<root>7</root>', timeout=15)
    finally:
        if child.poll() is None: child.kill()
        child.wait(timeout=3)
        if child.stdin is not None: child.stdin.close()
        if child.stderr is not None: child.stderr.close()
    assert child.returncode == 3 and error, (child.returncode, error)
    CASES += 1
    # Focused previous XML/HTML behavior on the same newly built real executable.
    run(['parse'], b'<r/>', exact=b'<r/>')
    run(['format'], b'<r><a/></r>', contains=b'\n  <a/>')
    run(['xpath','--expression','count(/r/a)'], b'<r><a/><a/></r>', exact=b'2\n')
    run(['canonicalize'], b'<r b="2" a="1"/>', exact=b'<r a="1" b="2"></r>')
    dtd = folder/'schema.dtd'; dtd.write_text('<!ELEMENT r EMPTY>')
    run(['validate-dtd','--dtd',str(dtd)], b'<r/>', exact=b'')
    run(['validate-dtd','--dtd',str(dtd)], b'<bad/>', code=1, exact=b'')
    childxml = folder/'child.xml'; childxml.write_text('<child/>')
    catalog = folder/'catalog.xml'; catalog.write_text(f"<catalog xmlns='urn:oasis:names:tc:entity:xmlns:xml:catalog'><uri name='urn:child' uri='{childxml.as_uri()}'/></catalog>")
    run(['catalog','--catalog',str(catalog),'--uri','urn:child'], exact=(childxml.as_uri()+'\n').encode())
    run(['xinclude','--catalog',str(catalog),'--allow-root',str(folder)], b'<r xmlns:xi="http://www.w3.org/2001/XInclude"><xi:include href="urn:child"/></r>', contains=b'<child')
    run(['html5'], b'<p>hello<br>world', exact=b'<html><head></head><body><p>hello<br>world</p></body></html>')
    run(['html-legacy'], b'<p>hello', contains=b'<p>hello</p>')
    run(['html-fragment','--context','div'], b'<p>one<p>two', exact=b'<p>one</p><p>two</p>')
    run(['html-select','--selector','p','--result','text'], b'<p>one&amp;two<p>three', exact=b'one&two\nthree\n')
    run(['parse','--input',str(folder/'absent')], code=2, exact=b'')
    run(['html5','--allow-root',str(folder)], b'<p>x', code=2, exact=b'')
    assert not list(folder.glob('.xemele-*.tmp'))
print(f'PASS real CLI: {CASES} controls, including prior XML/HTML and closed stdout; no counters')
