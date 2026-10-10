#!/usr/bin/env python3
"""Contract compilation/metadata only. No product parse, corpus replay or measurement."""
import hashlib,json,os,re,shutil,signal,subprocess,sys,tempfile
from pathlib import Path
from xml.sax.saxutils import escape
ROOT=Path(__file__).resolve().parents[3]
CLAIM='76c94937db9f301e6442cad87308452b6ad45c83'
TASK='M2-undeclared-attribute-contract'
SDK=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
ENV=dict(os.environ,DOTNET_ROOT=str(SDK.parent),DOTNET_CLI_USE_MSBUILD_SERVER='0')
ENV['PATH']=str(SDK.parent)+os.pathsep+ENV.get('PATH','')
REPORT=ROOT/f'docs/reports/{TASK}/checks.json'
s={'result':'failed','scope':'Nonpackaged independent contract/model checks; no product runtime/conformance or allocation claims','checks':[]}
def run(*cmd,expected=0):
 command=list(map(str,cmd))
 p=subprocess.Popen(command,cwd=ROOT,env=ENV,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,start_new_session=True)
 try:
  output,_=p.communicate(timeout=180)
 except subprocess.TimeoutExpired:
  os.killpg(p.pid,signal.SIGKILL);p.communicate();raise RuntimeError('Bounded child timed out: '+str(cmd))
 finally:
  # This session/process group belongs only to this invocation; clear descendants.
  try:os.killpg(p.pid,signal.SIGKILL)
  except ProcessLookupError:pass
 s['checks'].append({'command':command,'exit':p.returncode,'expectedExit':expected})
 if p.returncode!=expected:
  print(output[-6500:],file=sys.stderr);raise RuntimeError('Unexpected command exit: '+str(cmd))
 return output

