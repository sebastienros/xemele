#!/usr/bin/env python3
"""Bounded CLI behavior controls; no timing or allocation measurement."""
import json, os, pathlib, signal, subprocess, tempfile
ROOT=pathlib.Path(__file__).resolve().parents[3]
HOST='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
TOOL=ROOT/'src/Xemele.Tool/bin/Release/net10.0/Xemele.Tool.dll'
PREFIX=[os.environ['XEMELE_CLI_NATIVE']] if os.environ.get('XEMELE_CLI_NATIVE') else [HOST,'--fx-version','10.0.11',str(TOOL)]
CASES=[]
def run(args, data=b'', code=0, contains=None, exact=None):
    process=subprocess.Popen(PREFIX+args,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,start_new_session=True)
    try: out,err=process.communicate(data,timeout=20)
    finally:
        if process.poll() is None:
            os.killpg(process.pid,signal.SIGTERM);process.wait(timeout=5)
    assert process.returncode==code,(args,process.returncode,out,err)
    assert not out.startswith(b'\xef\xbb\xbf'),args
    if contains is not None: assert contains in out,(args,out)
    if exact is not None: assert out==exact,(args,out)
    if code: assert err and b' at ' not in err,(args,err)
    CASES.append({'args':args,'code':code,'stdout_bytes':len(out)})
    return out
with tempfile.TemporaryDirectory(prefix='xemele-cli-') as directory:
    d=pathlib.Path(directory);allowed=d/'allowed';allowed.mkdir();outside=d/'outside';outside.mkdir()
    (allowed/'child.xml').write_text('<child>yes</child>')
    (outside/'secret.xml').write_text('<secret/>')
    (allowed/'link.xml').symlink_to(outside/'secret.xml');(allowed/'linkdir').symlink_to(outside,target_is_directory=True)
    input=d/'input.xml';input.write_text('<r><a>1</a><a>2</a></r>');output=d/'output.xml';output.write_text('preserve')
    dtd=d/'schema.dtd';dtd.write_text('<!ELEMENT r (a*)><!ELEMENT a (#PCDATA)>')
    catalog=d/'catalog.xml';catalog.write_text('<catalog xmlns="urn:oasis:names:tc:entity:xmlns:xml:catalog"><uri name="urn:child" uri="'+(allowed/'child.xml').as_uri()+'"/></catalog>')
    for command in ['parse','format','xpath','validate-dtd','canonicalize','catalog','xinclude']:run([command,'--help'],contains=b'Usage:')
    run(['parse'],b'<r/>',exact=b'<r/>')
    run(['format'],b'<r><a/></r>',contains=b'\n  <a/>')
    run(['xpath','--expression','count(/r/a)','--input',str(input)],exact=b'2\n')
    run(['xpath','--expression','/r/a','--input',str(input)],exact=b'<a>1</a>\n<a>2</a>\n')
    run(['xpath','--expression','/r/@v'],b'<r v="a&amp;b"/>',exact=b'v="a&amp;b"\n')
    run(['xpath','--expression','/r/text()'],b'<r>a&amp;b</r>',exact=b'a&amp;b\n')
    run(['xpath','--expression','/r/text()'],b'<r>]]&gt;</r>',exact=b']]&gt;\n')
    run(['xpath','--expression','/r/@v'],b'<r v="a&#x9;b&#xA;c&#xD;d"/>',exact=b'v="a&#x9;b&#xA;c&#xD;d"\n')
    run(['xpath','--expression','/r/a'],b'<r xmlns:p="urn:p"><a><p:b/></a></r>',contains=b'xmlns:p="urn:p"')
    run(['xpath','--expression','true()'],b'<r/>',exact=b'true\n')
    run(['canonicalize'],b'<r b="2" a="1"><!--x--></r>',exact=b'<r a="1" b="2"></r>')
    run(['canonicalize','--exclusive','--with-comments'],b'<r><!--x--></r>',exact=b'<r><!--x--></r>')
    run(['validate-dtd','--dtd',str(dtd),'--input',str(input)],exact=b'')
    run(['validate-dtd','--dtd',str(dtd)],b'<wrong/>',code=1,exact=b'')
    run(['catalog','--catalog',str(catalog),'--uri','urn:child'],exact=((allowed/'child.xml').as_uri()+'\n').encode())
    run(['catalog','--catalog',str(catalog),'--uri','urn:missing'],code=1,exact=b'')
    include=lambda href: ('<r xmlns:xi="http://www.w3.org/2001/XInclude"><xi:include href="'+href+'"/></r>').encode()
    run(['xinclude','--base-uri',(allowed/'primary.xml').as_uri(),'--allow-root',str(allowed)],include('child.xml'),contains=b'<child')
    run(['xinclude','--catalog',str(catalog),'--allow-root',str(allowed)],include('urn:child'),contains=b'<child')
    run(['xinclude','--catalog',str(catalog)],include('urn:child'),code=1,exact=b'')
    for uri in [(outside/'secret.xml').as_uri(),(allowed/'link.xml').as_uri(),(allowed/'linkdir/secret.xml').as_uri(),'https://invalid.example/child.xml']:
        run(['xinclude','--allow-root',str(allowed)],include(uri),code=1,exact=b'')
    run(['xinclude'],include((allowed/'child.xml').as_uri()),code=1,exact=b'')
    for command in ['parse','format','xinclude']:
        run([command,'--output',str(output)],b'<r>',code=1,exact=b'');assert output.read_text()=='preserve'
    run(['parse','--input',str(input),'--output',str(output)],exact=b'');assert output.read_bytes()==b'<r><a>1</a><a>2</a></r>'
    run(['parse','--input',str(output),'--output',str(output)],exact=b'');assert output.read_bytes()==b'<r><a>1</a><a>2</a></r>'
    run(['parse','--input',str(d/'missing')],code=2,exact=b'')
    run(['parse','--output',str(d/'missingdir/out')],b'<r/>',code=2,exact=b'')
    for args in [['parse','--exclusive'],['xpath'],['catalog'],['validate-dtd'],['parse','--input'],['parse','--output','a','--output','b'],['parse','--base-uri','relative'],['catalog','--catalog',str(catalog),'--uri','relative'],['unknown']]:run(args,code=2,exact=b'')
    run(['xpath','--expression','['],b'<r/>',code=1,exact=b'')
    run(['parse'],b'<!DOCTYPE r [<!ELEMENT r EMPTY>]><r/>',code=1,exact=b'')
    run(['parse','--allow-dtd'],b'<!DOCTYPE r [<!ELEMENT r EMPTY>]><r/>',exact=b'<r/>')
    assert not list(d.glob('.xemele-*.tmp'))
print(json.dumps({'passed':len(CASES),'cases':CASES},indent=2))
