"""Only run after explicit bounded coordinator grant. No builds/tests/retries outside BDN's own child generation."""
import argparse,csv,hashlib,json,os,platform,re,shlex,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
DOTNET=Path(shutil.which('dotnet')).resolve(strict=True)
def main():
 p=argparse.ArgumentParser();p.add_argument('--exclusive-grant',required=True);p.add_argument('--report-prefix',required=True);p.add_argument('--mode',choices=['allocations','benchmarks'],required=True);a=p.parse_args();assert re.fullmatch('[a-z0-9-]+',a.report_prefix)
 report=ROOT/'docs/reports/M2-entities'/a.report_prefix;report.mkdir();raw=ROOT/'artifacts/M2-entities'/a.report_prefix;raw.mkdir(parents=True)
 revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
 known=json.loads((ROOT/'docs/reports/M2-entities/premeasurement.json').read_text());assert known['head']==revision
 source={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in known['compiledSources']};assert source==known['compiledSources']
 binary={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in known['binaries']};assert binary==known['binaries']
 s={'result':'failed','head':revision,'compiledSourceRevision':known['correctnessSourceRevision'],'exclusiveGrant':a.exclusive_grant,'mode':a.mode,'host':platform.platform(),'architecture':platform.machine(),'sourceIdentitiesBefore':source,'binaryIdentitiesBefore':binary}
 try:
  if a.mode=='allocations':command=[str(DOTNET),str(ROOT/'samples/M2-entities/bin/Release/net10.0/Xemele.SyntaxSample.dll'),'--allocations']
  else:command=[str(DOTNET),str(ROOT/'docs/reports/M2-entities/benchmark-driver/bin/Release/net10.0/Xemele.EntitiesBenchmarks.dll'),'--filter','*Entity*Benchmarks*','--artifacts',str(raw/'bdn')]
  s['command']=shlex.join(command);print(s['command'],flush=True)
  with (raw/'output.log').open('w') as output:
   process=subprocess.Popen(command,cwd=ROOT,env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0'),stdout=output,stderr=subprocess.STDOUT)
   s['processId']=process.pid;status=process.wait()
  s['exit']=status;output=(raw/'output.log').read_text();print(output[-7000:],flush=True)
  assert status==0
  if a.mode=='allocations':
   found=re.search(r'iterations=(\d+);ordinaryWarm=(\d+);entityWarm=(\d+);borrowedLookup=(\d+);coldCore=(\d+);coldContext=(\d+);escapedPositive=(\d+)',output);assert found
   values=dict(zip(['iterations','ordinaryWarm','entityWarm','borrowedLookup','coldCore','coldContext','escapedPositive'],map(int,found.groups())));assert values['iterations']==10000 and values['ordinaryWarm']==values['entityWarm']==values['borrowedLookup']==0 and values['escapedPositive']>0
   assert 'retention=callerCharBufferReleased;ownedDtdAndDomPreserved;oneCollectionOutsideIntervals' in output;s['allocationBytes']=values;s['retention']='Caller buffer released; exact owned DOM/DocumentType/internal subset/Dtd replacement preserved after one controlled outside-interval collection.'
  else:
   rows=[]
   for path in (raw/'bdn').rglob('*-report.csv'):
    with path.open() as data:
     for row in csv.DictReader(data):
      assert row.get('Mean') and row['Mean']!='NA' and row.get('Allocated') and row['Allocated']!='NA'
      rows.append({'class':path.name.replace('-report.csv',''),'method':row['Method'],'length':row.get('Length'),'mean':row['Mean'],'error':row.get('Error'),'stddev':row.get('StdDev'),'allocated':row['Allocated'],'gen0':row.get('Gen0')})
   assert len(rows)==6;s['benchmarks']=rows;s['environmentSummary']=[line.strip() for line in output.splitlines() if line.startswith('BenchmarkDotNet v') or '.NET SDK' in line or 'Arm64 RyuJIT' in line or 'Apple M' in line];s['timingLimit']='Informational single macOS arm64 host; no cross-architecture release or speedup claim.'
  s['sourceIdentitiesAfter']={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in source};s['binaryIdentitiesAfter']={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in binary};assert s['sourceIdentitiesAfter']==source and s['binaryIdentitiesAfter']==binary
  assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()==revision;s['result']='passed'
 except Exception as error:
  s['failure']={'type':type(error).__name__,'message':str(error)[:1000]}
  if (raw/'output.log').exists():s['failure']['outputTail']=(raw/'output.log').read_text()[-2500:]
  raise
 finally:
  shutil.rmtree(raw);s['temporaryOutputsRemoved']=not raw.exists();(report/'summary.json').write_text(json.dumps(s,indent=2)+'\n')
if __name__=='__main__':main()
