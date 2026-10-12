import json,hashlib,xml.etree.ElementTree as E
from pathlib import Path
out=Path('tests/corpora/schematron');ns='http://purl.oclc.org/dsdl/schematron';sv='http://purl.oclc.org/dsdl/svrl';E.register_namespace('svrl',sv)
def loc(n,i=1,u=''):return f"/*[local-name()='{n}' and namespace-uri()='{u}'][{i}]"
def pat(key=0,id=None,name=None):return dict(key=key,id=id,name=name)
def fire(context,location,key=0,p=0,r=0):return dict(key=key,patternKey=p,ruleKey=r,context=context,location=location)
def finding(test,text,context='item',location=None,kind='SuccessfulReport',id=None,role=None,flag=None,p=0,k=0,pid=None,subject=None,diagnostics=[],properties=[]):
 l=location if location is not None else loc('item');return dict(kind=kind,id=id,role=role,flag=flag,patternKey=p,firedRuleKey=k,patternId=pid,context=context,test=test,location=subject if subject is not None else l,contextLocation=l,text=text,diagnostics=diagnostics,properties=properties)
cases=[]
def add(id,body,document='<item/>',phase=None,title=None,namespaces={},patterns=None,fired=None,findings=None,resources={},schemaAttrs='',complete=True):
 patterns=patterns if patterns is not None else [pat()];fired=fired if fired is not None else [fire('item',loc('item'))];findings=findings or []
 attrs=' '+schemaAttrs if schemaAttrs else '';schema=f'<schema xmlns="{ns}"{attrs}>'+body+'</schema>'
 expected=dict(phase=phase or '#ALL',title=title,namespaces=namespaces,activePatterns=patterns,firedRules=fired,findings=findings,isValid=complete and not any(f['kind']=='FailedAssert' for f in findings),isComplete=complete)
 root=E.Element('{'+sv+'}schematron-output',{'phase':expected['phase']});
 if title is not None:root.set('title',title)
 for prefix,uri in sorted(namespaces.items()):E.SubElement(root,'{'+sv+'}ns-prefix-in-attribute-values',prefix=prefix,uri=uri)
 for p in patterns:
  a={k:p[k] for k in ['id','name'] if p[k] is not None};E.SubElement(root,'{'+sv+'}active-pattern',a)
  for r in fired:
   if r['patternKey']!=p['key']:continue
   E.SubElement(root,'{'+sv+'}fired-rule',context=r['context'])
   for f in findings:
    if f['firedRuleKey']!=r['key']:continue
    attrs={k:f[k] for k in ['test','location','id','role','flag'] if f[k] is not None};node=E.SubElement(root,'{'+sv+'}'+('failed-assert' if f['kind']=='FailedAssert' else 'successful-report'),attrs);E.SubElement(node,'{'+sv+'}text').text=f['text']
    for field,tag,attr in [('diagnostics','diagnostic-reference','diagnostic'),('properties','property-reference','property')]:
     for ref in f[field]:E.SubElement(node,'{'+sv+'}'+tag,{attr:ref['id']}).text=ref['text']
 cases.append(dict(id=id,schema=schema,document=document,phase=phase,resources=resources,expected=expected,expectedSvrl=E.tostring(root,encoding='unicode')))
