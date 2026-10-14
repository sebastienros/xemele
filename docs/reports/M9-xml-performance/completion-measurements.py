"""Final integrated ten-fixture DOM qualification and informational measurement."""
import hashlib,json,math,os,statistics,subprocess,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
OUT=ROOT/'artifacts/M9-xml-performance/completion';OUT.mkdir(parents=True,exist_ok=True)
HOST='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
RUNNER=HERE/'Runner/bin/Release/net10.0/XmlPerformanceRunner.dll'
WORKER=HERE/'MemoryWorker/bin/Release/net10.0/XmlPerformanceMemory.dll'
MANIFEST=ROOT/'benchmarks/baselines/release/fixtures.json'
env=dict(os.environ,DOTNET_ROOT=str(Path(HOST).parent),DOTNET_ROLL_FORWARD='Disable',DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',XEMELE_PERF_ROOT=str(ROOT))
report={'result':'incomplete','revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'productionSha256':hashlib.sha256((ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll').read_bytes()).hexdigest(),'fixtureManifestSha256':hashlib.sha256(MANIFEST.read_bytes()).hexdigest(),'environment':{k:env[k] for k in ['DOTNET_TieredCompilation','DOTNET_TieredPGO','DOTNET_ReadyToRun']},'commands':[],'rows':[],'timing':'informational shared-host; no dedicated release timing credit','fuzzExecuted':False}
def save(): (HERE/'completion-measurements.json').write_text(json.dumps(report,indent=2)+'\n')
def run(args,name):
 start=time.monotonic()
 with (OUT/(name+'.log')).open('w') as log:
  p=subprocess.run([HOST,'--fx-version','10.0.11',*map(str,args)],env=env,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=180)
 report['commands'].append({'name':name,'exit':p.returncode,'elapsedSeconds':round(time.monotonic()-start,3),'directChildReaped':True})
 save();assert p.returncode==0,name
if (HERE/'completion-measurements.json').exists():
 prior=json.loads((HERE/'completion-measurements.json').read_text());assert prior['productionSha256']==report['productionSha256'] and prior['fixtureManifestSha256']==report['fixtureManifestSha256'];report=prior
 report['observerCorrection']='XPath qualification-only MaxInputCharacters16MiB to documented128MiB; parser/memory-meter source unchanged; resume completed9fixtures.'
try:
 if not report['rows']:run([RUNNER,'quick',OUT/'quick.json'],'quick')
 timing=json.loads((OUT/'quick.json').read_text())
 for fixture,t in zip(json.loads(MANIFEST.read_text())['fixtures'],timing):
  assert fixture['id']==t['fixture']
  if any(r['fixture']==fixture['id'] for r in report['rows']):continue
  pair={}
  for engine in ['xemele','bcl']:
   path=MANIFEST.parent/fixture['path'];output=OUT/(fixture['id']+'-'+engine+'.json')
   run([WORKER,engine,path,MANIFEST,OUT/(fixture['id']+'-'+engine+'-qualification.json'),'qualify'],'qualify-'+fixture['id']+'-'+engine)
   run([WORKER,engine,path,MANIFEST,output],'memory-'+fixture['id']+'-'+engine)
   x=json.loads(output.read_text());assert len(x['rows'])==3 and len(x['controls'])==2
   assert all(abs(c['graph']-(c['bytes']+24))<=1024 for c in x['controls'])
   assert all(r['digest']==x['expectedSemanticSha256'] for r in x['rows'])
   pair[engine]={'retained':statistics.median(r['graphEstimate'] for r in x['rows']),'allocated':statistics.median(r['allocationBytes'] for r in x['rows']),'controls':x['controls'],'runtime':x['runtime'],'productionSha256':x['productionSha256']}
  current=statistics.median(r['us'] for r in t['rows']['xb']);bcl=statistics.median(r['us'] for r in t['rows']['bb'])
  row={'fixture':fixture['id'],'candidateUs':current,'bclUs':bcl,'latencyRatio':current/bcl,'allocationRatio':statistics.median(r['bytes'] for r in t['rows']['xb'])/statistics.median(r['bytes'] for r in t['rows']['bb']),'retainedRatio':pair['xemele']['retained']/pair['bcl']['retained'],'engines':pair}
  report['rows'].append(row);save();print('PASS',fixture['id'],row['latencyRatio'],row['allocationRatio'],row['retainedRatio'],flush=True)
 report['geomeanLatencyRatio']=math.exp(statistics.mean(math.log(r['latencyRatio']) for r in report['rows']))
 report['geomeanAllocationRatio']=math.exp(statistics.mean(math.log(r['allocationRatio']) for r in report['rows']))
 report['maximumLatencyRatio']=max(r['latencyRatio'] for r in report['rows']);report['maximumAllocationRatio']=max(r['allocationRatio'] for r in report['rows'])
 report['aggregateRetainedRatio']=sum(r['engines']['xemele']['retained'] for r in report['rows'])/sum(r['engines']['bcl']['retained'] for r in report['rows'])
 report['A2TargetPassed']=report['geomeanLatencyRatio']<=1.10 and report['geomeanAllocationRatio']<=1.10 and report['maximumLatencyRatio']<=1.25 and report['maximumAllocationRatio']<=1.25
 report['result']='passed-measurement';print(json.dumps({k:v for k,v in report.items() if k.startswith(('geomean','maximum','aggregate','A2'))}),flush=True)
finally:save()
