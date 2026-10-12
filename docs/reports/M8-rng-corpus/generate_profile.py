"""Authored additional witnesses with independently specified datatype/finalization oracles.
No product execution or generated product validity is used here.
"""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];CORPUS=ROOT/'tests/corpora/relaxng';NS='http://relaxng.org/ns/structure/1.0';XSD='http://www.w3.org/2001/XMLSchema-datatypes'
def generate():
 cases=[]
 def file(path,text):
  out=CORPUS/'profile'/path;out.parent.mkdir(parents=True,exist_ok=True);out.write_text(text,encoding='utf-8')
  data=out.read_bytes();return {'path':str(out.relative_to(CORPUS)),'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
 def case(id,pattern,examples,accepted=True,unused=False):
  body=f'<element xmlns="{NS}" name="r" datatypeLibrary="{XSD}">{pattern}</element>'
  if unused:body=f'<grammar xmlns="{NS}" datatypeLibrary="{XSD}"><start><element name="r"><empty/></element></start><define name="unused">{pattern}</define></grammar>'
  row={'id':'profile-'+id,'schemaExpected':'accepted' if accepted else 'rejected','schema':file(id+'/schema.rng',body),'instances':[],'classification':'authored-required-profile','reason':'RNG 2001 6.2.8 and its W3C XML Schema Datatypes library: independent lexical/value/facet witnesses. Incorrect definitions must fail public facade finalization even if unreachable.','primarySpec':'https://relaxng.org/spec-20011203.html#data-pattern'}
  for i,(xml,valid) in enumerate(examples,1):row['instances'].append({'id':row['id']+'-instance-'+str(i),'expected':'valid' if valid else 'invalid',**file(id+'/'+str(i)+'.xml',xml)})
  cases.append(row)
 for id,type,valid,invalid in [('int','int','42','2147483648'),('decimal','decimal','+12.500','1e2'),('boolean','boolean','1','yes'),('date','date','2024-02-29Z','2023-02-29'),('duration','duration','P1Y2M','1Y'),('base64','base64Binary','YQ==','YQ='),('hex','hexBinary','aB01','z0'),('language','language','en-US','en_Us'),('nmtokens','NMTOKENS','a b:c',''),('positive','positiveInteger','1','0')]:
  case(id,f'<data type="{type}"/>',[(f'<r>{valid}</r>',True),(f'<r>{invalid}</r>',False)])
 case('nan-value','<value type="double">NaN</value>',[('<r>NaN</r>',True),('<r>INF</r>',False)])
 case('conjunctive-patterns','<data type="string"><param name="pattern">a.*</param><param name="pattern">.*b</param><param name="minLength">2</param></data>',[('<r>ab</r>',True),('<r>aa</r>',False),('<r>bb</r>',False),('<r>a</r>',False)])
 case('integer-bounds','<data type="integer"><param name="minInclusive">10000000000000000000000000000000000000</param><param name="maxExclusive">10000000000000000000000000000000000002</param></data>',[('<r>10000000000000000000000000000000000001</r>',True),('<r>10000000000000000000000000000000000002</r>',False)])
 for id,pattern,unused in [('invalid-regex','<data type="string"><param name="pattern">[</param></data>',False),('invalid-facet','<data type="int"><param name="length">1</param></data>',False),('invalid-bounds','<data type="int"><param name="minInclusive">10</param><param name="maxInclusive">2</param></data>',False),('unused-invalid-value','<value type="int">bad</value>',True)]:case(id,pattern,[],False,unused)
 inventory={'suite':'Additional independent required-profile witnesses','missingCases':0,'missingResources':0,'cases':cases,'resources':[],'sources':[]}
 (CORPUS/'profile-inventory.json').write_text(json.dumps(inventory,indent=2)+'\n')
 print(len(cases),'authored schemas',sum(len(c['instances']) for c in cases),'instances')
if __name__=='__main__':generate()
