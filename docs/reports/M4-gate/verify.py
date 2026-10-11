#!/usr/bin/env python3
"""Owned M4 cumulative gate; raw output stays in ignored artifacts."""
import asyncio, hashlib, json, os, pathlib, subprocess, sys, tempfile, xml.etree.ElementTree as ET, zipfile
ROOT=pathlib.Path(__file__).resolve().parents[3]
OWN=ROOT/'docs/reports/M4-gate'; RAW=ROOT/'artifacts/M4-gate'; RAW.mkdir(parents=True,exist_ok=True)
DOTNET='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
ENV=dict(os.environ,PATH=str(pathlib.Path(DOTNET).parent)+os.pathsep+os.environ['PATH'],XEMELE_DOTNET=DOTNET,DOTNET_ROLL_FORWARD='LatestPatch',PYTHONDONTWRITEBYTECODE='1',DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER='1')
FLAGS=['-c','Release','-m:1','-nr:false','-p:UseSharedCompilation=false','-p:NuGetAudit=false','-p:RestoreIgnoreFailedSources=true','--verbosity','minimal']
def run(args,name,timeout=240,cwd=ROOT,env=ENV):
 p=subprocess.run(list(map(str,args)),cwd=cwd,env=env,capture_output=True,timeout=timeout);(RAW/(name+'.log')).write_bytes(p.stdout+p.stderr);print(name,'exit',p.returncode,flush=True)
 if p.returncode: print((p.stdout+p.stderr).decode(errors='replace')[-3500:]);raise RuntimeError(name+' failed')
 return p.stdout.decode()
def exe(path,*args):return [DOTNET,'--fx-version','10.0.11',path,*args]
def summary(name,data):(RAW/(name+'.json')).write_text(json.dumps(data,indent=2)+'\n')
def prepare():
 for project,name in [('Xemele.slnx','solution-build'),('samples/M2-conformance-grammar/Probe.csproj','grammar-build'),('docs/reports/M4-gate/Demo/Demo.csproj','demo-build'),('docs/reports/M4-corpus/Fuzz.Driver.csproj','fuzz-build'),('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj','api-helper-build')]:run([DOTNET,'build',project,*FLAGS],name)
 run(exe(OWN/'Demo/bin/Release/net10.0/Demo.dll'),'source-demo',30)
def tests():
 rows=[]
 for group in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']:
  xml=RAW/(group+'.xml');run(exe(ROOT/'tests'/group/'bin/Release/net10.0'/(group+'.dll'),'-parallelMode','none','-culture','invariant','-noColor','-result-xml',xml),group,300)
  tree=ET.parse(xml);assemblies=list(tree.getroot().iter('assembly'));counts={k:sum(int(a.get(k,'0')) for a in assemblies) for k in ['total','passed','failed','skipped','errors','not-run']}
  assert counts['total']>0 and counts['passed']==counts['total'] and all(counts[k]==0 for k in ['failed','skipped','errors','not-run']);assert all('.NET 10.0.11' in a.get('environment','') for a in assemblies)
  names=sorted(t.get('name','') for t in tree.getroot().iter('test'));rows.append(dict(group=group,**counts,identitiesSha256=hashlib.sha256('\n'.join(names).encode()).hexdigest()))
 summary('cumulative',dict(revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),groups=rows))
def fuzz():
 data=json.loads(run(exe(ROOT/'docs/reports/M4-corpus/bin/Release/net10.0/Fuzz.Driver.dll'),'xpath-fuzz',30));assert data==dict(seed=1295276112,runtime='.NET 10.0.11',generated=768,reused=768,invalid=128,mutation=32,quota=32,cancellation=64);summary('fuzz',data)
def grammar():
 sys.path.insert(0,str(ROOT/'docs/reports/M2-conformance-grammar'));import full_grammar
 data={'result':'failed','scope':'Unchanged original-byte/source-context normative model, all frozen resources and callback pauses'}
 try:asyncio.run(asyncio.wait_for(full_grammar.execute(data),180))
 except BaseException as error:data['failure']=str(error) or type(error).__name__;raise
 finally:summary('grammar',data);print(json.dumps({k:data[k] for k in ['result','counts','failure'] if k in data}),flush=True)
def api():
 # The official comparison calls bare dotnet for its approved extractor. A
 # transient host shim selects the same explicit runtime for that DLL only.
 shim=RAW/'host-bin';shim.mkdir(exist_ok=True);wrapper=shim/'dotnet';wrapper.write_text('#!/usr/bin/env python3\nimport os,sys\na=sys.argv[1:]\nif a and a[0].endswith(".dll"):a=["--fx-version","10.0.11",*a]\nos.execv('+repr(DOTNET)+',['+repr(DOTNET)+',*a])\n');wrapper.chmod(0o755)
 env=ENV|{'PATH':str(shim)+os.pathsep+ENV['PATH']};path=RAW/'api-full.json';run([sys.executable,'-B',ROOT/'tools/ApiBaseline/compare.py','--report',path],'api',120,env=env);d=json.loads(path.read_text());assert d['matched'] and d['checkerExit']==0 and d['inventory']['apiDeclarations']==897;summary('api-summary',{k:d[k] for k in ['matched','checkerExit','checkerOutput','assemblySha256']})
def package():
 feed=RAW/'packages';feed.mkdir(exist_ok=True);version='1.0.0-m4-preview';run([DOTNET,'pack',ROOT/'src/Xemele',*FLAGS,'--no-build','--no-restore','-p:PackageVersion='+version,'-o',feed],'pack')
 package=feed/('Xemele.'+version+'.nupkg')
 with zipfile.ZipFile(package) as z:
  names=z.namelist();assert 'lib/net10.0/Xemele.dll' in names and not any(n.startswith('lib/') and not n.startswith('lib/net10.0/') for n in names);assert not any(any(w in n for w in ['Contracts','Foundation','corpora']) for n in names);assemblySha=hashlib.sha256(z.read('lib/net10.0/Xemele.dll')).hexdigest()
 assert assemblySha==json.loads((RAW/'api-summary.json').read_text())['assemblySha256']
 with tempfile.TemporaryDirectory(prefix='xemele-m4-package-') as temp:
  temp=pathlib.Path(temp);(temp/'global.json').write_bytes((ROOT/'global.json').read_bytes());(temp/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="'+str(feed)+'"/></packageSources></configuration>');(temp/'Consumer.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="'+version+'" /></ItemGroup></Project>');(temp/'Program.cs').write_bytes((OWN/'Demo/Program.cs').read_bytes());run([DOTNET,'build',temp/'Consumer.csproj',*FLAGS,'-p:RestorePackagesPath='+str(temp/'cache')],'package-build',cwd=temp);output=run(exe(temp/'bin/Release/net10.0/Consumer.dll'),'package-demo',30,cwd=temp);assert output==(RAW/'source-demo.log').read_text()
 summary('package-summary',dict(version=version,packageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),assemblySha256=assemblySha,isolatedConsumer=True,onlyLocalFeed=True,matchesSourceDemo=True,published=False))
if __name__=='__main__':
 phases=dict(prepare=prepare,tests=tests,fuzz=fuzz,grammar=grammar,api=api,package=package)
 if len(sys.argv)!=2 or sys.argv[1] not in phases:raise SystemExit('prepare|tests|fuzz|grammar|api|package')
 phases[sys.argv[1]]()
