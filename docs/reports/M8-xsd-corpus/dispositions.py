#!/usr/bin/env python3
"""Report original oracle, adjudicated profile, and exact exception separately."""
import argparse, collections, importlib.util, json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
_spec=importlib.util.spec_from_file_location('approved_dispositions',Path(__file__).with_name('approved_dispositions.py'))
_approved=importlib.util.module_from_spec(_spec);_spec.loader.exec_module(_approved)
approved_sources=_approved.approved_sources
TIME='msMeta/DataTypes_w3c.xml#g119/s:time_maxInclusive007_119'
OPTIONAL_A004='msMeta/DataTypes_w3c.xml#g1339/i0:anyURI_a004_1339.i'
ERR='msMeta/Errata10_w3c.xml#g18/s:errF001'
DEP='msMeta/Errata10_w3c.xml#g18/i0:errF001.i'
XMLFILE=ROOT/'tests/corpora/xsd/xmlversions-adjudication.json'
XMLCASES={r['id']:r for r in json.loads(XMLFILE.read_text())['cases']} if XMLFILE.exists() else {}
EXPLICITFILE=ROOT/'tests/corpora/xsd/explicit-profile-adjudication.json'
EXPLICITCASES={r['id']:r for r in json.loads(EXPLICITFILE.read_text())['cases']} if EXPLICITFILE.exists() else {}
EXPLICITDEPS={r['id']:r for c in EXPLICITCASES.values() for r in c['dependent_instances']}
INSTANCEPROFILES={r['id']:r for name in ['supplementary-category','lexical-oracle'] if (ROOT/f'tests/corpora/xsd/{name}-adjudication.json').exists() for r in json.loads((ROOT/f'tests/corpora/xsd/{name}-adjudication.json').read_text())['cases']}
SUBSTITUTIONFILE=ROOT/'tests/corpora/xsd/substitution-oracle-adjudication.json'
SUBSTITUTION=json.loads(SUBSTITUTIONFILE.read_text())['case'] if SUBSTITUTIONFILE.exists() else {}
XMLSUPPLYFILE=ROOT/'tests/corpora/xsd/xml-namespace-profile.json'
XMLSUPPLY=json.loads(XMLSUPPLYFILE.read_text())['cases'] if XMLSUPPLYFILE.exists() else []
XMLSUPPLYORIGINALS={id for c in XMLSUPPLY for id in [c['group']+'/s:'+c['schema'],c['group']+'/i0:'+c['instance']]}
ADAPTERFILE=ROOT/'tests/corpora/xsd/adapter-boundaries.json'
ADAPTER=json.loads(ADAPTERFILE.read_text()) if ADAPTERFILE.exists() else {}
LATEHINTS={c['id'] for c in ADAPTER.get('late_hints',[])}
FATALXML={c['id'] for c in ADAPTER.get('fatal_xml',[])}
NAMECHAR=ADAPTER.get('namechar_case',{})
def view(row):
 result={'originalPassed':row.get('passed'),'profilePassed':row.get('passed'),'approvedException':False,'notExecutable':False,'substrate':False,'invalidSchemaDependency':False,'applicationInputUnassessed':False,'adapterUnassessed':False,'fatalXmlUnassessed':False}
 if row['id']==TIME:result['approvedException']=row['actual']=='schema-valid' and row.get('passed') is False and row.get('engine') in (None,'product') and row.get('expected','invalid')=='invalid'
 elif row['id']==ERR:result['profilePassed']=row['actual']=='schema-invalid'
 elif row['id']==DEP:
  result['notExecutable']=row['actual']=='schema-unavailable';result['profilePassed']=None if result['notExecutable'] else False
 if row['id'] in XMLCASES:
  case=XMLCASES[row['id']]
  if case['disposition']=='assess':result['profilePassed']=row['actual']==('schema-' if case['kind']=='schema' else 'instance-')+case['derivedExpected']
  else:
   result['profilePassed']=None;result['substrate']=case['disposition']=='outside-XML-1.0-substrate';result['invalidSchemaDependency']=case['disposition']=='invalid-schema-dependency';result['notExecutable']=row['actual']=='schema-unavailable'
 if row['id'] in EXPLICITCASES:result['profilePassed']=row['actual']=='schema-'+EXPLICITCASES[row['id']]['profile_expected']
 if row['id'] in EXPLICITDEPS:
  result['profilePassed']=None;result['invalidSchemaDependency']=True;result['notExecutable']=row['actual']=='schema-unavailable'
 if row['id'] in INSTANCEPROFILES:result['profilePassed']=row['actual']=='instance-'+INSTANCEPROFILES[row['id']]['profile_expected']
 if row['id']==SUBSTITUTION.get('id'):result['profilePassed']=row['actual']=='schema-'+SUBSTITUTION['derived_expected']
 if row['id']==NAMECHAR.get('id'):result['profilePassed']=row['actual']=='instance-'+NAMECHAR['profile_expected']
 if row['id'] in XMLSUPPLYORIGINALS:
  result['profilePassed']=None;result['applicationInputUnassessed']=True;result['notExecutable']=row['actual']=='schema-unavailable'
 if row['id'] in LATEHINTS and row.get('engine')=='product' and row.get('hintOrderingProfile') is True:
  result['profilePassed']=row['actual']=='instance-invalid' and 'XSD.HintOrder' in row.get('codes',[])
 elif row['id'] in LATEHINTS or row['id']==ADAPTER.get('namespace_guard',{}).get('id'):
  result['profilePassed']=None;result['adapterUnassessed']=True
 if row['id'] in FATALXML:
  fatal=row['actual'] in ['incomplete-refusal','instance-syntax-error']
  result['profilePassed']=None if fatal else False;result['fatalXmlUnassessed']=fatal
 if row['id']==OPTIONAL_A004 and row.get('engine')=='product':
  result['profilePassed']=row.get('expected')=='invalid' and row['actual']=='instance-valid' and row.get('codes')==[]
  result['profileAuthority']='verified optional composition; W3C4126 derived VALID, original INVALID unchanged'
 result['approvedGateDisposition']=_approved.gate_disposition(row)
 return result

