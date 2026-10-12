"""Inspect only after durable cleanup RELEASE; persist summaries, not raw outputs."""
import json, statistics, re, subprocess, hashlib
from collections import Counter
from decimal import Decimal, localcontext
from pathlib import Path
root=Path(__file__).resolve().parents[3]
raw=root/'artifacts/M8-facets/measurement-1'
life=json.loads((raw/'lifecycle.json').read_text())
assert life['status']==0 and life['beforeIdentities'] and life['afterIdentities'],life
assert all(r['exit']==0 and r['directChildReaped'] and r['ownedGroupAbsent'] and r['logClosed'] for r in life['sequence'])
result=json.loads((raw/'allocations.json').read_text())
assert result['status']=='passed' and result['runtime']=='.NET 10.0.11' and len(result['rows'])==17
rows=[]
for row in result['rows']:
 values=row['samples'];assert len(values)==64
 assert all(v==0 for v in values) if row['route']<11 else all(v>0 for v in values)
 rows.append({'route':row['route'],'name':row['name'],'count':len(values),'min':min(values),'max':max(values),'median':statistics.median(values),'totalBytes':sum(values),'valueFrequencies':dict(sorted(Counter(values).items())),'firstBytes':row['firstBytes']})
reports=list((raw/'bdn/results').glob('*-report-github.md'))
assert len(reports)==1,reports
# Tables contain aggregate statistics only; raw iterations remain transient.
table=reports[0].read_text();cases=[]
for line in table.splitlines():
 if not line.startswith('|') or not any(name in line for name in ['BaselineLoop','BorrowedPreparedPattern']):continue
 cells=[c.strip().replace('**','') for c in line.strip('|').split('|')]
 cases.append({'method':cells[0],'case':cells[1],'mean':cells[2],'error':cells[3],'standardDeviation':cells[4],'observedRatio':cells[5],'ratioStandardDeviation':cells[6],'reportedAllocated':cells[7]})
assert len(cases)==6,cases
current=None;gc=[]
for line in (raw/'command-1.log').read_text().splitlines():
 if line.startswith('// Benchmark: '):
  match=re.search(r'FacetBenchmarks\.(\w+):.*\[Case=(\w+)\]',line);assert match,line
  current={'method':match[1],'case':match[2]}
 elif line.startswith('// GC:'):
  counts=list(map(int,line.removeprefix('// GC:').split()));assert len(counts)==5
  gen0,gen1,gen2,total,operations=counts;assert operations>0
  whole,residual=divmod(total,operations)
  gc.append({**current,'gen0Collections':gen0,'gen1Collections':gen1,'gen2Collections':gen2,'totalAllocatedBytes':total,'operations':operations,'bytesPerOperationIntegerPart':whole,'residualBytes':residual})
assert len(gc)==6
with localcontext() as context:
 context.prec=50
 for item in gc:
  item['allocatedBytesPerOperationFraction']=f"{item['totalAllocatedBytes']}/{item['operations']}"
  item['allocatedBytesPerOperationExact']=str(Decimal(item['totalAllocatedBytes'])/Decimal(item['operations']))
manifest_path=root/'docs/reports/M8-facets/preparation.json'
manifest=json.loads(manifest_path.read_text())
summary={k:v for k,v in result.items() if k!='rows'}
summary.update(rows=rows,benchmarkCases=cases,benchmarkGc=gc,warmCallsPerRoute=32,measuredCallsPerRoute=64,benchmarkLaunches=1,benchmarkWarmupIterations=3,benchmarkMeasuredIterations=3)
summary['preparedManifestSha256']=hashlib.sha256(manifest_path.read_bytes()).hexdigest()
summary['measurementLease']={'id':'M8-report-content-facets-serial-1','grantRevision':subprocess.check_output(['git','rev-parse','b9e9f7e9'],cwd=root,text=True).strip(),'releaseRevision':subprocess.check_output(['git','rev-parse','cf39961a'],cwd=root,text=True).strip(),'releaseBeforeInspection':True,'beforeIdentities':life['beforeIdentities'],'afterIdentities':life['afterIdentities'],'identityCount':life['identityCount'],'elapsedSeconds':life['elapsedSeconds'],'latestStart':life['latestStart'],'absoluteExpiry':life['absoluteExpiry'],'childExitCodes':[r['exit'] for r in life['sequence']],'allChildrenReapedGroupsAbsentLogsClosed':True}
summary['controlIdentities']={k:v for k,v in manifest['identities'].items() if k.startswith('benchmarks/Xemele.Benchmarks/M8-facets/') and (k.endswith(('.cs','.fixture')) or k.endswith('Xemele.Benchmarks.dll'))}
(root/'docs/reports/M8-facets/measurements.json').write_text(json.dumps(summary,indent=2)+'\n')
print('ASSERTED17routes/1088sample distribution observations;6cases/exactGCfractions; summaries only, raw outputs remain transient for root cleanup.')
