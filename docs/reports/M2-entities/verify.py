"""Exact-commit correctness/package/API provenance. Allocation and timing modes are separate and require an exclusive grant."""
import argparse,hashlib,json,os,platform,re,shlex,shutil,subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
REPORT=ROOT/'docs/reports/M2-entities'
CLAIM='a29d2dac7bdd4c36c51797bd21752ebcec6b81e9'
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
def finalize(summary, commands, raw, child_root, prior_children, inherited_log, inherited_invoked, prior_log, report):
 """Cleanup must preserve the primary check failure and still emit a summary."""
 errors=[]
 def guarded(label, action):
  try:return action()
  except Exception as error:errors.append({'stage':label,'type':type(error).__name__,'message':str(error)[:400]});return None
 summary['commands']=commands;summary['artifactPolicy']='Summaries/provenance only; current generated outputs removed after inspection.'
 new_children=guarded('enumerate-current-children',lambda:(set(child_root.iterdir())-prior_children) if child_root.exists() else set()) or set()
 summary['temporaryChildPairsRemoved']=len(new_children);summary['framingChildSummaries']=[]
 for directory in sorted(new_children):
  pair={}
  for mode in ['real','positive']:
   payload=directory/(mode+'.report.json')
   def summarize():
    data=json.loads(payload.read_text())
    result={k:data[k] for k in ['ProcessId','WarmIterations','MeasuredIterations','AllocatedBytes','WarmConsumed','MeasuredConsumed','FinalColumn']}
    result['sourceVersion']=data['Actual']['ProductVersion'];result['productSha256']=data['Actual']['ProductSha256'];return result
   if payload.exists():pair[mode]=guarded('parse-'+directory.name+'-'+mode,summarize)
  summary['framingChildSummaries'].append(pair)
 for directory in new_children:guarded('remove-current-child-'+directory.name,lambda d=directory:shutil.rmtree(d))
 if raw.exists():guarded('remove-current-raw',lambda:shutil.rmtree(raw))
 if inherited_invoked:
  # Restore any pre-existing peer log exactly; only a newly created output is removed.
  if prior_log is not None:guarded('restore-prior-inherited-log',lambda:inherited_log.write_bytes(prior_log))
  elif inherited_log.exists():guarded('remove-current-inherited-log',inherited_log.unlink)
 summary['temporaryOutputsRemoved']=not raw.exists() and all(not d.exists() for d in new_children)
 if errors:summary['cleanupErrors']=errors;summary['result']='failed'
 (report/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
 return not errors

def main():
 p=argparse.ArgumentParser();p.add_argument('--report-prefix',required=True);p.add_argument('--audit-base',default=CLAIM);p.add_argument('--expected-branch',default='codex/m2-entities');a=p.parse_args();assert re.fullmatch('[a-z0-9-]+',a.report_prefix)
 report=REPORT/a.report_prefix;report.mkdir();raw=ROOT/'artifacts/M2-entities'/a.report_prefix;raw.mkdir(parents=True)
 sdk=Path(shutil.which('dotnet')).resolve(strict=True)
 env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0',XEMELE_DOTNET=str(sdk),XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',XEMELE_CORPUS_RESULTS=str(raw/'case-results'))
 child_root=ROOT/'artifacts/M2-dtd-framing-allocation-probe/child-runs';prior_children=set(child_root.iterdir()) if child_root.exists() else set()
 inherited_log=ROOT/'artifacts/M1-contracts/checks.txt';inherited_invoked=False;prior_log=None
 commands=[];s={'result':'failed','host':platform.platform(),'architecture':platform.machine(),'auditBase':a.audit_base}
 def run(*cmd,expected=0):
  cmd=list(map(str,cmd));cmd[0]=str(sdk) if cmd[0]=='dotnet' else cmd[0];print('$ '+shlex.join(cmd),flush=True)
  r=subprocess.run(cmd,cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=600)
  commands.append({'command':shlex.join(cmd),'exit':r.returncode,'expected':expected});print(r.stdout[-1000:],flush=True)
  if r.returncode!=expected:
   s['failure']={'command':shlex.join(cmd),'exit':r.returncode,'outputTail':r.stdout[-3500:]};raise AssertionError('Check failed: '+shlex.join(cmd))
  return r.stdout
 def identities(directory):return {str(f.relative_to(directory)):hashlib.sha256(f.read_bytes()).hexdigest() for f in directory.rglob('*') if f.is_file()}
 try:
  revision=run('git','rev-parse','HEAD').strip();s['sourceRevision']=revision;run('git','merge-base','--is-ancestor',CLAIM,'HEAD');run('git','merge-base','--is-ancestor',a.audit_base,'HEAD');assert run('git','branch','--show-current').strip()==a.expected_branch;s['expectedBranch']=a.expected_branch
  task=json.loads((ROOT/'docs/tasks/M2-entities.json').read_text())
  for dep in task['depends_on']:
   record=json.loads((ROOT/f'docs/tasks/{dep}.json').read_text());assert record['status']=='verified';run('git','merge-base','--is-ancestor',record['integration_revision'],'HEAD')
  source=[x for x in task['owns'] if not x.startswith('docs/reports/')];tracked=run('git','ls-files','--',*source).splitlines();before={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in tracked};s['sourceIdentitiesBefore']=before
  assert not run('git','diff','HEAD','--name-only','--',*source).strip();assert not run('git','ls-files','--others','--exclude-standard','--',*source).strip()
  global_sources=[n for n in run('git','ls-files').splitlines() if n.endswith(('.cs','.csproj','.props','.targets','.slnx','.cs.fixture')) or n in ('global.json','NuGet.Config')];global_before={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in global_sources};s['globalCompiledSourcesBefore']=global_before
  assert run('dotnet','--version').strip()=='10.0.400'
  run('python3','-B','docs/reports/M1-corpus-import/provision.py');registry=json.loads(run('python3','-B','tools/ApiBaseline/discover.py'));assert registry['fixtures']==3388 and registry['apiDeclarations']==272;s['registry']=registry
  run('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS);runner=ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll';shutil.copytree(runner.parent,raw/'pinned-reference');pinned=raw/'pinned-reference'/runner.name;reference_files=identities(pinned.parent);runner_hash=hashlib.sha256(pinned.read_bytes()).hexdigest();env['XEMELE_BCL_RUNNER']=str(pinned)
  prior_log=inherited_log.read_bytes() if inherited_log.exists() else None;inherited_invoked=True
  inherited=run('python3','-B','docs/reports/M1-contracts/verify.py')
  counts={};all_runtime=set();runtime_rows=[]
  for assembly in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']:
   listing=run('dotnet','test','--project',ROOT/f'tests/{assembly}','-c','Release','--no-build','--no-restore','--list-tests')
   discovered=re.findall(r'Discovered (\d+) tests\.',listing);assert discovered
   xml=raw/(assembly+'.xml');run('dotnet',ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll','-noColor','-result-xml',xml)
   entries=list(ET.parse(xml).iter('test'));assert len(entries)==int(discovered[-1]) and all(t.get('result')=='Pass' for t in entries)
   counts[assembly]={'total':len(entries),'failed':0,'skipped':0}
   if assembly=='Xemele.Tests':runtime_rows=entries;all_runtime={(t.get('type'),t.get('method'),t.get('name')) for t in entries}
  assert counts['Xemele.Conformance.Tests']['total']==255 and counts['Xemele.Differential.Tests']['total']==299
  entity=[t for t in runtime_rows if t.get('type','').startswith('Xemele.Tests.Entities.')];foundation=[t for t in runtime_rows if t.get('type','').startswith('Xemele.Tests.EntitiesFoundation.')]
  assert entity and len(foundation)==9;assert counts['Xemele.Tests']['total']==4515+len(entity)
  totals=re.findall(r'^\s*total:\s*(\d+)\s*$',inherited,re.MULTILINE);assert totals and int(totals[-1])==sum(c['total'] for c in counts.values());s['mtpTotal']=int(totals[-1]);s['directAndDiscoveryTests']=counts;s['entityTests']=len(entity);s['foundationTests']=len(foundation)
  run('python3','-B','docs/reports/M2-entities/preservation.py',raw,report)
  s['foundationTransition']='Only two theory IDs replace transitional Parse rejection with real Parse admission; default Prohibit and no unrequested open remain tested.'
  run('python3','-B','tools/ApiBaseline/compare.py','--report',raw/'runtime-api.json');api=json.loads((raw/'runtime-api.json').read_text());assert api['matched'] and len(api['provenance'])==272;s['actualRuntimeMembers']=272;s['publicAdditions']=0
  run('dotnet','build','samples/M2-entities/Sample.csproj',*FLAGS);sample=ROOT/'samples/M2-entities/bin/Release/net10.0/Xemele.SyntaxSample.dll';assert run('dotnet',sample,'--runtime').strip()=='10.0.11'
  actual=run('dotnet',sample,'--contract');s['compiledActualContract']=actual.splitlines()
  approved=raw/'approved';approved.mkdir();fixture_root=ROOT/'tests/Contracts/Fixtures'
  for name in ['M2/Common','M2/Options','M2/Resolver','M2/Dtd','M1/Tree','M2/Entities','M2/Syntax','M1/Input','M2/DomMetadata']:
   text=(fixture_root/(name+'.cs.fixture')).read_text()
   if name=='M2/Options':text=text.replace('    public bool Recover { get; init; }\n','')
   (approved/(name.replace('/','-')+'.cs')).write_text(text)
  (approved/'Program.cs').write_text('Contract.Print();')
  (approved/'Approved.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><Compile Include="'+str(ROOT/'samples/M2-entities/Contract.cs')+'" /></ItemGroup></Project>')
  run('dotnet','build',approved/'Approved.csproj',*FLAGS);model=approved/'bin/Release/net10.0/Approved.dll';approved_output=run('dotnet',model);s['compiledApprovedContract']=approved_output.splitlines();assert actual==approved_output
  output=run('dotnet',sample);assert 'DOM:doctype=r;namespace=urn:r;default=owned tail;id=one;text=payloadtail' in output and 'streams=3;disposed=3' in output;s['sourceSampleOutputSummary']=output.splitlines()
  run('dotnet','pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','-o',raw/'feed');packages=list((raw/'feed').glob('*.nupkg'));assert len(packages)==1;s['packageSha256']=hashlib.sha256(packages[0].read_bytes()).hexdigest()
  run('dotnet','build','samples/M2-entities/Sample.csproj',*FLAGS,'-p:UsePackage=true','-p:ManagePackageVersionsCentrally=false',f'-p:RestoreSources={raw}/feed',f'-p:RestorePackagesPath={raw}/packages','-p:NuGetAudit=false')
  assert run('dotnet',sample)==output;assert run('dotnet',sample,'--contract')==actual;s['packageConsumer']='fresh nupkg output and compiled internal constructor/context/optional hook match'
  run('dotnet','build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*FLAGS);run('dotnet','build','docs/reports/M2-entities/benchmark-driver/Xemele.EntitiesBenchmarks.csproj',*FLAGS);run('dotnet','build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*FLAGS)
  standalone=ROOT/'docs/reports/M2-entities/benchmark-driver/bin/Release/net10.0/Xemele.EntitiesBenchmarks.dll';oracle=run('dotnet',standalone,'--oracle');s['benchmarkOracleSummary']=oracle.strip()
  for assembly in [standalone,ROOT/'benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll']:
   listing=run('dotnet',assembly,'--list','flat','--filter','*Entity*Benchmarks*');assert all(n in listing for n in ['EntityCompositionBenchmarks.InternalDocument','EntityCompositionBenchmarks.ExternalDocument','EntityLookupBenchmarks.GeneralLookup','EntityLookupBenchmarks.DefaultLookup'])
  s['benchmarkFixtures']='main/standalone build and discovery; actual timed methods full-output oracle passed; no timing/allocation mode'
  records=[json.loads(f.read_text()) for f in (raw/'case-results').glob('reference-*.json')];assert len(records)==242 and len({r['id'] for r in records})==242 and identities(pinned.parent)==reference_files
  assert all(r['runnerSha256']==runner_hash and r['response']['engine']['build']=='1.0.0+'+revision for r in records);assert sum(r['comparison']=='matched-rejection' for r in records)==239 and sum(r['comparison']=='documented-reference-divergence' for r in records)==3
  s['referenceProvenance']={'records':242,'matchedRejections':239,'documentedDivergences':3,'runnerSha256':runner_hash,'build':'1.0.0+'+revision,'outputIdentities':reference_files,'freshCompleteCopy':True,'unchangedCopy':True}
  s['referenceProvenance']['caseIdentitySha256']=hashlib.sha256(json.dumps(sorted(r['id'] for r in records),separators=(',',':')).encode()).hexdigest()
  run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v');validation=run('python3','-B','tools/planning/tasks.py','validate');assert 'Validated 124 tasks:' in validation;s['planningValidation']=validation.strip()
  paths=[]
  for cmd in [('git','diff','--name-only',a.audit_base+'..HEAD'),('git','diff','--name-only'),('git','diff','--cached','--name-only'),('git','ls-files','--others','--exclude-standard')]:paths+=run(*cmd).splitlines()
  owned=task['owns']+['docs/tasks/M2-entities.json'];assert all(any(path.startswith(o) if o.endswith('/') else path==o for o in owned) for path in paths),paths
  for suffix in [[a.audit_base+'..HEAD'],['--cached'],[]]:run('git','diff','--check',*suffix)
  after={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in tracked};assert after==before and run('git','rev-parse','HEAD').strip()==revision;global_after={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in global_sources};assert global_after==global_before;assert run('git','branch','--show-current').strip()==a.expected_branch;s['globalCompiledSourcesAfter']=global_after;s['sourceIdentitiesAfter']=after;s['ownedPaths']=sorted(set(paths));s['result']='passed';s['limitations']='macOS arm64 only; eight pinned simple document cases are not a full XML conformance oracle; timing/allocation modes held for exclusive grant.'
 except Exception as error:
  s.setdefault('failure',{'type':type(error).__name__,'message':str(error)[:1500]})
  raise
 finally:
  clean=finalize(s,commands,raw,child_root,prior_children,inherited_log,inherited_invoked,prior_log,report)
  if not clean and 'failure' not in s:raise AssertionError('Verification output cleanup failed; see summary.')
if __name__=='__main__':main()
