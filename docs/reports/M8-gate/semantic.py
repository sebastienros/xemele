"""Finite semantic checks and concise output inspection; never builds or restores."""
import sys
sys.dont_write_bytecode=True
import asyncio,hashlib,importlib.util,json,os,subprocess,time,xml.etree.ElementTree as ET
from pathlib import Path
from recipes import ROOT,HERE,NET,PACKAGE_WORK as WORK,dll

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def digest(v):return hashlib.sha256(json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=True).encode()).hexdigest()
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def bounded_run(argv,maximum):
 # All descendants inherit the already registered semantic command group. No separate session.
 end=min(float(os.environ['XEMELE_GATE_WORK_DEADLINE']),time.monotonic()+maximum)
 result=subprocess.run(argv,cwd=ROOT,env=os.environ.copy(),capture_output=True,timeout=max(.001,end-time.monotonic()))
 if result.returncode: raise RuntimeError(str(argv)+': '+result.stderr.decode(errors='replace')[-1600:]+' '+result.stdout.decode(errors='replace')[-1600:])
 return result

def api():
 b=module('gate_api_bindings',ROOT/'docs/reports/M8-xsd-resolved-availability/harness/bindings.py')
 out=Path(os.environ['XEMELE_GATE_RUN_DIR'])/'api';out.mkdir()
 union=out/'public.txt'; b.write_api_union(union)
 assert len(list((ROOT/'docs/api').glob('*.txt')))==55
 product=ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll'
 def call(binary,mode,path):return bounded_run(dll(binary,mode,product,path),60)
 call('docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare',union)
 internal=out/'internal.txt';call('docs/reports/M8-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll','emit',internal);actual=b.rows(internal)
 lanes={}
 for lane in ['Model','Regex','Datatypes','Facets','RelaxNg','Xsd','Schematron','Reporting']:
  expected=b.rows(ROOT/f'tests/Contracts/Fixtures/M8/{lane}.golden.txt');b.exact_projection(actual,expected);lanes[lane]=len(expected)
 for path,count in [('tests/Contracts/Fixtures/M8/Xsd.golden.txt',615),('docs/reports/M8-xsd-compile/compiler.golden.txt',257)]:
  expected=b.rows(ROOT/path);assert len(expected)==count;b.exact_projection(actual,expected)
 proof=ROOT/'docs/reports/M8-xsd-resolved-availability/verified-runtime-api.json'
 assert sha(proof)=='5016c3b603fc99470bfdb2f53cbd500b972a4ce8c68d5469372f62b3fd46b187'
 provenance=json.loads(proof.read_text());golden=ROOT/'docs/reports/M8-xsd-resolved-availability/verified-added.golden.txt'
 assert sha(golden)=='33a4823adf25f638d0e52990918f859c2b64a35359d25aa59c318f4543a1812d'
 added=out/'added.txt';call('docs/reports/M8-xsd-resolved-availability/harness/bin/Release/net10.0/Missing.Api.dll','emit',added)
 rows=b.rows(added);assert len(rows)==93 and rows==b.rows(golden)==provenance['canonical_added_projection']
 call('docs/reports/M8-xsd-resolved-availability/harness/bin/Release/net10.0/Missing.Api.dll','compare',golden)
 return {'publicRows':1430,'fragments':55,'lanes':lanes,'xsdRows':615,'compilerRows':257,'nullableMigrationRows':3,
         'verifiedProvenanceSha256':sha(proof),'addedRows':93,'canonicalAddedSha256':sha(golden),'productSha256':sha(product)}