def hashfile(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def freeze(paths):return {str(p):hashfile(p) for p in paths}
def unchanged(values):
 for p,h in values.items():
  if hashfile(p)!=h:raise RuntimeError('Changed frozen input: '+p)
def source_project(path,sources,name):
 path.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors><AssemblyName>'+name+'</AssemblyName></PropertyGroup><ItemGroup>'+''.join('<Compile Include="'+escape(str(p))+'" />' for p in sources)+'</ItemGroup></Project>')
try:
 rev=run('git','rev-parse','HEAD').strip();s['revision']=rev
 run('git','merge-base','--is-ancestor',CLAIM,rev)
 task=json.loads((ROOT/f'docs/tasks/{TASK}.json').read_text())
 assert task['branch']==run('git','branch','--show-current').strip()
 assert task['owner']=='worker:'+TASK and task['status'] in ['in_progress','implemented']
 s['dependencies']={}
 for dep in task['depends_on']:
  d=json.loads((ROOT/f'docs/tasks/{dep}.json').read_text());assert d['status']=='verified'
  run('git','merge-base','--is-ancestor',d['integration_revision'],rev);s['dependencies'][dep]=d['integration_revision']
 paths=run('git','ls-files','--','src/Xemele','docs/api','tests/Contracts/Fixtures','Directory.Build.props','Directory.Packages.props','global.json','NuGet.config','samples/'+TASK,'docs/reports/'+TASK+'/verify.py','docs/decisions/M2/undeclared-references.md','docs/specs/parser.md','tests/corpora/xml/inventory.json','tests/corpora/xml/m2-applicability.json').splitlines()
 hashes={}
 for path in paths:
  data=subprocess.check_output(['git','show',rev+':'+path],cwd=ROOT)
  assert (ROOT/path).read_bytes()==data,'Uncommitted source '+path
  hashes[path]=hashlib.sha256(data).hexdigest()
 frozen={str(ROOT/p):h for p,h in hashes.items()}
 s['sourceCount']=len(hashes);s['sourceClosureSha256']=hashlib.sha256(json.dumps(hashes,sort_keys=True).encode()).hexdigest()
 s['proposalInputs']={p:h for p,h in hashes.items() if '/UndeclaredAttributes/' in p or '/'+TASK+'/' in p}
 for p in ['src','docs/api','Directory.Build.props','Directory.Packages.props','global.json','NuGet.config','tests/corpora/xml']:
  assert not run('git','diff',CLAIM,rev,'--',p).strip(),'Outside preserved inputs changed '+p
 identities=json.loads((ROOT/'tests/Contracts/Fixtures/M2/UndeclaredAttributes/authored-identities.json').read_text())
 assert len(identities['observations'])==7
 for i,row in enumerate(identities['observations']):
  fixture=ROOT/'tests/Contracts/Fixtures/M2/UndeclaredAttributes'/row['fixture']
  assert hashfile(fixture)==row['inputSha256'] and row['id']=='unknown-attribute-'+str(i)
  assert row['classification'].startswith('EntityDeclared-WFC')==(i in [0,1,3])
  assert row['build']=='1.0.0+588ae1cbcd74b2a585b8f779221baf88db389c5f'
 assert hashlib.sha256(identities['resource']['utf8'].encode()).hexdigest()==identities['resource']['sha256']
 s['authoredHistoricalIdentities']={'count':7,'WfcControls':3,'VcRepresentationGaps':4,'hashBound':True,'productObservationRevision':'588ae1cbcd74b2a585b8f779221baf88db389c5f','executedByThisTask':False}
 inventory=json.loads((ROOT/'tests/corpora/xml/inventory.json').read_text());ledger=json.loads((ROOT/'tests/corpora/xml/m2-applicability.json').read_text())
 assert len(inventory['cases'])==len(ledger['cases'])==2585
 assert len(inventory['files'])==len(ledger['files'])==3386 and inventory['files']==ledger['files']
 assert {x['id']:x['inputSha256'] for x in inventory['cases']}=={x['id']:x['inputSha256'] for x in ledger['cases']}
 s['originalCorpusPreserved']={'cases':2585,'resources':3386,'inventorySha256':hashfile(ROOT/'tests/corpora/xml/inventory.json'),'ledgerSha256':hashfile(ROOT/'tests/corpora/xml/m2-applicability.json'),'assessmentOnly':True}
 assert run(SDK,'--version').strip()=='10.0.400'
 rt=SDK.parent/'shared/Microsoft.NETCore.App/10.0.11'
 runtime_hashes=freeze([SDK,rt/'System.Private.CoreLib.dll',rt/'libcoreclr.dylib',rt/'libhostpolicy.dylib'])
 frozen.update(runtime_hashes);s['pinnedRuntimeInputs']=runtime_hashes
 with tempfile.TemporaryDirectory(prefix='attribute-contract-',dir='/private/tmp') as directory:
  temp=Path(directory);art=temp/'build';flags=[*FLAGS,'--artifacts-path',art]
  run(SDK,'build',ROOT/'src/Xemele/Xemele.csproj',*flags)
  product=art/'bin/Xemele/release/Xemele.dll'
  run(SDK,'build',ROOT/'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj',*flags)
  checker=art/'bin/ApiCheck/release/ApiCheck.dll'
  sys.path.insert(0,str(ROOT/'tools/ApiBaseline'));from compare import compose
  api_root=temp/'api-root';(api_root/'docs/api').mkdir(parents=True)
  for fragment in (ROOT/'docs/api').glob('*.txt'):shutil.copyfile(fragment,api_root/'docs/api'/fragment.name)
  union,api=compose(api_root);baseline=temp/'public-api.txt';baseline.write_text(union)
  run(SDK,checker,'compare',product,baseline)
  assert len(api['provenance'])==290
  s['currentPublicApi']={'members':290,'assemblySha256':hashfile(product),'baselineSha256':hashfile(baseline)}
  sample=ROOT/f'samples/{TASK}/Sample.csproj'
  run(SDK,'build',sample,*flags)
  consumer=art/'bin/Sample/release/AttributeContractConsumer.dll'
  model=art/'bin/Model/release/AttributeContractModel.dll'
  binaries=freeze([product,checker,consumer,model])
  output=run(SDK,consumer,product,rev)
  s['modelAssertions']=int(re.search(r'Passed (\d+)',output)[1])
  ident=json.loads(re.search(r'RuntimeIdentity:(.+)',output)[1]);assert ident['Version']=='10.0.11' and ident['ProcessArchitecture']=='Arm64'
  assert Path(ident['CoreLibrary']).resolve()==(rt/'System.Private.CoreLib.dll').resolve()
  ident['CoreLibrarySha256']=hashfile(ident['CoreLibrary']);assert ident['CoreLibrarySha256']==runtime_hashes[str(rt/'System.Private.CoreLib.dll')]
  assert re.fullmatch(r'10\.0\.11(?:-[0-9A-Za-z.-]+)?\+[0-9a-f]{40}',ident['CoreLibraryInformationalVersion'])
  s['executingRuntime']=ident;s['binaries']=binaries
  s['preservedModels']={'frozenInternalShapes':7,'publicHandleAndContentShapes':7,'independentAssemblies':['AttributeContractModel','AttributeContractConsumer']}
  probe_flags=[*FLAGS,'--artifacts-path',temp/'probes']
  negatives=[('borrowed-attribute','class Probe { Xemele.Parsing.Syntax.XmlIncompleteAttributeView retained; }','CS8345'),('borrowed-default','class Probe { Xemele.Parsing.Syntax.XmlIncompleteAttributeDefaultView retained; }','CS8345'),('immutable-record','class Probe { void Use(Xemele.XmlIncompleteAttributeValue v) { v.LexicalValue="lost"; } }','CS8852'),('no-complete-value','class Probe { string Use(Xemele.Parsing.Syntax.XmlIncompleteAttributeView v) => v.Value.ToString(); }','CS1061')]
  for name,code,error in negatives:
   probe=temp/(name+'.cs');probe.write_text(code)
   output=run(SDK,'build',sample,*probe_flags,'-p:ProbeSource='+str(probe),expected=1);assert error in output,name
  s['compilerNegatives']=[{'name':n,'requiredError':e} for n,_,e in negatives]
  old=temp/'old-sink.cs';old.write_text('''using Xemele; using Xemele.Parsing.Syntax;
sealed class OldSink : IXmlEventSink {
 public bool StartDocument(XmlLocation l)=>true;public bool Declaration(XmlDeclarationView d)=>true;
 public bool StartElement(XmlNameView n,bool e,XmlLocation l)=>true;public bool NamespaceDeclaration(XmlNamespaceView n)=>true;
 public bool Attribute(XmlAttributeView a)=>true;public bool StartElementContent()=>true;public bool EndElement(XmlNameView n,XmlLocation l)=>true;
 public bool StartText(XmlTextKind k,ReadOnlySpan<char> t,XmlLocation l)=>true;public bool TextFragment(ReadOnlySpan<char> t,bool e,XmlLocation l)=>true;public bool EndDocument(XmlLocation l)=>true; }
''')
  run(SDK,'build',sample,*probe_flags,'-p:ProbeSource='+str(old));s['oldSinkCompilesWithoutNewInterface']=True
  # Compile deliberately changed frozen event metadata, then exercise real reflection comparison.
  syntax=temp/'mutated-syntax.cs';syntax.write_text((ROOT/'tests/Contracts/Fixtures/M2/Syntax.cs.fixture').read_text().replace('internal readonly ref struct XmlAttributeView','internal ref struct XmlAttributeView'))
  source_paths=[ROOT/f'tests/Contracts/Fixtures/{p}.cs.fixture' for p in ['M2/Common','M2/Options','M2/Resolver','M2/Dtd','M1/Tree','M2/Entities']]+[syntax]
  mutant=temp/'Mutant.csproj';source_project(mutant,source_paths,'Mutant')
  run(SDK,'build',mutant,*flags)
  output=run(SDK,consumer,art/'bin/Mutant/release/Mutant.dll',rev,expected=1)
  assert 'Frozen ABI mismatch: Xemele.Parsing.Syntax.XmlAttributeView' in output
  s['compiledAbiMismatchDetected']='readonly attribute view changed to mutable'
  # Mutation of the exact optional proposal breaks the independently compiled consumer.
  bad=temp/'bad-proposal.cs';bad.write_text((ROOT/'tests/Contracts/Fixtures/M2/UndeclaredAttributes/Proposal.cs.fixture').read_text().replace('bool IncompleteAttribute(XmlIncompleteAttributeView','int IncompleteAttribute(XmlIncompleteAttributeView'))
  xml=(ROOT/f'samples/{TASK}/Model.csproj').read_text()
  includes=re.findall(r'<Compile Include="([^"]+)"',xml)
  bad_sources=[(ROOT/f'samples/{TASK}'/p).resolve() for p in includes]
  bad_sources=[bad if str(p).endswith('/UndeclaredAttributes/Proposal.cs.fixture') else p for p in bad_sources]
  # Same friend assembly name, but altered contract and independent consumer sources in a temporary project.
  bad_sources += [ROOT/f'samples/{TASK}/Program.cs',ROOT/'tests/Contracts/Fixtures/M2/Undeclared/Preservation.cs.fixture']
  badproj=temp/'BadContract.csproj';source_project(badproj,bad_sources,'AttributeContractConsumer')
  output=run(SDK,'build',badproj,*flags,expected=1);assert 'CS0738' in output
  s['proposalConsumerMismatchDetected']='CS0738 bool callback changed to int'
  guard=temp/'source-control';guard.write_text('bound');guard_hash=freeze([guard]);guard.write_text('changed')
  detected=False
  try:unchanged(guard_hash)
  except RuntimeError:detected=True
  assert detected;s['changedSourceNegativeDetected']=True
  unchanged(binaries);s['executedBinariesUnchanged']=True
 s['temporaryOutputsRemoved']=not temp.exists()
 output=run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py')
 s['planningControlCount']=int(re.search(r'Ran (\d+) tests',output)[1])
 s['planningValidation']=run('python3','-B','tools/planning/tasks.py','validate').strip()
 run('git','diff','--check',CLAIM,'HEAD');run('git','diff','--check')
 changed=set(run('git','diff','--name-only',CLAIM,'HEAD').splitlines())|set(run('git','diff','--name-only','HEAD').splitlines())|set(run('git','ls-files','--others','--exclude-standard').splitlines())
 owned=task['owns']+['docs/tasks/'+TASK+'.json']
 assert all(any(p.startswith(o) if o.endswith('/') else p==o for o in owned) for p in changed),sorted(changed)
 s['ownedPaths']=sorted(changed);assert run('git','rev-parse','HEAD').strip()==rev
 unchanged(frozen);s['finalFrozenInputsUnchanged']=True;s['result']='passed'
except Exception as e:
 s['failure']=str(e)
 s['temporaryOutputsRemoved']='temp' not in globals() or not temp.exists()
REPORT.write_text(json.dumps(s,indent=2)+'\n');print(json.dumps({k:v for k,v in s.items() if k not in ['checks','proposalInputs','pinnedRuntimeInputs','binaries','ownedPaths']},indent=2))
sys.exit(0 if s['result']=='passed' else 1)
