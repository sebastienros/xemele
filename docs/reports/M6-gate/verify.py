#!/usr/bin/env python3
"""Owned M6 cumulative gate; raw output stays in ignored artifacts."""
import asyncio, hashlib, json, os, pathlib, subprocess, sys, tempfile, xml.etree.ElementTree as ET, zipfile
ROOT=pathlib.Path(__file__).resolve().parents[3]
OWN=ROOT/'docs/reports/M6-gate'; RAW=ROOT/'artifacts/M6-gate'; RAW.mkdir(parents=True,exist_ok=True)
DOTNET='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
ENV=dict(os.environ,PATH=str(pathlib.Path(DOTNET).parent)+os.pathsep+os.environ['PATH'],XEMELE_DOTNET=DOTNET,DOTNET_ROLL_FORWARD='Disable',DOTNET_CLI_USE_MSBUILD_SERVER='0',MSBUILDDISABLENODEREUSE='1',NUGET_SCRATCH='/private/tmp/xemele-m5-gate-nuget',PYTHONDONTWRITEBYTECODE='1',DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER='1')
FLAGS=['-p:RuntimeFrameworkVersion=10.0.11','-c','Release','-m:1','-nr:false','-p:UseSharedCompilation=false','-p:NuGetAudit=false','-p:RestoreIgnoreFailedSources=true','--verbosity','minimal']
def run(args,name,timeout=240,cwd=ROOT,env=ENV):
 p=subprocess.Popen(list(map(str,args)),cwd=cwd,env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE,start_new_session=True)
 error=None
 try: stdout,stderr=p.communicate(timeout=timeout)
 except BaseException as failure:
  error=failure
  try: os.killpg(p.pid,9)
  except ProcessLookupError: pass
  stdout,stderr=p.communicate(timeout=5)
 finally:
  try: os.killpg(p.pid,9)
  except ProcessLookupError: pass
  if p.poll() is None:p.wait(timeout=5)
 (RAW/(name+'.log')).write_bytes(stdout+stderr);print(name,'exit',p.returncode,flush=True)
 if error:raise error
 if p.returncode: print((stdout+stderr).decode(errors='replace')[-3500:]);raise RuntimeError(name+' failed')
 return stdout.decode()

def exe(path,*args):return [DOTNET,'--fx-version','10.0.11',path,*args]
def summary(name,data):(RAW/(name+'.json')).write_text(json.dumps(data,indent=2)+'\n')
def prepare():
 for project,name in [('Xemele.slnx','solution-build'),('tools/ReferenceRunners/Bcl/BclReference.csproj','bcl-build'),('samples/M2-conformance-grammar/Probe.csproj','grammar-build'),('samples/XmlToolkit/XmlToolkit.csproj','demo-build'),('src/Xemele.Tool/Xemele.Tool.csproj','tool-build'),('docs/reports/M4-corpus/Fuzz.Driver.csproj','fuzz-build'),('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj','api-helper-build')]:
  run([DOTNET,'restore',project,'-p:RuntimeFrameworkVersion=10.0.11','--source','/Users/sebastienros/.nuget/packages','--ignore-failed-sources','-p:NuGetAudit=false','-m:1'],name+'-restore',400)
  run([DOTNET,'build',project,'--no-incremental',*FLAGS,'--no-restore','--disable-build-servers'],name,400)
 run(exe(ROOT/'samples/XmlToolkit/bin/Release/net10.0/XmlToolkit.dll',ROOT/'tests/corpora'),'source-demo',30)
 runtimeconfig_controls()
def runtimeconfig_controls():
 rows=[]
 paths=[ROOT/'tests'/g/'bin/Release/net10.0'/(g+'.runtimeconfig.json') for g in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']]+[ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.runtimeconfig.json']
 for path in paths:
  framework=json.loads(path.read_text())['runtimeOptions']['framework'];assert framework==dict(name='Microsoft.NETCore.App',version='10.0.11'),(path,framework)
  rows.append(dict(path=str(path.relative_to(ROOT)),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),framework=framework))
 summary('runtimeconfig-controls',rows)
def inspect_tests():
 rows=[]
 for group in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']:
  xml=RAW/(group+'.xml')
  tree=ET.parse(xml);assemblies=list(tree.getroot().iter('assembly'));counts={k:sum(int(a.get(k,'0')) for a in assemblies) for k in ['total','passed','failed','skipped','errors','not-run']}
  assert counts['total']>0 and counts['passed']==counts['total'] and all(counts[k]==0 for k in ['failed','skipped','errors','not-run']);assert all('.NET 10.0.11' in a.get('environment','') for a in assemblies)
  names=sorted(t.get('name','') for t in tree.getroot().iter('test'))
  prefixes={
   'Xemele.Tests':['Xemele.Tests.ResourceKinds.ResourceKindTests.','Xemele.Tests.ParserBudget.SharedParserBudgetTests.','Xemele.Tests.M6Adapters.AdapterTests.','Xemele.Tests.M6XInclude.BoundaryTests.','Xemele.Tests.M6XInclude.LedgerTests.','Xemele.Tests.M6XInclude.ContinuationTests.','Xemele.Tests.M6XInclude.IncludeTests.','Xemele.Tests.CatalogTests.','Xemele.Tests.M6SourceUri.SourceUriTests.'],
   'Xemele.Conformance.Tests':['Xemele.Conformance.Tests.Integration.ToolkitTests.'],
   'Xemele.Differential.Tests':[]}[group]
  earlier=[n for n in names if not any(n.startswith(p) for p in prefixes)]
  earlierSha=hashlib.sha256('\n'.join(earlier).encode()).hexdigest()
  baseline=next(r for r in json.loads((ROOT/'docs/reports/M5-gate/summary.json').read_text())['cumulative']['groups'] if r['group']==group)
  assert len(earlier)==baseline['total'] and earlierSha==baseline['identitiesSha256'],(group,len(earlier),earlierSha)
  rows.append(dict(group=group,**counts,identitiesSha256=hashlib.sha256('\n'.join(names).encode()).hexdigest(),earlierCount=len(earlier),earlierIdentitiesSha256=earlierSha,exactEarlierIdentitiesPreserved=True,newCount=len(names)-len(earlier)))
 summary('cumulative',dict(revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),groups=rows))
