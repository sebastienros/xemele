"""Pinned native graph output, never obtains expected strings from Xemele."""
import base64,hashlib,json,pathlib,subprocess,tempfile
root=pathlib.Path(__file__).resolve().parents[3]
report=root/'docs/reports/M7-serialize';fixtures=root/'tests/fixtures/M7-serialize'
native=pathlib.Path('/private/tmp/xemele-serialize-native');source=native/'source';build=native/'build';oracle=native/'oracle'
subprocess.run(['clang','-mmacosx-version-min=15.8','-I'+str(build),'-I'+str(source/'include'),'-I'+str(build/'include'),str(report/'oracle.c'),str(build/'libxml2.a'),'-lpthread','-lm','-o',str(oracle)],check=True)
H='http://www.w3.org/1999/xhtml';S='http://www.w3.org/2000/svg';M='http://www.w3.org/1998/Math/MathML';X='http://www.w3.org/XML/1998/namespace';N='http://www.w3.org/2000/xmlns/';L='http://www.w3.org/1999/xlink'
def text(v):return dict(kind='Text',name='',value=v,attributes=[],children=[])
def leaf(k,v,name=''):return dict(kind=k,name=name,value=v,attributes=[],children=[])
def el(name,children=[],attributes=[],uri=H,prefix=''):return dict(kind='Element',name=name,value='',uri=uri,prefix=prefix,attributes=attributes,children=children)
def dt(name='html',pub=None,sys=None):return dict(kind='DocumentType',name=name,value='',public=pub,system=sys,attributes=[],children=[])
def attr(name,value,uri='',prefix=''):return dict(name=name,value=value,uri=uri,prefix=prefix)
def hex(v):return '!' if v is None else v.encode('utf-8').hex() or '-'
def wire(tree):
 rows=[]
 def visit(n):
  k=n['kind']
  if k=='Element':
   uri=n.get('uri',H);rows.append('\t'.join(['E',hex(n['name']),hex('' if uri==H else uri),hex(n.get('prefix',''))]))
   for a in n['attributes']:
    if isinstance(a,list):a=attr(*a)
    rows.append('\t'.join(['A',hex(a['name']),hex(a['value']),hex(a.get('uri','')),hex(a.get('prefix',''))]))
   for c in n.get('ordinaryChildren',[])+n['children']:visit(c)
   rows.append('Z')
  elif k=='DocumentType':rows.append('\t'.join(['D',hex(n['name']),hex(n.get('public')),hex(n.get('system'))]))
  elif k=='ProcessingInstruction':rows.append('\t'.join(['P',hex(n['name']),hex(n['value'])]))
  else:rows.append('\t'.join(['T' if k=='Text' else 'C',hex(n['value'])]))
 for n in tree:visit(n)
 return '\n'.join(rows)+'\n'
original=json.loads((root/'tests/fixtures/M7-legacy/oracle.json').read_text());adapters=json.loads((root/'tests/fixtures/M7-legacy/adaptations.json').read_text())
cases=[]
for item in original:
 tree=item['tree'];cases.append(dict(id='legacy-original-'+item['id'],tree=adapters.get(item['id'],{}).get('managedTree',tree),originalTree=tree,adaptation='legacy-root-envelope-v1' if item['id'] in adapters else None))
voids='area base basefont bgsound br col embed frame hr img input isindex keygen link meta param source track wbr'.split()
raws='style script xmp iframe noembed noframes plaintext noscript textarea title'.split()
for name in voids+raws+['div','unknown']:
 cases.append(dict(id='native-factory-'+name,tree=[el(name,[text('<>&\u00a0é😀'),leaf('Comment','bad-->tail-'),leaf('ProcessingInstruction','x?>','Pi')])]))
for name in ['checked','compact','declare','defer','disabled','ismap','multiple','nohref','noresize','noshade','nowrap','readonly','selected','async','autofocus','required','unknown']:
 cases.append(dict(id='native-boolean-'+name,tree=[el('div',[],[attr(name,'no'),attr('data-empty','')])]))
for name in ['href','action','src','name','unknown']:
 for tag in ['a','div']:
  cases.append(dict(id='native-uri-'+tag+'-'+name,tree=[el(tag,[],[attr(name,' \t\n\r\f x <>&"é😀\x7f\x01'),attr(name+'2',' \t\n\r\f x <>&"é😀\x7f\x01')])]))
for name in ['br','script','g']:
 for uri in [S,M,'urn:custom','']:
  cases.append(dict(id='native-namespace-'+name+'-'+uri,tree=[el(name,[text('<>&é')],[attr('href','é',L,'link'),attr('lang','<>&"',X,'other')],uri,'p')]))
for data in ['', ' ', 'x?>tail', '\0after', '<&>\n\t', 'é😀']:
 cases.append(dict(id='native-pi-'+repr(data),tree=[leaf('ProcessingInstruction',data,'Target'),el('div',[leaf('ProcessingInstruction',data,'_T')])]))