add('assert-report-metadata-escaping','<title>A &amp; B</title><pattern id="p"><title>P</title><rule context="item"><assert test="@ok" id="a" role="error" flag="">need &lt;ok&gt;</assert><report test="true()" id="r">A &amp; B ]]&gt; 😀</report><assert test="true()">hidden</assert><report test="false()">hidden</report></rule></pattern>',title='A & B',patterns=[pat(id='p',name='P')],findings=[finding('@ok','need <ok>',kind='FailedAssert',id='a',role='error',flag='',pid='p'),finding('true()','A & B ]]> 😀',id='r',pid='p')])
add('first-rule-singleton-focus','<pattern><rule context="item"><report test="position()=1 and last()=1"><value-of select="@n"/></report></rule><rule context="*"><assert test="false()">wrong</assert></rule></pattern>','<root><item n="1"/><item n="2"/></root>',fired=[fire('item',loc('root')+loc('item'),0),fire('item',loc('root')+loc('item',2),1)],findings=[finding('position()=1 and last()=1','1',location=loc('root')+loc('item'),k=0),finding('position()=1 and last()=1','2',location=loc('root')+loc('item',2),k=1)])
# Root catch-all would match root before item; restrict fallback to item too.
cases[-1]['schema']=cases[-1]['schema'].replace('context="*"','context="item"')
add('namespaces-subject-messages','<ns prefix="z" uri="urn:z"/><ns prefix="a" uri="urn:a"/><pattern><rule context="z:item" subject="@code"><report test="true()" subject=".." diagnostics="d" properties="p"><emph>hello </emph><name/>:<value-of select="@code"/><dir value="ltr">!</dir><span class="c"></span></report></rule></pattern><diagnostics><diagnostic id="d">D:<value-of select="name(@code)"/></diagnostic></diagnostics><properties><property id="p">P:<value-of select="@code"/></property></properties>','<root><z:item xmlns:z="urn:z" code="42"/></root>',namespaces={'z':'urn:z','a':'urn:a'},fired=[fire('z:item',loc('root')+loc('item',u='urn:z'))],findings=[finding('true()','hello z:item:42!',context='z:item',location=loc('root')+loc('item',u='urn:z'),subject=loc('root'),diagnostics=[dict(id='d',text='D:code')],properties=[dict(id='p',text='P:42')])])
add('rule-subject-empty-check-override','<pattern><rule context="item" subject="@code"><report test="true()">attribute</report><report test="true()" subject="missing">empty</report></rule></pattern>','<item code="x"/>',findings=[finding('true()','attribute',subject=loc('item')+"/@*[local-name()='code' and namespace-uri()='']"),finding('true()','empty',subject='')])
add('document-attribute-context','<pattern><rule context="/"><report test="true()">root:<name/></report></rule><rule context="@code"><report test="true()"><name/>:<value-of select="."/></report></rule></pattern>','<item code="x"/>',fired=[fire('/','/'),fire('@code',loc('item')+"/@*[local-name()='code' and namespace-uri()='']",1,r=1)],findings=[finding('true()','root:',context='/',location='/'),finding('true()','code:x',context='@code',location=loc('item')+"/@*[local-name()='code' and namespace-uri()='']",k=1)])
add('let-dependencies-shadowing','<let name="x" value="1"/><pattern><let name="y" value="$x+1"/><let name="x" value="$x+1"/><rule context="item"><let name="x" value="$x+1"/><report test="$x=3 and $y=3"><value-of select="$x"/>:<value-of select="$y"/></report></rule></pattern>',findings=[finding('$x=3 and $y=3','3:3')])
add('abstract-parameters-token-boundaries','<pattern abstract="true" id="base"><rule context="$context"><report test="$v=2 and \'$v\'=\'$v\'"><value-of select="$v"/></report></rule></pattern><pattern is-a="base" id="p"><param name="context" value="item"/><param name="v" value="2"/></pattern>',patterns=[pat(id='p')],findings=[finding("2=2 and '$v'='$v'",'2',pid='p')])
add('abstract-rule-recursive-extends','<pattern><rule abstract="true" id="a"><report test="true()">base</report></rule><rule abstract="true" id="b"><extends rule="a"/></rule><rule context="item"><extends rule="b"/><report test="true()">tail</report></rule></pattern>',findings=[finding('true()','base'),finding('true()','tail')])
add('include-nested-base','<include href="sub/pattern.sch"/>',resources={'sub/pattern.sch':f'<pattern xmlns="{ns}"><include href="rule.sch"/></pattern>','sub/rule.sch':f'<rule xmlns="{ns}" context="item"><report test="true()">included</report></rule>'},findings=[finding('true()','included')])
add('foreign-annotations','<note xmlns="urn:notes"><text>ignored</text></note><pattern><rule context="item"><report test="true()">kept</report></rule></pattern>',findings=[finding('true()','kept')])
phasebody='<phase id="chosen"><let name="v" value="7"/><active pattern="p2"/><active pattern="p1"/></phase><pattern id="p1"><rule context="item"><report test="$v=7">one</report></rule></pattern><pattern id="p2"><rule context="item"><report test="$v=7">two</report></rule></pattern>'
for id,selection in [('phase-named','chosen'),('phase-default',None),('phase-default-marker','#DEFAULT')]:
 add(id,phasebody,phase='chosen',schemaAttrs='defaultPhase="chosen"',patterns=[pat(0,'p1'),pat(1,'p2')],fired=[fire('item',loc('item')),fire('item',loc('item'),1,1,1)],findings=[finding('$v=7','one',pid='p1'),finding('$v=7','two',p=1,k=1,pid='p2')]);cases[-1]['phase']=selection
add('phase-all-missing-variable',phasebody,complete=False,patterns=[pat(0,'p1')],fired=[fire('item',loc('item'))]);cases[-1]['phase']='#ALL';cases[-1]['expected']['fatalCode']='SCH.XPath'
add('union-predicate-context','<pattern><rule context="item[1]|other"><report test="true()"><name/></report></rule></pattern>','<root><item/><item/><other/></root>',fired=[fire('item[1]|other',loc('root')+loc('item')),fire('item[1]|other',loc('root')+loc('other'),1)],findings=[finding('true()','item',context='item[1]|other',location=loc('root')+loc('item')),finding('true()','other',context='item[1]|other',location=loc('root')+loc('other'),k=1)])
add('duplicate-context-keyed-patterns','<pattern id="p"><rule context="item"><report test="true()">one</report></rule></pattern><pattern><rule context="item"><report test="true()">two</report></rule></pattern>',patterns=[pat(0,'p'),pat(1)],fired=[fire('item',loc('item')),fire('item',loc('item'),1,1,1)],findings=[finding('true()','one',pid='p'),finding('true()','two',p=1,k=1)])
add('zero-findings-trace','<pattern><rule context="item"><assert test="true()">hidden</assert></rule></pattern>')
add('xslt-explicit-binding','<pattern><rule context="item"><report test="true()">yes</report></rule></pattern>',schemaAttrs='queryBinding="xslt"',findings=[finding('true()','yes')])
add('no-implicit-xpath-namespace','<pattern><rule context="item"><assert test="false()">hidden</assert></rule></pattern>','<item xmlns="urn:item"/>',fired=[])
(out/'profile.json').write_text(json.dumps(dict(schemaVersion=1,source='Authored from frozen ISO2016/direct-M4/SVRL contracts; expected rows hand-specified before execution',cases=cases),indent=2,ensure_ascii=False)+'\n')
print(len(cases))
