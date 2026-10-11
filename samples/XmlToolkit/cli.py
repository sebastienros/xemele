#!/usr/bin/env python3
"""Seven public CLI process examples with explicit local-resource grants."""
import json,os,signal,subprocess,tempfile,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[2]
art=root/'artifacts/M6-integration';art.mkdir(parents=True,exist_ok=True)
host='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
base=[host,'--fx-version','10.0.11',str(root/'src/Xemele.Tool/bin/Release/net10.0/Xemele.Tool.dll')]
commands=[]
with tempfile.TemporaryDirectory(prefix='xemele-m6-cli-integration-') as temp:
    folder=Path(temp);source=folder/'root.xml';source.write_text('<r><item>offline &amp; safe</item></r>')
    dtd=folder/'schema.dtd';dtd.write_text('<!ELEMENT r (item)><!ELEMENT item (#PCDATA)>')
    catalog=folder/'catalog.xml';catalog.write_text('<catalog xmlns="urn:oasis:names:tc:entity:xmlns:xml:catalog"><uri name="urn:offline:item" uri="root.xml"/></catalog>')
    def run(command,args):
        full=base+[command]+[str(a) for a in args];commands.append(full)
        with (art/('cli-'+command+'.stdout')).open('wb') as out,(art/('cli-'+command+'.stderr')).open('wb') as err:
            p=subprocess.Popen(full,stdout=out,stderr=err,start_new_session=True)
            try: code=p.wait(timeout=30)
            finally:
                try:os.killpg(p.pid,signal.SIGTERM)
                except ProcessLookupError:pass
                try:p.wait(timeout=10)
                except subprocess.TimeoutExpired:os.killpg(p.pid,signal.SIGKILL);p.wait(timeout=10)
                try:os.killpg(p.pid,signal.SIGKILL)
                except ProcessLookupError:pass
        assert code==0,(command,code)
        return (art/('cli-'+command+'.stdout')).read_text()
    assert run('parse',['--input',source])=='<r><item>offline &amp; safe</item></r>'
    assert run('format',['--input',source])=='<r>\n  <item>offline &amp; safe</item>\n</r>'
    assert run('xpath',['--input',source,'--expression','string(/r/item)'])=='offline & safe\n'
    assert run('validate-dtd',['--input',source,'--dtd',dtd])==''
    assert run('canonicalize',['--input',source])=='<r><item>offline &amp; safe</item></r>'
    assert run('catalog',['--catalog',catalog,'--uri','urn:offline:item'])==source.as_uri()+'\n'
    source.write_text('<r xmlns:xi="http://www.w3.org/2001/XInclude"><xi:include href="item.xml"/></r>')
    (folder/'item.xml').write_text('<item>offline &amp; safe</item>')
    result=ET.fromstring(run('xinclude',['--input',source,'--allow-root',folder]))
    assert result.tag=='r' and len(result)==1 and result[0].tag=='item' and result[0].text=='offline & safe'
(art/'cli-commands.json').write_text(json.dumps(commands,indent=2))
print('PASS: seven offline CLI process examples; XInclude explicitly grants one local root')