for pub,sys in [(None,None),(None,'about:legacy-compat'),(None,''),('',''),('A "quote"','B'),('A "both\'quotes','S "both\'quotes'),(None,'<>&é'),('a\0tail','b')]:
 cases.append(dict(id='native-doctype-'+repr((pub,sys)),tree=[leaf('Comment','before'),dt('hTmL',pub,sys),el('html',[])]))
for data in ['', '\0after','a\0b','--','tail-','<>&é😀', '\x01\t\n\r\x7f\u00a0']:
 cases.append(dict(id='native-data-'+repr(data),tree=[el('div',[text(data),leaf('Comment',data)],[attr('title',data)])]))
cases.append(dict(id='native-html-prefix',tree=[el('script',[text('<>&')],[attr('x','v','','p')],H,'p')]))
for name in ['BR','SCRIPT','sTyLe','IsInDeX','a']:
 cases.append(dict(id='native-no-namespace-case-'+name,tree=[el(name,[text('<>&')],[attr('HREF','é'),attr('CHECKED','no'),attr('ſrc','é')],'','p')]))
cases.append(dict(id='native-raw-reset',tree=[el('script',[text('<>'),el('div',[text('<>&')]),text('<>&'),el('span',[text('<>&')])])]))
cases.append(dict(id='native-template-content',tree=[el('template',[el('script',[text('<>&')]),el('template',[leaf('ProcessingInstruction','','p'),text('<>')])])]))
dual=el('template',[leaf('ProcessingInstruction','','p'),el('b',[text('<>&')])]);dual['ordinaryChildren']=[el('script',[text('<>&')]),text('<ordinary>')]
cases.append(dict(id='native-template-dual-graph',tree=[dual]))
cases.append(dict(id='native-empty-document',tree=[]))
for item in cases:
 with tempfile.NamedTemporaryFile(mode='w',suffix='.wire') as f:
  f.write(wire(item['tree']));f.flush();item['output']=json.loads(subprocess.check_output([str(oracle),f.name],text=True))
  if item.get('adaptation'):
   f.seek(0);f.truncate();f.write(wire(item['originalTree']));f.flush();item['originalOutput']=json.loads(subprocess.check_output([str(oracle),f.name],text=True))
parsed_templates=[]
for item in original:
 if item['id']=='vector-028':
  with tempfile.NamedTemporaryFile() as f:
   f.write(base64.b64decode(item['bytes']));f.flush();output=json.loads(subprocess.check_output([str(oracle),'--parse',f.name],text=True))
  parsed_templates.append(dict(id=item['id'],input=item,expected=output))
(fixtures/'native-template-parser.json').write_text(json.dumps(parsed_templates,ensure_ascii=False,indent=2)+'\n')
(fixtures/'native.json').write_text(json.dumps(cases,ensure_ascii=False,indent=2)+'\n')
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
provenance={'commit':'96498992efa48d52b0e8b83058bd88dbdaf153c1','profile':'htmlNodeDumpFormatOutput UTF-8/no encoder/format=0; NODEFDTD|NOERROR|NOWARNING|NONET native-tree inputs','graphMapping':'managed HTML namespace maps to native null; other namespaces retain prefix/URI without invented nsDef; all attribute values including empty are explicit native text; legacy template ordinary children followed by template content project to native plain child content; fragment concatenates child node dumps; permanent shared owner retains no extra native metadata','nativeCases':len(cases),'source':{p:digest(source/p) for p in ['HTMLtree.c','xmlIO.c','HTMLparser.c','codegen/escape.inc']},'oracle.c':digest(report/'oracle.c'),'nativeArchive':digest(build/'libxml2.a'),'oracleBinary':digest(oracle),'nativeTemplateParserGoldens':digest(fixtures/'native-template-parser.json'),'nativeGoldens':digest(fixtures/'native.json'),'upstreamParserGoldens':digest(root/'tests/fixtures/M7-legacy/oracle.json'),'compiler':subprocess.check_output(['clang','--version'],text=True),'cmake':subprocess.check_output(['/private/tmp/xemele-serialize-cmake-env/bin/cmake','--version'],text=True),'config':(build/'CMakeCache.txt').read_text(),'macroHeader':(build/'libxml/xmlversion.h').read_text()}
provenance['actualPreprocessedFeatures']=[x for x in subprocess.check_output(['clang','-dM','-E','-x','c','-I'+str(build),'-I'+str(source/'include'),'-include','libxml/xmlversion.h','/dev/null'],text=True).splitlines() if x.startswith('#define LIBXML')]
provenance['sourceArchiveSha256']=hashlib.sha256(subprocess.check_output(['git','--git-dir=/private/tmp/xemele-reference-cache/libxml2.git','archive',provenance['commit']])).hexdigest()
(report/'native-provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
print('Pinned native goldens',len(cases))
