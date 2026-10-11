#!/usr/bin/env python3
"""Real managed/native CLI correctness controls, with bounded child cleanup."""
import json, os, pathlib, signal, subprocess, tempfile
ROOT = pathlib.Path(__file__).resolve().parents[3]
HOST = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
PREFIX = [os.environ['XEMELE_CLI_NATIVE']] if os.environ.get('XEMELE_CLI_NATIVE') else [HOST, '--fx-version', '10.0.11', str(ROOT/'src/Xemele.Tool/bin/Release/net10.0/Xemele.Tool.dll')]
CASES = []
def run(args, data=b'', code=0, exact=None, contains=None, error=None):
    process = subprocess.Popen(PREFIX+args, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
    try: out, err = process.communicate(data, timeout=30)
    finally:
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGTERM); process.wait(timeout=5)
    assert process.returncode == code, (args, process.returncode, out, err)
    assert not out.startswith(b'\xef\xbb\xbf'), (args, out)
    if exact is not None: assert out == exact, (args, out, exact)
    if contains is not None: assert contains in out, (args, out, contains)
    if code: assert err and b'\n   at ' not in err, (args, err)
    else: assert not err, (args, err)
    if error is not None: assert error in err, (args, err, error)
    CASES.append({'args':args,'code':code,'stdout_bytes':len(out)})
    return out
