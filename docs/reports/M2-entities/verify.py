"""Exact-commit correctness/package/API provenance. Allocation and timing modes are separate and require an exclusive grant."""
import argparse,gzip,hashlib,json,os,platform,re,shlex,shutil,subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
REPORT=ROOT/'docs/reports/M2-entities'
CLAIM='b3705965c0ed3a27cad117f4fdcd5cee44738079'
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
def main():
 p=argparse.ArgumentParser();p.add_argument('--report-prefix',required=True);a=p.parse_args();assert re.fullmatch('[a-z0-9-]+',a.report_prefix)
 report=REPORT/a.report_prefix;report.mkdir();raw=ROOT/'artifacts/M2-entities'/a.report_prefix;raw.mkdir(parents=True)
 sdk=Path(shutil.which('dotnet')).resolve(strict=True)
 env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0',XEMELE_DOTNET=str(sdk),XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',XEMELE_CORPUS_RESULTS=str(raw/'case-results'))
 logs=[];s={'result':'failed','host':platform.platform(),'architecture':platform.machine(),'auditBase':CLAIM}
 def run(*cmd,expected=0):
  cmd=list(map(str,cmd));cmd[0]=str(sdk) if cmd[0]=='dotnet' else cmd[0];print('$ '+shlex.join(cmd),flush=True)
  r=subprocess.run(cmd,cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=600)
  logs.append('$ '+shlex.join(cmd)+'\n'+r.stdout+f'\nexit={r.returncode}; expected={expected}\n');print(r.stdout[-1000:],flush=True);assert r.returncode==expected,logs[-1];return r.stdout
 def identities(directory):return {str(f.relative_to(directory)):hashlib.sha256(f.read_bytes()).hexdigest() for f in directory.rglob('*') if f.is_file()}
 def save(name,data): (report/name).write_bytes(gzip.compress(data.encode() if isinstance(data,str) else data,mtime=0))
 try:
  revision=run('git','rev-parse','HEAD').strip();s['sourceRevision']=revision;run('git','merge-base','--is-ancestor',CLAIM,'HEAD');assert run('git','branch','--show-current').strip()=='codex/m2-entities'
  task=json.loads((ROOT/'docs/tasks/M2-entities.json').read_text())
  for dep in task['depends_on']:
   record=json.loads((ROOT/f'docs/tasks/{dep}.json').read_text());assert record['status']=='verified';run('git','merge-base','--is-ancestor',record['integration_revision'],'HEAD')
  source=[x for x in task['owns'] if not x.startswith('docs/reports/')];tracked=run('git','ls-files','--',*source).splitlines();before={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in tracked};s['sourceIdentitiesBefore']=before
  assert not run('git','diff','HEAD','--name-only','--',*source).strip();assert not run('git','ls-files','--others','--exclude-standard','--',*source).strip()
  assert run('dotnet','--version').strip()=='10.0.400'
  run('python3','-B','docs/reports/M1-corpus-import/provision.py');registry=json.loads(run('python3','-B','tools/ApiBaseline/discover.py'));assert registry['fixtures']==3388 and registry['apiDeclarations']==272;s['registry']=registry
  run('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS);runner=ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll';shutil.copytree(runner.parent,raw/'pinned-reference');pinned=raw/'pinned-reference'/runner.name;reference_files=identities(pinned.parent);runner_hash=hashlib.sha256(pinned.read_bytes()).hexdigest();env['XEMELE_BCL_RUNNER']=str(pinned)
  inherited=run('python3','-B','docs/reports/M1-contracts/verify.py');save('inherited.txt.gz',inherited)
  counts={};all_runtime=set();runtime_rows=[]
  for assembly in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']:
   listing=run('dotnet','test','--project',ROOT/f'tests/{assembly}','-c','Release','--no-build','--no-restore','--list-tests');save(assembly+'-discovery.txt.gz',listing)
   discovered=re.findall(r'Discovered (\d+) tests\.',listing);assert discovered
   xml=raw/(assembly+'.xml');run('dotnet',ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll','-noColor','-result-xml',xml)
   entries=list(ET.parse(xml).iter('test'));assert len(entries)==int(discovered[-1]) and all(t.get('result')=='Pass' for t in entries)
   counts[assembly]={'total':len(entries),'failed':0,'skipped':0};save(assembly+'.xml.gz',xml.read_bytes())
   if assembly=='Xemele.Tests':runtime_rows=entries;all_runtime={(t.get('type'),t.get('method'),t.get('name')) for t in entries}
  assert counts['Xemele.Conformance.Tests']['total']==255 and counts['Xemele.Differential.Tests']['total']==299
  entity=[t for t in runtime_rows if t.get('type','').startswith('Xemele.Tests.Entities.')];foundation=[t for t in runtime_rows if t.get('type','').startswith('Xemele.Tests.EntitiesFoundation.')]
  assert entity and len(foundation)==9;assert counts['Xemele.Tests']['total']==4514+len(entity)
  totals=re.findall(r'^\s*total:\s*(\d+)\s*$',inherited,re.MULTILINE);assert totals and int(totals[-1])==sum(c['total'] for c in counts.values());s['mtpTotal']=int(totals[-1]);s['directAndDiscoveryTests']=counts;s['entityTests']=len(entity);s['foundationTests']=len(foundation)
  s['foundationTransition']='Only two theory IDs replace transitional Parse rejection with real Parse admission; default Prohibit and no unrequested open remain tested.'
  run('python3','-B','tools/ApiBaseline/compare.py','--report',report/'runtime-api.json');api=json.loads((report/'runtime-api.json').read_text());assert api['matched'] and len(api['provenance'])==272;s['actualRuntimeMembers']=272;s['publicAdditions']=0
  run('dotnet','build','samples/M2-entities/Sample.csproj',*FLAGS);sample=ROOT/'samples/M2-entities/bin/Release/net10.0/Xemele.SyntaxSample.dll';assert run('dotnet',sample,'--runtime').strip()=='10.0.11'
  actual=run('dotnet',sample,'--contract');(report/'actual-contract.txt').write_text(actual)
  approved=raw/'approved';approved.mkdir();fixture_root=ROOT/'tests/Contracts/Fixtures'
  for name in ['M2/Common','M2/Options','M2/Resolver','M2/Dtd','M1/Tree','M2/Entities','M2/Syntax','M1/Input','M2/DomMetadata']:
   text=(fixture_root/(name+'.cs.fixture')).read_text()
   if name=='M2/Options':text=text.replace('    public bool Recover { get; init; }\n','')
   (approved/(name.replace('/','-')+'.cs')).write_text(text)
  (approved/'Program.cs').write_text('Contract.Print();')
  (approved/'Approved.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><Compile Include="'+str(ROOT/'samples/M2-entities/Contract.cs')+'" /></ItemGroup></Project>')
  run('dotnet','build',approved/'Approved.csproj',*FLAGS);model=approved/'bin/Release/net10.0/Approved.dll';approved_output=run('dotnet',model);(report/'approved-contract.txt').write_text(approved_output);assert actual==approved_output
  output=run('dotnet',sample);assert 'DOM:doctype=r;namespace=urn:r;default=owned tail;id=one;text=payloadtail' in output and 'streams=3;disposed=3' in output;(report/'sample.txt').write_text(output)
  run('dotnet','pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','-o',raw/'feed');packages=list((raw/'feed').glob('*.nupkg'));assert len(packages)==1;s['packageSha256']=hashlib.sha256(packages[0].read_bytes()).hexdigest()
  run('dotnet','build','samples/M2-entities/Sample.csproj',*FLAGS,'-p:UsePackage=true','-p:ManagePackageVersionsCentrally=false',f'-p:RestoreSources={raw}/feed',f'-p:RestorePackagesPath={raw}/packages','-p:NuGetAudit=false')
  assert run('dotnet',sample)==output;assert run('dotnet',sample,'--contract')==actual;s['packageConsumer']='fresh nupkg output and compiled internal constructor/context/optional hook match'
  run('dotnet','build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*FLAGS);run('dotnet','build','docs/reports/M2-entities/benchmark-driver/Xemele.EntitiesBenchmarks.csproj',*FLAGS);run('dotnet','build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*FLAGS)
  standalone=ROOT/'docs/reports/M2-entities/benchmark-driver/bin/Release/net10.0/Xemele.EntitiesBenchmarks.dll';oracle=run('dotnet',standalone,'--oracle');(report/'benchmark-oracle.txt').write_text(oracle)
  for assembly in [standalone,ROOT/'benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll']:
   listing=run('dotnet',assembly,'--list','flat','--filter','*Entity*Benchmarks*');assert all(n in listing for n in ['EntityCompositionBenchmarks.InternalDocument','EntityCompositionBenchmarks.ExternalDocument','EntityLookupBenchmarks.GeneralLookup','EntityLookupBenchmarks.DefaultLookup']);logs.append(listing)
  s['benchmarkFixtures']='main/standalone build and discovery; actual timed methods full-output oracle passed; no timing/allocation mode'
  records=[json.loads(f.read_text()) for f in (raw/'case-results').glob('reference-*.json')];assert len(records)==242 and len({r['id'] for r in records})==242 and identities(pinned.parent)==reference_files
  assert all(r['runnerSha256']==runner_hash and r['response']['engine']['build']=='1.0.0+'+revision for r in records);assert sum(r['comparison']=='matched-rejection' for r in records)==239 and sum(r['comparison']=='documented-reference-divergence' for r in records)==3
  s['referenceProvenance']={'records':242,'matchedRejections':239,'documentedDivergences':3,'runnerSha256':runner_hash,'build':'1.0.0+'+revision,'outputIdentities':reference_files,'freshCompleteCopy':True,'unchangedCopy':True}
  (report/'reference-provenance.json').write_text(json.dumps([{'id':r['id'],'runnerSha256':r['runnerSha256'],'build':r['response']['engine']['build']} for r in sorted(records,key=lambda r:r['id'])],indent=2)+'\n')
  run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v');validation=run('python3','-B','tools/planning/tasks.py','validate');assert 'Validated 123 tasks:' in validation;s['planningValidation']=validation.strip()
  paths=[]
  for cmd in [('git','diff','--name-only',CLAIM+'..HEAD'),('git','diff','--name-only'),('git','diff','--cached','--name-only'),('git','ls-files','--others','--exclude-standard')]:paths+=run(*cmd).splitlines()
  owned=task['owns']+['docs/tasks/M2-entities.json'];assert all(any(path.startswith(o) if o.endswith('/') else path==o for o in owned) for path in paths),paths
  for suffix in [[CLAIM+'..HEAD'],['--cached'],[]]:run('git','diff','--check',*suffix)
  after={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in tracked};assert after==before and run('git','rev-parse','HEAD').strip()==revision;s['sourceIdentitiesAfter']=after;s['ownedPaths']=sorted(set(paths));s['result']='passed';s['limitations']='macOS arm64 only; eight pinned simple document cases are not a full XML conformance oracle; timing/allocation modes held for exclusive grant.'
 finally:
  save('checks.txt.gz','\n'.join(logs));(report/'summary.json').write_text(json.dumps(s,indent=2)+'\n')
if __name__=='__main__':main()
