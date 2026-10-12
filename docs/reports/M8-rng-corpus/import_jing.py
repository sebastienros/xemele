"""Reproducibly split the pinned upstream XML suite; preserve authored oracles."""
import hashlib,json,shutil,sys
from pathlib import Path
from xml.dom import minidom
ROOT=Path(__file__).resolve().parents[3]; CORPUS=ROOT/'tests/corpora/relaxng'
REV='a6bc0041035988325dfbfe7823ef2c098fc56597'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def split(upstream):
 original=upstream/'mod/rng-validate/test/spectest.xml'
 target=CORPUS/'upstream/jing';target.mkdir(parents=True,exist_ok=True)
 for name,path in [('spectest.xml',original),('copying.txt',upstream/'copying.txt'),('index.html',upstream/'test/index.html'),('prep.xsl',upstream/'test/prep.xsl')]:shutil.copyfile(path,target/name)
 doc=minidom.parse(str(original));cases=[];resources=[]
 def elements(node):return [n for n in node.childNodes if n.nodeType==n.ELEMENT_NODE]
 def content(wrapper):
  # As upstream prep.xsl copy-of(node()), retain comments/PI/whitespace, namespace
  # declarations inherited from suite wrappers, entity-expanded Unicode and @dtd.
  result=[]
  for n in wrapper.childNodes:
   copy=n.cloneNode(True)
   if n.nodeType==n.ELEMENT_NODE:
    scope={};anc=n.parentNode
    while anc and anc.nodeType==anc.ELEMENT_NODE:
     for i in range(anc.attributes.length):
      a=anc.attributes.item(i)
      if a.name=='xmlns' or a.name.startswith('xmlns:'):scope.setdefault(a.name,a.value)
     anc=anc.parentNode
    for k,v in scope.items():
     if not copy.hasAttribute(k):copy.setAttribute(k,v)
   result.append(copy.toxml())
  return (wrapper.getAttribute('dtd')+''.join(result)).encode('utf-8')
 def save(wrapper,path):
  path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(content(wrapper))
  return {'path':str(path.relative_to(CORPUS)),'sha256':digest(path),'bytes':path.stat().st_size}
 def imports(node,directory):
  for child in elements(node):
   if child.tagName=='resource':resources.append(save(child,directory/child.getAttribute('name')))
   elif child.tagName=='dir':imports(child,directory/child.getAttribute('name'))
 for i,case in enumerate(doc.getElementsByTagName('testCase'),1):
  ident=f'jing-{i:03}';directory=CORPUS/'jing'/f'{i:03}'
  schema=next(n for n in elements(case) if n.tagName in ('correct','incorrect'))
  row={'id':ident,'sourceOrdinal':i,'schemaExpected':'accepted' if schema.tagName=='correct' else 'rejected','schema':save(schema,directory/('c.rng' if schema.tagName=='correct' else 'i.rng')),'instances':[],'classification':'applicable','reason':'OASIS 3 December 2001 XML syntax; original upstream correct/incorrect and valid/invalid predicates.'}
  section=case;sections=[]
  while section and section.nodeType==section.ELEMENT_NODE:
   for n in elements(section):
    if n.tagName=='section':sections.append(''.join(c.data for c in n.childNodes if c.nodeType==c.TEXT_NODE))
   section=section.parentNode
  row['sections']=sections
  imports(case,directory)
  for j,n in enumerate([n for n in elements(case) if n.tagName in ('valid','invalid')],1):
   row['instances'].append({'id':f'{ident}-instance-{j}','expected':'valid' if n.tagName=='valid' else 'invalid',**save(n,directory/f'{j}.{ "v" if n.tagName=="valid" else "i"}.xml')})
  cases.append(row)
 inventory={'suite':'Jing XML syntax specification suite','upstream':'https://github.com/relaxng/jing-trang','revision':REV,'license':'BSD-3-Clause','normativeProfile':'https://relaxng.org/spec-20011203.html','suiteDocumentation':'upstream/jing/index.html identifies final December specification; legacy embedded October documentation is retained verbatim.','missingCases':0,'missingResources':0,'cases':cases,'resources':resources,'sources':[{'path':str(p.relative_to(CORPUS)),'sha256':digest(p),'bytes':p.stat().st_size} for p in sorted(target.iterdir())]}
 (CORPUS/'jing-inventory.json').write_text(json.dumps(inventory,indent=2)+'\n')
 print(len(cases),'schemas',sum(len(c['instances']) for c in cases),'instances',len(resources),'resources')
if __name__=='__main__':split(Path(sys.argv[1]))
