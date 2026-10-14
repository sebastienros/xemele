"""Qualified managed retained heap and instrumented native payload sidecars."""
import hashlib,json,os,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'artifacts/M9-xml-performance/memory-current';OUT.mkdir(parents=True,exist_ok=True)
HOST='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
MANIFEST=ROOT/'benchmarks/baselines/release/fixtures.json'
WORKER=ROOT/'docs/reports/M9-xml-performance/MemoryWorker/bin/Release/net10.0/XmlPerformanceMemory.dll'
NATIVE=ROOT/'artifacts/M9-xml-performance/libxml2-memory-worker'
RUST=ROOT/'artifacts/M9-xml-performance/rust-memory-build/release/xemele-release-reference'
env=os.environ.copy();env.update(XEMELE_PERF_ROOT=str(ROOT),DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0')
report={'rows':[],'engines':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [WORKER,NATIVE,RUST]}}
def run(cmd):
 p=subprocess.run([str(v) for v in cmd],env=env,capture_output=True,text=True,timeout=120)
 if p.returncode:raise RuntimeError(p.stderr+'\n'+p.stdout)
 return p.stdout
for f in json.loads(MANIFEST.read_text())['fixtures']:
 path=ROOT/'benchmarks/baselines/release'/f['path'];row={'fixture':f['id'],'engines':{}}
 for engine in ['xemele','bcl']:
  output=OUT/(f['id']+'-'+engine+'.json')
  log=run([HOST,'--fx-version','10.0.11',WORKER,engine,path,MANIFEST,output]);(OUT/(f['id']+'-'+engine+'.log')).write_text(log)
  row['engines'][engine]=json.loads(output.read_text())
  layout=run([HOST,'--fx-version','10.0.11',WORKER,'layout',f['id'],engine]);(OUT/(f['id']+'-'+engine+'-layout.log')).write_text(layout)
 for engine,command in [('libxml2',[NATIVE,path]),('xmloxide',[RUST,path,f['xpath'],'memory',1,OUT/f['id']])]:
  row['engines'][engine]=json.loads(run(command));assert row['engines'][engine]['payloadControlsPassed']
  assert all(r['releasedDelta']==0 for r in row['engines'][engine]['rows'])
 report['rows'].append(row);(OUT/'report.json').write_text(json.dumps(report,indent=2));print(f['id'],flush=True)
print('PASS all managed digests, heap controls and native payload controls')
