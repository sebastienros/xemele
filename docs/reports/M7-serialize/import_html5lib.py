"""Keep every original; independent normative graph projection, no product use."""
import collections,hashlib,json,pathlib
root=pathlib.Path(__file__).resolve().parents[3];folder=root/'tests/fixtures/M7-serialize/html5lib'
H='http://www.w3.org/1999/xhtml';void=set('area base basefont bgsound br col embed frame hr img input keygen link meta param source track wbr'.split());raw=set('style script xmp iframe noembed noframes plaintext'.split())
def escape(value,attribute=False):
 value=value.replace('&','&amp;').replace('\u00a0','&nbsp;')
 return value.replace('"','&quot;') if attribute else value.replace('<','&lt;').replace('>','&gt;')
def render(n,parent=None):
 k=n['kind']
 if k=='Text':return n['value'] if parent and parent['uri']==H and parent['name'] in raw else escape(n['value'])
 if k=='Comment':return '<!--'+n['value']+'-->'
 if k=='DocumentType':return '<!DOCTYPE '+n['name']+'>'
 tag=n['name'];attrs=''.join(' '+a['name']+'="'+escape(a['value'],True)+'"' for a in n['attributes'])
 start='<'+tag+attrs+'>'
 return start if n['uri']==H and tag in void else start+''.join(render(c,n) for c in n['children'])+'</'+tag+'>'
ledger=[]
for p in sorted(folder.glob('*.test')):
 for index,c in enumerate(json.loads(p.read_text())['tests']):
  tree=[];stack=[];issues=[];unmatched=[]
  for pos,t in enumerate(c['input']):
   kind=t[0];node=None
   if kind in ['StartTag','EmptyTag']:
    uri,name,attrs=(t[1],t[2],t[3]) if kind=='StartTag' else (H,t[1],t[2])
    attrs=[] if attrs=={} else [dict(name=a['name'],value=a['value'],uri=a.get('namespace') or '',prefix='') for a in attrs]
    if any(a['uri'] for a in attrs):raise ValueError('Namespace projection requires explicit prefix rule')
    node=dict(kind='Element',name=name,value='',uri=uri,prefix='',attributes=attrs,children=[])
   elif kind=='EndTag':
    if stack and stack[-1]['name']==t[2] and stack[-1]['uri']==t[1]:stack.pop()
    else:unmatched.append(dict(position=pos,token=t))
    continue
   elif kind=='Doctype':node=dict(kind='DocumentType',name=t[1],value='',public=t[2] if len(t)>2 else None,system=t[3] if len(t)>3 else None,attributes=[],children=[])
   elif kind in ['Characters','Comment']:node=dict(kind='Text' if kind=='Characters' else 'Comment',name='',value=t[1],attributes=[],children=[])
   else:raise ValueError(kind)
   (stack[-1]['children'] if stack else tree).append(node)
   if kind=='StartTag' and not (uri==H and name in void):stack.append(node)
  dangling=[n['name'] for n in stack]
  options=c.get('options',{})
  if options:issues.extend('unsupported option '+k+'='+repr(v) for k,v in sorted(options.items()) if k!='encoding')
  if p.name=='optionaltags.test':issues.append('original optional-tag minimization; frozen DOM emits every nonvoid start/end')
  if dangling:issues.append('token stream ends with open elements '+repr(dangling)+'; DOM outer serialization necessarily closes them')
  if unmatched:issues.append('unmatched end tokens have no DOM-node representation: '+repr(unmatched))
  expected=None if unmatched else ''.join(render(n) for n in tree)
  exact=expected is not None and expected in c['expected'] and not dangling
  if expected is not None and not exact:
   if any(n['kind']=='DocumentType' and (n['public'] is not None or n['system'] is not None) for n in tree):issues.append('current WHATWG doctype output retains name only')
   if any(a for n in tree if n['kind']=='Element' for a in n['attributes']):issues.append('current attributes use stable source order and double quotes; HTML5 escapes ampersand/NBSP/quote only')
  entry=dict(id=p.name+':'+str(index),source=p.name,index=index,description=c['description'],original=c,originalSha256=hashlib.sha256(json.dumps(c,sort_keys=True,separators=(',',':')).encode()).hexdigest(),classification='exact-original-overlap' if exact else 'normative-DOM-witness' if expected is not None else 'unrepresentable-token-control',reasons=issues,normative='WHATWG13.3 pinned SHA34e193906e6898ae7faf576a8d4c0f8de619a86ec99d85a3baeb2713171f1dbf; complete HTML fragment algorithm; contract outer markup',tree=tree if expected is not None else None,expected=expected,container='Document' if any(n['kind']=='DocumentType' for n in tree) else 'Fragment')
  if not exact and not issues:raise ValueError(entry)
  ledger.append(entry)
assert len(ledger)==230
out=root/'tests/fixtures/M7-serialize/html5lib-dispositions.json';out.write_text(json.dumps(ledger,indent=2,ensure_ascii=False)+'\n')
manifest={'commit':'c777c408b61078ea2eb4acefc2535f54dbc8b28a','files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(folder.iterdir())},'counts':dict(collections.Counter(c['classification'] for c in ledger)),'dispositionsSha256':hashlib.sha256(out.read_bytes()).hexdigest()}
(root/'docs/reports/M7-serialize/html5lib-provenance.json').write_text(json.dumps(manifest,indent=2)+'\n');print(manifest['counts'])
