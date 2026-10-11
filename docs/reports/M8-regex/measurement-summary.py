"""Summarize existing released measurements; never launches workloads."""
from pathlib import Path
import json,hashlib
manifest=json.loads(Path('docs/reports/M8-regex/preparation.json').read_text())
for p,h in manifest['files'].items():assert hashlib.sha256(Path(p).read_bytes()).hexdigest()==h,p
probes=[]
for number in (1,2):
 p=Path(f'artifacts/M8-regex/allocation{number}.json');a=json.loads(p.read_text())
 assert a['mode']=='--measure' and a['runtime']=='.NET 10.0.11' and a['sourceRevision']==manifest['sourceRevision']
 assert len(a['rows'])==8 and all(len(r['distribution'])==64 and all(v==0 for v in r['distribution']) for r in a['rows'])
 assert len(a['owned'])==64 and min(a['owned'])==max(a['owned'])==456 and a['positiveBytes']>=1024
 for name,key in [('Xemele.dll','assemblySha256'),('Xemele.Benchmarks.dll','probeSha256')]:
  assert a[key].lower()==manifest['files'][str(Path('benchmarks/Xemele.Benchmarks/M8-regex/bin/Release/net10.0',name).resolve())]
 probes.append({'number':number,'rawSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'warmRoutes':8,'warmSamplesPerRoute':64,'warmBytesPerSampleMin':0,'warmBytesPerSampleMax':0,'ownedSamples':64,'ownedRunBytesPerOperation':456,'coldSetupBytes':a['coldSetupBytes'],'coldMatchBytesByRoute':[r['coldMatchBytes'] for r in a['rows']],'positiveControlBytes':a['positiveBytes'],'retainedPrograms':256,'retainedInclusiveDeltaBytes':a['retainedAfter']-a['retainedBefore'],'sourceRevision':a['sourceRevision'],'assemblySha256':a['assemblySha256'],'probeSha256':a['probeSha256'],'inputSha256':a['inputSha256'],'invalidScalar':a['invalidScalar']})
p=next(Path('artifacts/M8-regex/bdn/results').glob('*full.json'));j=json.loads(p.read_text());assert len(j['Benchmarks'])==8
host=j['HostEnvironmentInfo'];assert host['RuntimeVersion'].startswith('.NET 10.0.11') and host['Architecture']=='Arm64' and host['DotNetCliVersion']=='10.0.400'
rows=[]
for b in j['Benchmarks']:
 assert b['Method']=='MatchBorrowed' and b['Statistics']['N']==3
 rows.append({'case':b['Parameters'],'meanNs':b['Statistics']['Mean'],'standardDeviationNs':b['Statistics']['StandardDeviation'],'measuredIterations':3,'allocatedBytesPerOperation':b['Memory']['BytesAllocatedPerOperation'],'memoryDiagnosticOperations':b['Memory']['TotalOperations']})
assert sum(r['allocatedBytesPerOperation']==0 for r in rows)==7
summary={'sourceRevision':manifest['sourceRevision'],'measurementSupervisorRevision':'ea9974285b08d0e9154a8d4a7d71f4b4cf016dda','host':{k:host[k] for k in ['BenchmarkDotNetVersion','OsVersion','ProcessorName','RuntimeVersion','Architecture','Configuration','DotNetCliVersion']},'serverGc':False,'preparedIdentityCount':len(manifest['files']),'preparationManifestSha256':hashlib.sha256(Path('docs/reports/M8-regex/preparation.json').read_bytes()).hexdigest(),'probes':probes,'bdnRawSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bdnToolchain':'InProcessNoEmitToolchain','bdnJob':'ShortRun,1launch,3warmup,3measure,MemoryDiagnoser','bdnCases':rows,'lifecycle':['Lease1: firstprobe valid; supervisor ps denied; root cleanup inventory verified and durable ACK a6181ba preceded inspection. No rerun of successfulfirstprobe.','Lease2: remaining-only supervisor under require_escalated; secondprobe and8NoEmitBDN exited0, reaped/groups drained, logsclosed and42finalidentitiespassed; durableRELEASEACK preceded inspection.'],'limitations':['Pooled BDN case reports 1 B/op over 4,096 memory-diagnostic operations, while both directly warmed 64-sample probes report 0 B/op; attribution is unestablished and no unqualified pooled BDN zero allocation claim is made.','Inclusive retained heap deltas are 153,008 and 161,224 bytes for 256 programs, including retaining-array/runtime bookkeeping; they are not exact object-size or portable memory contracts.','Single Arm64 engine microbenchmarks are provisional. No throughput optimization/speedup or x64/cumulative release claim; facade composition and cross-architecture release confirmation belong to gate.']}
Path('docs/reports/M8-regex/measurements.json').write_text(json.dumps(summary,indent=2)+'\n')
print('Released measurements verified/summarized:2probes,1024zero-allocation warmed samples,128owned samples,8successfulBDNcases,42stableidentities. PooledBDN1B explicitly preserved.')