fixture = ROOT/'tests/fixtures/M7-cli'
with tempfile.TemporaryDirectory(prefix='xemele-m7-cli-') as directory:
    d = pathlib.Path(directory); output = d/'output.html'; source = d/'input.html'
    for command in ['html-legacy', 'html5', 'html-fragment', 'html-select']:
        run([command,'--help'],contains=b'Usage:')
    helptext = run(['--help'])
    for command in ['html-legacy','html5','html-fragment','html-select','parse','format','xpath','canonicalize','catalog','xinclude','validate-dtd']:
        assert command.encode() in helptext, command
    run(['html5'],b'<p>hello<br>world',exact=b'<html><head></head><body><p>hello<br>world</p></body></html>')
    run(['html-legacy'],b'<p>hello<br>world',contains=b'<p>hello<br>world</p>')
    run(['html-legacy','--mode','html5'],b'<p>hello',exact=b'<html><body><p>hello</p></body></html>')
    run(['html5','--mode','legacy'],b'<!doctype html><input disabled>',contains=b'<input disabled>')
    run(['html5','--mode','html5'],b'<!doctype html><input disabled>',contains=b'<input disabled="">')
    run(['html5'],b'<p>a\x00b',contains=b'<p>ab</p>')
    run(['html5'],b'<textarea>a\x00b</textarea>',contains='<textarea>a�b</textarea>'.encode())
    # Byte input uses HTML sniffing, never XML declarations or UTF-8-only coercion.
    run(['html5'],b'<p>\x80',contains='<p>€</p>'.encode())
    run(['html-legacy','--mode','html5'],b'<p>\x80',contains='<p>€</p>'.encode())
    run(['html5'],b'\xef\xbb\xbf<p>caf\xc3\xa9',contains='<p>café</p>'.encode())
    run(['html5'],b'<meta charset=utf-8><p>caf\xc3\xa9',contains='<p>café</p>'.encode())
    run(['html5'],b'<table>outside<tr><td>x</table>',contains=b'<body>outside<table><tbody><tr><td>x</td></tr></tbody></table>')
    run(['html5'],b'<svg><lineargradient/></svg>',contains=b'<svg><linearGradient></linearGradient></svg>')
    for flag, expected in [('false',b'<noscript><b>x</b></noscript>'),('true',b'<noscript><b>x</b></noscript>')]:
        run(['html5','--scripting',flag],b'<body><noscript><b>x</b></noscript>',contains=expected)
    run(['html-select','--selector','noscript > b','--scripting','false'],b'<body><noscript><b>x</b></noscript>',exact=b'<b>x</b>\n')
    run(['html-select','--selector','noscript > b','--scripting','true'],b'<body><noscript><b>x</b></noscript>',exact=b'')
    run(['html5'],b'<!DOCTYPE html SYSTEM "https://invalid.example/external.dtd"><p>&external;<script src="https://invalid.example/code.js">alert(1)</script>',contains=b'&amp;external;')
    run(['html-fragment','--context','div'],b'<p>one<p>two',exact=b'<p>one</p><p>two</p>')
    run(['html-fragment','--context','table'],b'<tr><td>cell',exact=b'<tbody><tr><td>cell</td></tr></tbody>')
    run(['html-fragment','--context','textarea'],b'&amp;<b>',exact=b'&amp;&lt;b&gt;')
    run(['html-fragment','--context','div'],b'\xef\xbb\xbf<p>caf\xc3\xa9',exact='<p>café</p>'.encode())
    run(['html-fragment','--context','div'],b'<p>\x80',code=1,exact=b'',error=b'HTML.CLI.Encoding:')
    run(['html-fragment','--context','svg','--context-namespace','http://www.w3.org/2000/svg'],b'<circle/>',exact=b'<circle></circle>')
    context = str(fixture/'context.html')
    run(['html-fragment','--context-file',context,'--context-selector','#rows'],b'<tr><td>cell',exact=b'<tr><td>cell</td></tr>')
    # A selected context carries its ancestor form pointer; the nested form start is ignored.
    run(['html-fragment','--context-file',context,'--context-selector','#inside'],b'<form><input></form>',exact=b'<input>')
    run(['html-fragment','--context','div'],b'<form><input></form>',exact=b'<form><input></form>')
    run(['html-fragment','--context-file',context,'--context-selector','#foreign'],b'<p>HTML',exact=b'<p>HTML</p>')
    for quirks in ['no-quirks','limited-quirks','quirks']:
        run(['html-fragment','--context','div','--quirks',quirks],b'<p>p<table><tr><td>x',contains=b'<table>')
    run(['html-fragment','--context-file',context,'--context-selector','missing'],b'<p>x',code=1,exact=b'',error=b'HTML.CLI.Rejected:')
    run(['html-fragment','--context-file',context,'--context-selector','div,tbody'],b'<p>x',code=1,exact=b'')
    run(['html-select','--input',str(fixture/'document.html'),'--selector','.item'],exact=b'<p class="item" id="one">one &amp; two</p>\n<p class="item" id="two">three<br><svg><circle></circle></svg><template><p>hidden</p></template>\n</p>\n')
    run(['html-select','--selector','p','--result','text'],b'<p>one&amp;two<p>three',exact=b'one&two\nthree\n')
    run(['html-select','--selector','missing'],b'<p>one',exact=b'')
    for profile in ['legacy','html5']:
        run(['html-select','--profile',profile,'--mode','html5','--selector','P'],b'<p id=a>A<p id=b>B',exact=b'<p id="a">A</p>\n<p id="b">B</p>\n')
    markup=b'<ul><li id=a class="x y" data-v="en-US"><span>A</span><li id=b class=x>B<li id=c>C</ul>'
    selectors={'#b':b'id="b"','.y':b'id="a"','ul > li:first-child':b'id="a"','li + li':b'id="b"','li ~ li':b'id="c"','li:nth-child(2n)':b'id="b"','li:not(.x)':b'id="c"','[data-v|=en]':b'id="a"','li:nth-last-of-type(1)':b'id="c"','li span':b'<span>A</span>','li[data-v^=en][data-v$=US][data-v*=n-][class~=y]':b'id="a"',':root':b'<html>'}
    for selector, expected in selectors.items(): run(['html-select','--selector',selector],markup,contains=expected)
    run(['html-select','--selector','#b,#a,#b'],b'<p id=a>A<p id=b>B',exact=b'<p id="a">A</p>\n<p id="b">B</p>\n')
    run(['html-select','--selector','p'],b'<template><p>hidden</p></template><p>visible',exact=b'<p>visible</p>\n')
    run(['html-select','--selector','s|circle','--css-namespace','s=http://www.w3.org/2000/svg'],b'<svg><circle/></svg>',exact=b'<circle></circle>\n')
    run(['html-select','--selector','circle','--css-namespace','=http://www.w3.org/2000/svg'],b'<svg><circle/></svg>',exact=b'<circle></circle>\n')
    for selector in ['p:has(b)','p:is(b)','p:where(b)','p::before','p:hover','p:lang(en)','p:dir(ltr)','[a=x i]','[a=x s]','p || b','li:nth-child(2 of .x)','p:not(div > b)','p:not(:not(b))','x|p','p, [']:
        run(['html-select','--selector',selector],b'<p>a',code=1,exact=b'',error=b'HTML.CLI.Selector: offset=')
    for command,extra in [('html5',[]),('html-legacy',[]),('html-fragment',['--context','div']),('html-select',['--selector','p'])]:
        output.write_text('preserve')
        run([command,*extra,'--max-characters','1','--output',str(output)],b'<p>long',code=1,exact=b'')
        assert output.read_text()=='preserve'
        run([command,*extra,'--max-token-characters','2'],b'<longtag>',code=1,exact=b'')
        run([command,*extra,'--max-depth','1'],b'<div><div><p>x',code=1,exact=b'')
    for args in [['html5','--input'],['html5','--mode','xml'],['html-select'],['html-select','--profile','xml','--selector','p'],['html-select','--result','json','--selector','p'],['html-select','--selector','p','--css-namespace','broken'],['html-select','--selector','p','--css-namespace','x=a','--css-namespace','x=b'],['html-fragment'],['html-fragment','--context-file',context],['html-fragment','--context','div','--context-file',context,'--context-selector','#inside'],['html-fragment','--context','div','--context-selector','p'],['html-fragment','--context','div','--quirks','unknown'],['html-legacy','--scripting','false'],['html-select','--profile','legacy','--scripting','false','--selector','p'],['html5','--scripting','yes'],['html5','--output','a','--output','b'],['html5','--max-depth','0'],['html5','--max-characters','-1'],['html5','--max-token-characters','2147483648'],['html5','--allow-dtd'],['html5','--allow-root',directory]]:
        run(args,code=2,exact=b'',error=b'HTML.CLI.Invocation:')
    run(['html5','--input',str(d/'missing')],code=2,exact=b'')
    run(['html5','--output',str(d/'missingdir/out')],b'<p>x',code=2,exact=b'')
    source.write_bytes(b'<p>file')
    run(['html5','--input',str(source),'--output',str(output)],exact=b'')
    assert output.read_bytes()==b'<html><head></head><body><p>file</p></body></html>'
    run(['html5','--input',str(output),'--output',str(output)],exact=b'')
    assert output.read_bytes()==b'<html><head></head><body><p>file</p></body></html>'
    assert not list(d.glob('.xemele-*.tmp'))
print(json.dumps({'passed':len(CASES),'cases':CASES},indent=2))