def main():
 p=argparse.ArgumentParser();p.add_argument('revision');p.add_argument('--engine',default='product',choices=['product','bcl']);a=p.parse_args()
 root=ROOT/'artifacts/M8-xsd-corpus'/a.revision;rows=[r for f in sorted(root.glob(a.engine+'-*.jsonl')) for r in map(json.loads,f.read_text().splitlines())]
 counts=collections.Counter();uncovered=[];inventory=json.loads((ROOT/'tests/corpora/xsd/inventory.json').read_text());conditional={r['id'] for g in inventory['groups'] for r in g['instances'] if r['adjudication'] and not r['adjudication']['supplemental']}
 for row in rows:
  if row['classification']!='applicable':counts[row['classification']]+=1;continue
  if row['id'].endswith('/unicode16-supplemental'):counts['supplemental']+=1
  elif row['id'] in conditional:counts['originalConditionalNoSingleOracle']+=1
  else:counts['originalPassed' if row['passed'] is True else 'originalFailed' if row['passed'] is False else 'originalIndeterminateObserved']+=1
  d=view(row)
  counts['profilePassed' if d['profilePassed'] is True else 'profileFailed' if d['profilePassed'] is False else 'profileApplicationInputUnassessed' if d['applicationInputUnassessed'] else 'profileAdapterUnassessed' if d['adapterUnassessed'] else 'profileFatalXmlUnassessed' if d['fatalXmlUnassessed'] else 'profileSubstrateUnassessed' if d['substrate'] else 'profileInvalidSchemaDependency' if d['invalidSchemaDependency'] else 'profileNotExecutable' if d['notExecutable'] else 'profileIndeterminateObserved']+=1
  if d['approvedException']:counts['approvedExceptionOriginalFailure']+=1
  if d['approvedGateDisposition']:counts['approvedGateDisposition.'+d['approvedGateDisposition']]+=1
  elif row['passed'] is not True:
   uncovered.append({'id':row['id'],'expected':row['expected'],'actual':row['actual'],'codes':row.get('codes',[]),'originalPassed':row['passed'],'profile':d})
 for name in [TIME,ERR,DEP]:
  matches=[r for r in rows if r['id']==name]
  if matches:print(name,'original=',matches[0]['expected'],'actual=',matches[0]['actual'],view(matches[0]))
 print(json.dumps({'counts':dict(sorted(counts.items())), 'originalNonpassesWithoutExactGateDisposition':uncovered, 'gateStatus':'pending root review; no automatic conformance pass'},sort_keys=True))
if __name__=='__main__':main()