def grammar():
 sys.path.insert(0,str(ROOT/'docs/reports/M2-conformance-grammar'))
 import full_grammar as g
 nested=module('gate_grammar_nested',HERE/'nested_ownership.py')
 supervisor=module('gate_grammar_supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
 original=g.Child
 class PinnedChild(original):
  @classmethod
  async def start(cls,argv):
   assert argv[0]==NET and str(argv[1]).endswith('.dll')
   return await super().start([NET,'--fx-version','10.0.11',*map(str,argv[1:])])
 g.Child=PinnedChild
 baseline=json.loads((ROOT/'docs/reports/M7-gate/summary.json').read_text())['grammar']
 outcome={'result':'failed'}
 end=min(float(os.environ['XEMELE_GATE_WORK_DEADLINE'])-20,time.monotonic()+200)
 # Accepted Child bodies remain unchanged. Python's global Popen wrapper registers
 # both asyncio new sessions in EVERY inherited ancestor before their real exec.
 with nested.interruptions(),nested.track_launches(supervisor,os.environ.copy(),end):
  asyncio.run(asyncio.wait_for(g.execute(outcome),max(.001,end-time.monotonic())))
 assert outcome['result']=='passed' and outcome['counts']==baseline['counts']
 for name in ['caseModelHashes','originalOutputWitnesses','referenceResolutions']:
  assert digest(outcome[name])==baseline[name+'Sha256'],name
 return {'result':'passed','counts':outcome['counts'],**{name+'Sha256':digest(outcome[name]) for name in ['caseModelHashes','originalOutputWitnesses','referenceResolutions']},
         'provenanceSha256':digest(outcome['provenance']),'childExitAndCleanup':outcome['childExitAndCleanup']}

def external(lane):
 assert lane in ['managed','trim','aot']
 command=dll(WORK/'consumer/bin/Release/net10.0/Consumer.dll',ROOT/'tests/corpora') if lane=='managed' else [str(WORK/lane/'Consumer'),str(ROOT/'tests/corpora')]
 result=bounded_run(command,45)
 expected=(ROOT/'tests/corpora/xinclude/expected.txt').read_bytes()+b'\n'+(HERE/'consumer-expected.txt').read_bytes()
 assert result.stdout==expected, {'lane':lane,'stdoutSha256':hashlib.sha256(result.stdout).hexdigest(),'expectedSha256':hashlib.sha256(expected).hexdigest(),'tail':result.stdout[-600:].decode(errors='replace')}
 assert result.stderr.strip()==b'Framework=.NET 10.0.11; Architecture=Arm64',result.stderr.decode(errors='replace')
 return {'lane':lane,'stdoutSha256':hashlib.sha256(result.stdout).hexdigest(),'independentExpectedOutputMatches':True,'framework':'.NET 10.0.11','architecture':'Arm64'}

def inspect_command(argv,text,raw):
 """Called ONLY after durable lifecycle/whole-family terminal receipts."""
 baseline=json.loads((ROOT/'docs/reports/M7-gate/summary.json').read_text())
 if any('CounterControl.dll' in arg for arg in argv):
  value=json.loads(text);positive='--positive' in argv
  assert value['countersEnabled'] is positive and value['runtime']=='.NET 10.0.11' and value['architecture']=='Arm64'
  assert value['allocatedBytes']>=4096 if positive else value['allocatedBytes']==0
  assert value['escapedLength']==4096 and value['productSha256']==sha(ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll')
  return value
 for path,key in [('docs/reports/M4-corpus/bin/Release/net10.0/Fuzz.Driver.dll','xpathFuzz'),('docs/reports/M7-corpus/bin/Release/net10.0/Fuzz.Driver.dll','htmlFuzz')]:
  if path in argv:
   value=json.loads(text);assert value==baseline[key],(key,value);return value
 if any(arg.endswith('/XmlToolkit.dll') for arg in argv):
  assert text.encode()==(ROOT/'tests/corpora/xinclude/expected.txt').read_bytes()+b'\n';return {'independentXmlToolkitOutput':True}
 if '-result-xml' in argv:
  return inspect_suite(Path(argv[argv.index('-result-xml')+1]),baseline)
 if 'docs/reports/M8-gate/semantic.py' in argv:
  value=json.loads(text.split('GATE_SEMANTIC ')[-1]);assert value['passed'];return value
 return {'tail':text.splitlines()[-8:]}

def inspect_suite(path,baseline):
 tree=ET.parse(path);assemblies=list(tree.getroot().iter('assembly'));tests=list(tree.getroot().iter('test'))
 counts={k:sum(int(a.get(k,'0')) for a in assemblies) for k in ['total','passed','failed','skipped','errors','not-run']}
 bad=[{'id':t.get('id'),'name':t.get('name'),'result':t.get('result'),'details':ET.tostring(t,encoding='unicode')[:2400]} for t in tests if t.get('result')!='Pass']
 group=path.stem;old=next(r for r in baseline['cumulative']['groups'] if r['group']==group)
 identities=[t.get('id') for t in tests];names=[t.get('name') for t in tests]
 assert all(identities) and len(set(identities))==len(tests), 'Missing/duplicate runtime identities'
 assert all(names) and len(set(names))==len(tests), 'Missing/duplicate display identities'
 inherited_types=json.loads((HERE/'m7-types.json').read_text())[group]
 earlier=sorted(t.get('name') for t in tests if t.get('type') in inherited_types)
 transition=None
 if group=='Xemele.Tests':
  actual_witness='Xemele.Tests.ResourceKinds.ResourceKindTests.UndefinedKindsFailBeforeResolver(value: 6)'
  historical_witness=actual_witness.replace('(value: 6)','(value: 5)')
  witness=[name for name in earlier if name==actual_witness]
  assert historical_witness not in names, 'Historical undefined5 identity must be absent globally'
  assert len(witness)==1,'Exact6 inherited witness must exist once'
  transition={'observed':witness[0],'baseline':witness[0].replace('(value: 6)','(value: 5)'), 'reason':'Schema=5 added; Undefined=6','passed':next(t.get('result') for t in tests if t.get('name')==witness[0])=='Pass'}
  earlier=sorted(transition['baseline'] if name==transition['observed'] else name for name in earlier)
 inherited_sha=hashlib.sha256('\n'.join(earlier).encode()).hexdigest()
 result={'group':group,**counts,'uniqueIds':len(identities),'identityIdsSha256':digest(sorted(identities)),
         'displayNamesSha256':hashlib.sha256('\n'.join(sorted(names)).encode()).hexdigest(),'inheritedCount':len(earlier),
         'inheritedSha256':inherited_sha,'resourceKindTransition':transition,'nonpasses':bad,
         'xmlSha256':sha(path),'warmBorrowedCases':sum('WarmBorrowed' in name for name in names),
         'newTypes':sorted(set(t.get('type') for t in tests)-set(inherited_types))}
 # Persist diagnostics before an assertion can reject the suite. Parent includes this file before deletion.
 (path.parent/(group+'-inspection.json')).write_text(json.dumps(result,indent=2)+'\n')
 assert assemblies and all('.NET 10.0.11' in a.get('environment','') for a in assemblies)
 assert counts['total']==counts['passed']==len(tests) and counts['total']>0 and not bad and all(counts[k]==0 for k in ['failed','errors','skipped','not-run']),result
 assert len(earlier)==old['total'] and inherited_sha==old['identitiesSha256'],result
 if transition:
  assert transition['passed']
  schema=[t for t in tests if t.get('type')=='Xemele.Tests.SchemaResources.SchemaResourceKindTests']
  methods={'ExactEnumValuesAndActualRuntime':1,'SchemaRequestForwardingAndExactlyOnceOwnership':2,'SchemaRefusalRequiresExplicitResolverSuccess':4,'CallerCanAuthorizeSchemaWithoutAuthorizingEntitiesOrIncludes':2,'CatalogUsesSchemaUriAndPreservesPurposeAndLexicalIds':2,'DefaultAsyncResolverFallbackPreservesSchemaRequest':1}
  assert len(schema)==12 and all(t.get('result')=='Pass' for t in schema)
  assert {method:sum(t.get('method')==method for t in schema) for method in methods}==methods
  result['schemaResourcePositiveCases']=12
  result['schemaResourcePositiveIdentitySha256']=digest(sorted(t.get('id') for t in schema))
 return result

def inspect_allocation_delta(before):
 from identities import sha as file_sha
 rows=[]; roots=set();now={str(p.relative_to(ROOT)):file_sha(p) for p in (ROOT/'artifacts').rglob('*') if p.is_file() and 'M8-gate' not in p.parts}
 assert all(now.get(name)==value for name,value in before.items()),'Pre-existing artifact mutation refused'
 delta=set(now)-set(before)
 for name in sorted(n for n in delta if n.endswith('.request.json')):
  path=ROOT/name;request=json.loads(path.read_text());report=json.loads(Path(request['ReportPath']).read_text())
  assert request['Version']==report['Version']==1 and request['Mode']==report['Mode'] and request['Token']==report['Token'] and request['Expected']==report['Actual']
  actual=report['Actual']
  for p,h in [('Executable','ExecutableSha256'),('TestAssembly','TestSha256'),('ProductAssembly','ProductSha256'),('CoreLibrary','CoreLibrarySha256')]:assert file_sha(Path(actual[p]))==actual[h].lower()
  if 'NativeGcLibrary' in actual:assert file_sha(Path(actual['NativeGcLibrary']))==actual['NativeGcSha256'].lower()
  assert file_sha(Path(actual['CoreLibrary']).parent/'libcoreclr.dylib')==actual['NativeRuntimeSha256'].lower()
  assert file_sha(Path(actual['TestAssembly']).with_suffix('.runtimeconfig.json'))==actual['RuntimeConfigSha256'].lower()
  assert actual['RuntimeVersion']=='10.0.11'
  if 'RuntimeOverrides' in actual:
   overrides=json.loads(actual['RuntimeOverrides']);assert all(overrides[k]=='0' for k in ['DOTNET_TieredCompilation','DOTNET_TieredPGO','DOTNET_ReadyToRun'])
  measurements=report.get('Labels',[{k:v for k,v in report.items() if k not in ['Actual','Token','ProcessId']}])
  for value in measurements:
   assert (value['AllocatedBytes']==value['EscapedObjectLength']==0) if request['Mode']=='real' else (value['AllocatedBytes']>0 and value['EscapedObjectLength']>0)
  child_xml=Path(request['XmlPath']);assemblies=list(ET.parse(child_xml).getroot().iter('assembly'))
  assert assemblies and all(int(a.get('passed','0'))>0 and all(int(a.get(k,'0'))==0 for k in ['failed','errors','skipped','not-run']) for a in assemblies)
  run_root=path.parent;assert run_root.parent.name=='child-runs' and run_root.parent.parent.name in ['M2-streaming-allocation-probe','M2-tree-allocation-probe','M2-resolver-allocation-probe','M2-pi-allocation-probe','M2-dtd-framing-allocation-probe','M2-encoding-allocation-probe']
  assert ROOT/'artifacts' in run_root.parents
  roots.add(str(run_root.relative_to(ROOT)))
  rows.append({'probe':run_root.parent.parent.name,'mode':request['Mode'],'measurements':measurements,'requestReportExact':True,'runtimeProductBindingMatches':True,'childXmlPassed':True,'requestSha256':file_sha(path),'reportSha256':file_sha(Path(request['ReportPath']))})
 assert len(rows)==12 and len(roots)==6,'Exact six inherited real+positive child pairs required'
 assert all(any(ROOT/root in (ROOT/name).parents for root in roots) for name in delta),'Unexpected generated artifact delta refused'
 return {'pairs':12,'rows':rows,'rawRoots':sorted(roots),'rawFileCount':len(delta),'rawMembershipSha256':digest({name:now[name] for name in sorted(delta)}),'rawIdentities':{name:now[name] for name in sorted(delta)}}

def delete_allocation_delta(before,summary):
 import shutil
 from identities import sha as file_sha
 for name,value in summary['rawIdentities'].items():assert file_sha(ROOT/name)==value,'Inspected raw changed before deletion'
 for name in summary['rawRoots']:shutil.rmtree(ROOT/name)
 assert all(not (ROOT/name).exists() for name in summary['rawRoots'])
 assert all(file_sha(ROOT/name)==value for name,value in before.items()),'Canonical prior artifacts changed'

def main():
 action=sys.argv[1]
 result=api() if action=='api' else grammar() if action=='grammar' else external(sys.argv[2]) if action=='external' else None
 assert result is not None
 print('GATE_SEMANTIC '+json.dumps({'action':action,'passed':True,'result':result},sort_keys=True),flush=True)
if __name__=='__main__':main()
