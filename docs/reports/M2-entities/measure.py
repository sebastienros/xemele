"""Requires an explicit bounded grant. No retries; declared class jobs; concise observations and generated-child provenance only."""
import argparse,csv,hashlib,json,math,os,platform,re,shlex,shutil,subprocess,signal,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def bounded_wait(process, logfile, summary, total_seconds=1800, child_seconds=180):
 start=time.monotonic();child_start=None;read=0;carry=""
 summary['deadlinesSeconds']={'total':total_seconds,'build':240,'child':child_seconds}
 try:
  while process.poll() is None:
   with logfile.open() as stream:
    stream.seek(read);new=stream.read();read=stream.tell()
   carry+=new;lines=carry.split('\n');carry=lines.pop()
   for line in lines:
    if line.startswith('// Execute:'):child_start=time.monotonic()
    if re.match(r'// Benchmark Process \d+ has exited',line):child_start=None
   elapsed=time.monotonic()-start
   if elapsed>total_seconds or child_start is not None and time.monotonic()-child_start>child_seconds:
    summary['timeout']={'stage':'benchmark-child' if child_start is not None else 'total','elapsedSeconds':elapsed};raise TimeoutError('Bounded measurement deadline exceeded.')
   time.sleep(0.2)
  return process.wait(timeout=5)
 except BaseException:
  termination_errors=[]
  try:os.killpg(process.pid,signal.SIGTERM)
  except ProcessLookupError:pass
  except Exception as error:termination_errors.append(str(error))
  try:process.wait(timeout=5)
  except subprocess.TimeoutExpired:pass
  except Exception as error:termination_errors.append(str(error))
  # A reaped parent does not prove descendants obeyed TERM. Always kill the
  # remaining group, then verify disappearance before claiming termination.
  try:os.killpg(process.pid,signal.SIGKILL)
  except ProcessLookupError:pass
  except Exception as error:termination_errors.append(str(error))
  try:process.wait(timeout=5)
  except Exception as error:termination_errors.append(str(error))
  empty=False
  for _ in range(100):
   try:os.killpg(process.pid,0)
   except ProcessLookupError:empty=True;break
   except Exception as error:termination_errors.append(str(error));break
   time.sleep(0.05)
  summary['terminatedProcessGroup']=empty
  if not empty:termination_errors.append('Process group still exists after KILL deadline.')
  if termination_errors:summary['terminationErrors']=termination_errors
  raise


def selected_observations(output):
 blocks=re.split(r'^// Benchmark: ',output,flags=re.MULTILINE)[1:]
 observations=[];scales={'ns':1,'us':1000,'μs':1000,'ms':1000000,'s':1000000000}
 for block in blocks:
  title=block.splitlines()[0];found=re.match(r'(Entity\w+Benchmarks)\.(\w+):',title);length=re.search(r'Length=(\d+)',title)
  numeric={stage:[] for stage in ['Actual','Result']}
  for stage in numeric:
   pattern=r'^Workload'+stage+r'\s+(\d+):\s+(\d+) op,\s+([\d.eE+-]+)\s+(ns|us|μs|ms|s),\s+([\d.eE+-]+)\s+(ns|us|μs|ms|s)/op'
   for match in re.finditer(pattern,block,re.MULTILINE):
    numeric[stage].append({'iteration':int(match[1]),'operations':int(match[2]),'durationNs':float(match[3])*scales[match[4]],'nsPerOperation':float(match[5])*scales[match[6]]})
  gc=[]
  for match in re.finditer(r'^// GC:\s+(\d+) (\d+) (\d+) (\d+) (\d+)',block,re.MULTILINE):
   operations=int(match[5]);gc.append({'gen0':int(match[1]),'gen1':int(match[2]),'gen2':int(match[3]),'allocatedBytes':int(match[4]),'operations':operations,'bytesPerOperation':int(match[4])/operations if operations else None})
  observations.append({'case':(found[1],found[2],length[1] if length else '') if found else None,
   'actual':numeric['Actual'],'result':numeric['Result'],'gc':gc,
   'childExits':re.findall(r'^// Benchmark Process (\d+) has exited with code (\d+)\.',block,re.MULTILINE),
   'environment':[line for line in block.splitlines() if line.startswith(('// Runtime=','// GC=','// HardwareIntrinsics='))]})
 return observations


