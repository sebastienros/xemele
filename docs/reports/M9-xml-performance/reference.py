"""Pinned direct native/Rust semantic qualifications and informational parse/lifecycle timings."""
import argparse,hashlib,json,statistics,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
p=argparse.ArgumentParser();p.add_argument('--phase',default='baseline');a=p.parse_args()
out=ROOT/'artifacts/M9-xml-performance'/('references-'+a.phase);out.mkdir(parents=True,exist_ok=True)
manifest=json.loads((ROOT/'benchmarks/baselines/release/fixtures.json').read_text())
engines={'libxml2':ROOT/'artifacts/M9-xml-performance/libxml2-worker','xmloxide':ROOT/'docs/reports/M9-xml-performance/RustWorker/target/release/xemele-release-reference'}
report={'phase':a.phase,'engines':{k:{'binary':str(v),'sha256':hashlib.sha256(v.read_bytes()).hexdigest()} for k,v in engines.items()},'rows':[],'qualified':0}
def run(engine,fixture,op,n):
 prefix=out/(fixture['id']+'-'+engine+'-'+op)
 proc=subprocess.run([str(engines[engine]),str(ROOT/'benchmarks/baselines/release'/fixture['path']),fixture['xpath'],op,str(n),str(prefix)],capture_output=True,text=True,timeout=120)
 if proc.returncode:raise RuntimeError(proc.stderr)
 result=json.loads(proc.stdout);assert result['consumed']>0
 if op=='qualify':assert hashlib.sha256(prefix.with_suffix('.events').read_bytes()).hexdigest()==fixture['semantic']['sha256'];report['qualified']+=1
 return result
for f in manifest['fixtures']:
 row={'fixture':f['id'],'inputBytes':f['bytes'],'engines':{}}
 for engine in engines:
  run(engine,f,'qualify',1)
  pilot=run(engine,f,'parse',1);ns=max(1,pilot['steadyNanoseconds']);n=max(1,min(100000,int(200000000/ns)))
  measurements={op:[] for op in ['parse','lifecycle']}
  for sample_round in range(7):
   for op in ['parse','lifecycle'] if sample_round%2==0 else ['lifecycle','parse']:
    result=run(engine,f,op,n);measurements[op].append({'round':sample_round,**result,'nsPerOp':result['steadyNanoseconds']/n})
  row['engines'][engine]={'iterations':n,'parseNs':statistics.median(x['nsPerOp'] for x in measurements['parse']),'lifecycleNs':statistics.median(x['nsPerOp'] for x in measurements['lifecycle']),'samples':measurements}
 report['rows'].append(row);print(f['id'],{e:round(v['parseNs']/1000,2) for e,v in row['engines'].items()},flush=True)
 (out/'report.json').write_text(json.dumps(report,indent=2))
print('PASS',report['qualified'],'semantic reference qualifications')