def fuzz():
 data=json.loads(run(exe(ROOT/'docs/reports/M4-corpus/bin/Release/net10.0/Fuzz.Driver.dll'),'xpath-fuzz',30));assert data==dict(seed=1295276112,runtime='.NET 10.0.11',generated=768,reused=768,invalid=128,mutation=32,quota=32,cancellation=64);summary('fuzz',data)
def grammar():
 sys.path.insert(0,str(ROOT/'docs/reports/M2-conformance-grammar'));import full_grammar
 original=full_grammar.Child
 class PinnedChild(original):
  @classmethod
  async def start(cls,command):
   assert command[0]==DOTNET and str(command[1]).endswith('.dll')
   return await super().start([command[0],'--fx-version','10.0.11',*command[1:]])
 full_grammar.Child=PinnedChild
 data={'result':'failed','scope':'Unchanged original-byte/source-context normative model, all frozen resources and callback pauses'}
 try:asyncio.run(asyncio.wait_for(full_grammar.execute(data),180))
 except BaseException as error:data['failure']=str(error) or type(error).__name__;raise
 finally:summary('grammar',data);print(json.dumps({k:data[k] for k in ['result','counts','failure'] if k in data}),flush=True)
def api():
 # The official comparison calls bare dotnet for its approved extractor. A
 # transient host shim selects the same explicit runtime for that DLL only.
 shim=RAW/'host-bin';shim.mkdir(exist_ok=True);wrapper=shim/'dotnet';wrapper.write_text('#!/usr/bin/env python3\nimport os,sys\na=sys.argv[1:]\nif a and a[0].endswith(".dll"):a=["--fx-version","10.0.11",*a]\nos.execv('+repr(DOTNET)+',['+repr(DOTNET)+',*a])\n');wrapper.chmod(0o755)
 env=ENV|{'PATH':str(shim)+os.pathsep+ENV['PATH']};path=RAW/'api-full.json';run([sys.executable,'-B',ROOT/'tools/ApiBaseline/compare.py','--report',path],'api',120,env=env);d=json.loads(path.read_text());assert d['matched'] and d['checkerExit']==0 and d['inventory']['apiDeclarations']==1054;summary('api-summary',{k:d[k] for k in ['matched','checkerExit','checkerOutput','assemblySha256']})
def package():
 run([sys.executable,'-B',ROOT/'tests/PackageSmoke/Xml/run.py'],'package',600)
 identity=json.loads((ROOT/'artifacts/M6-integration/package-identity.json').read_text())
 assert identity['source_package_dll_equal'] and identity['sha256']==json.loads((RAW/'api-summary.json').read_text())['assemblySha256']
 local=RAW/'packages';local.mkdir(exist_ok=True)
 package=pathlib.Path(identity['package']);retained=local/package.name;retained.write_bytes(package.read_bytes())
 summary('package-summary',identity|{'retained':str(retained),'packageSha256':hashlib.sha256(retained.read_bytes()).hexdigest(),'published':False})
def cli():
 run([sys.executable,'-B',ROOT/'samples/XmlToolkit/cli.py'],'cli-examples',240)
 run([sys.executable,'-B',ROOT/'tests/Xemele.Tests/M6-cli/process_controls.py'],'cli-managed',240)
 native=RAW/'native-cli'
 run([DOTNET,'restore',ROOT/'src/Xemele.Tool/Xemele.Tool.csproj','-r','osx-arm64','-p:PublishAot=true','-p:RuntimeFrameworkVersion=10.0.11','-p:NuGetAudit=false','--source','/Users/sebastienros/.nuget/packages','--disable-build-servers','-m:1'],'cli-aot-restore',180)
 run([DOTNET,'publish',ROOT/'src/Xemele.Tool/Xemele.Tool.csproj','-c','Release','--no-restore','-r','osx-arm64','--self-contained','true','-p:PublishAot=true','-p:RuntimeFrameworkVersion=10.0.11','-t:Rebuild,Publish','--disable-build-servers','-m:1','-o',native],'cli-aot-publish',180)
 binary=native/'Xemele.Tool'; info=run(['file',binary],'cli-aot-file',10);assert 'Mach-O 64-bit executable arm64' in info and not (native/'Xemele.Tool.dll').exists()
 run([sys.executable,'-B',ROOT/'tests/Xemele.Tests/M6-cli/process_controls.py'],'cli-native',240,env=ENV|{'XEMELE_CLI_NATIVE':str(binary)})
 summary('cli-summary',{'nativeSha256':hashlib.sha256(binary.read_bytes()).hexdigest(),'platform':'osx-arm64','managedAndNativeControls':57,'examples':7})
if __name__=='__main__':
 phases=dict(prepare=prepare,inspect_tests=inspect_tests,fuzz=fuzz,grammar=grammar,api=api,package=package,cli=cli)
 if len(sys.argv)!=2 or sys.argv[1] not in phases:raise SystemExit('prepare|inspect_tests|fuzz|grammar|api|package')
 phases[sys.argv[1]]()