def main():
 p=argparse.ArgumentParser();p.add_argument('--exclusive-grant',required=True);p.add_argument('--report-prefix',required=True);p.add_argument('--mode',choices=['allocations','benchmarks'],required=True);a=p.parse_args();assert re.fullmatch('[a-z0-9-]+',a.report_prefix)
 report=ROOT/'docs/reports/M2-entities'/a.report_prefix;report.mkdir();raw=ROOT/'artifacts/M2-entities'/a.report_prefix;raw.mkdir(parents=True)
 s={'result':'failed','exclusiveGrant':a.exclusive_grant,'mode':a.mode};generated=set();parent=ROOT/'docs/reports/M2-entities/benchmark-driver/bin/Release/net10.0';prior_dirs=set(parent.iterdir()) if parent.exists() else set();primary=None
 def hashes(names):return {n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in names}
 try:
  dotnet=Path(shutil.which('dotnet')).resolve(strict=True);revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
  known=json.loads((ROOT/'docs/reports/M2-entities/premeasurement.json').read_text());assert known['head']==revision
  source=hashes(known['compiledSources']);assert source==known['compiledSources'];binary=hashes(known['binaries']);assert binary==known['binaries']
  sdk=subprocess.check_output([str(dotnet),'--version'],cwd=ROOT,text=True).strip();assert sdk=='10.0.400'
  probe=ROOT/'docs/reports/M2-entities/provenance-probe/bin/Release/net10.0/EntityProvenanceProbe.dll'
  parent_metadata=json.loads(subprocess.check_output([str(dotnet),str(probe),str(parent/'Xemele.EntitiesBenchmarks.dll'),str(ROOT/'samples/M2-entities/bin/Release/net10.0/Xemele.SyntaxSample.dll')],cwd=ROOT,text=True))
  assert len(parent_metadata)==2 and all(m['runtime']=='10.0.11' and m['architecture']=='Arm64' and m['sourceVersion']=='1.0.0+'+revision for m in parent_metadata)
  s['sdk']=sdk;s['hostExecutableSha256']=hashlib.sha256(dotnet.read_bytes()).hexdigest();s['runtime']='10.0.11';s['processArchitecture']='Arm64';s['parentAssemblyProvenance']=parent_metadata
  s.update(head=revision,compiledSourceRevision=known['correctnessSourceRevision'],host=platform.platform(),architecture=platform.machine(),sourceIdentitiesBefore=source,binaryIdentitiesBefore=binary)
  s['runtimeOverrides']={k:v for k,v in os.environ.items() if k.startswith(('DOTNET_','COMPlus_'))};assert not any(k.startswith('COMPlus_') or any(x in k.upper() for x in ['TIERED','GC','READYTORUN']) for k in s['runtimeOverrides'])
  if a.mode=='allocations':command=[str(dotnet),str(ROOT/'samples/M2-entities/bin/Release/net10.0/Xemele.SyntaxSample.dll'),'--allocations']
  else:command=[str(dotnet),str(parent/'Xemele.EntitiesBenchmarks.dll'),'--filter','*Entity*Benchmarks*','--artifacts',str(raw/'bdn'),'--keepFiles','--buildTimeout','240','--stopOnFirstError']
  s['command']=shlex.join(command);print(s['command'],flush=True)
  with (raw/'output.log').open('w') as output:
   process=subprocess.Popen(command,cwd=ROOT,env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0'),stdout=output,stderr=subprocess.STDOUT,start_new_session=True);s['processId']=process.pid;status=bounded_wait(process,raw/'output.log',s)
  s['exit']=status;output=(raw/'output.log').read_text();s['selectedObservations']=selected_observations(output);s['buildExits']=re.findall(r'^// command took .* and exited with (\d+)',output,re.MULTILINE);print(output[-7000:],flush=True);assert status==0
  if a.mode=='allocations':
   found=re.search(r'iterations=(\d+);ordinaryWarm=(\d+);entityWarm=(\d+);borrowedLookup=(\d+);coldCore=(\d+);coldContext=(\d+);escapedPositive=(\d+)',output);assert found
   values=dict(zip(['iterations','ordinaryWarm','entityWarm','borrowedLookup','coldCore','coldContext','escapedPositive'],map(int,found.groups())));assert values['iterations']==10000 and values['ordinaryWarm']==values['entityWarm']==values['borrowedLookup']==0 and values['escapedPositive']>0
   assert 'retention=callerCharBufferReleased;ownedDtdAndDomPreserved;oneCollectionOutsideIntervals' in output;s['allocationBytes']=values;s['retention']='Exact owned DOM/DocumentType/subset/Dtd preserved; caller buffer released after one outside-interval collection.'
  else:
   rows=[];expected={('EntityCompositionBenchmarks',m,str(n)) for m in ['InternalDocument','ExternalDocument'] for n in [16,4096]}|{('EntityLookupBenchmarks',m,'') for m in ['GeneralLookup','DefaultLookup']}
   for path in (raw/'bdn').rglob('*-report.csv'):
    with path.open() as data:
     for row in csv.DictReader(data):
      assert row.get('Mean') and row['Mean']!='NA' and row.get('Allocated') and row['Allocated']!='NA'
      rows.append({'class':path.name.replace('-report.csv','').split('.')[-1],'method':row['Method'],'length':row.get('Length',''),'mean':row['Mean'],'error':row.get('Error'),'stddev':row.get('StdDev'),'allocated':row['Allocated'],'gen0':row.get('Gen0')})
   assert len(rows)==6 and {(r['class'],r['method'],r['length']) for r in rows}==expected
   blocks=re.split(r'^// Benchmark: ',output,flags=re.MULTILINE)[1:];assert len(blocks)==6
   observations=[];child_provenance=[]
   probe=ROOT/'docs/reports/M2-entities/provenance-probe/bin/Release/net10.0/EntityProvenanceProbe.dll'
   for block in blocks:
    title=block.splitlines()[0];found=re.match(r'(Entity\w+Benchmarks)\.(\w+):',title);assert found
    length=re.search(r'Length=(\d+)',title);identity=(found[1],found[2],length[1] if length else '');assert identity in expected
    actual=len(re.findall(r'^WorkloadActual\s+\d+:',block,re.MULTILINE));result=len(re.findall(r'^WorkloadResult\s+\d+:',block,re.MULTILINE));gc=len(re.findall(r'^// GC:',block,re.MULTILINE));exits=re.findall(r'^// Benchmark Process (\d+) has exited with code (\d+)\.',block,re.MULTILINE)
    s['observations']=selected_observations(output)
    row=s['observations'][len(observations)]
    assert len(row['actual'])==len(row['result'])==3 and len(row['gc'])==1
    for measurement in row['actual']+row['result']:
     assert measurement['operations']>0 and math.isfinite(measurement['durationNs']) and measurement['durationNs']>0 and math.isfinite(measurement['nsPerOperation']) and measurement['nsPerOperation']>0
    assert row['gc'][0]['operations']>0 and row['gc'][0]['allocatedBytes']>=0 and math.isfinite(row['gc'][0]['bytesPerOperation']) and all(m['operations']==row['gc'][0]['operations'] for m in row['actual']+row['result'])
    assert actual==result==3 and gc==1 and len(exits)==1 and exits[0][1]=='0';assert '.NET 10.0.11' in block and re.search('Arm64',block,re.IGNORECASE)
    execution=re.search(r'^// Execute: (.+) in (.+)$',block,re.MULTILINE);assert execution;argv=shlex.split(execution[1]);dll=next(v for v in argv if v.endswith('.dll'));cwd=Path(execution[2].strip());exe=cwd/dll;library=cwd/'Xemele.EntitiesBenchmarks.dll';assert exe.is_file() and library.is_file()
    assert hashlib.sha256(library.read_bytes()).hexdigest()==binary[str((parent/library.name).relative_to(ROOT))]
    metadata=json.loads(subprocess.check_output([str(dotnet),str(probe),str(exe),str(library)],cwd=ROOT,text=True));assert len(metadata)==2 and metadata[1]['sourceVersion']=='1.0.0+'+revision and all(x['runtime']=='10.0.11' and x['architecture']=='Arm64' for x in metadata)
    observations.append(row);child_provenance.append({'case':identity,'executableSha256':metadata[0]['sha256'],'librarySha256':metadata[1]['sha256'],'librarySourceVersion':metadata[1]['sourceVersion'],'runtime':metadata[1]['runtime'],'coreLibrarySha256':metadata[1]['coreLibrarySha256'],'childEnvironment':[line for line in block.splitlines() if line.startswith(('// Runtime=','// GC=','// HardwareIntrinsics='))]})
   assert {tuple(o['case']) for o in observations}==expected and re.search(r'Global total time:.*executed benchmarks: 6',output)
   assert s['buildExits'] and all(code=='0' for code in s['buildExits'])
   s['benchmarks']=rows;s['observations']=observations;s['generatedChildProvenance']=child_provenance;s['environmentSummary']=[line.strip() for line in output.splitlines() if line.startswith('BenchmarkDotNet v') or '.NET SDK' in line or 'Arm64 RyuJIT' in line or 'Apple M' in line];s['timingLimit']='Informational macOS arm64; no cross-architecture release or speedup claim.'
  s['sourceIdentitiesAfter']=hashes(source);s['binaryIdentitiesAfter']=hashes(binary);assert s['sourceIdentitiesAfter']==source and s['binaryIdentitiesAfter']==binary;assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()==revision;s['result']='passed'
 except Exception as error:
  primary=error;s['failure']={'type':type(error).__name__,'message':str(error)[:1000]}
  if (raw/'output.log').exists():
   partial=(raw/'output.log').read_text();s['selectedObservations']=selected_observations(partial);s['buildExits']=re.findall(r'^// command took .* and exited with (\d+)',partial,re.MULTILINE);s['failure']['outputTail']=partial[-2500:]
  raise
 finally:
  errors=[]
  try:generated={d for d in set(parent.iterdir())-prior_dirs if d.is_dir()} if parent.exists() else set()
  except Exception as error:errors.append(str(error))
  for directory in [raw,*generated]:
   try:
    if directory.exists():shutil.rmtree(directory)
   except Exception as error:errors.append(str(error))
  s['temporaryOutputsRemoved']=not raw.exists() and all(not d.exists() for d in generated)
  if errors:s['cleanupErrors']=errors;s['result']='failed'
  (report/'summary.json').write_text(json.dumps(s,indent=2)+'\n')
  if errors and primary is None:raise RuntimeError('Measurement cleanup failed; see summary.')
if __name__=='__main__':main()
