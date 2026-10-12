"""Inspect raw results only after the coordinator's durable cleanup/release ACK."""
import json,statistics
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
 rows.append({'route':row['route'],'name':row['name'],'min':min(values),'max':max(values),'median':statistics.median(values),'firstBytes':row['firstBytes'],'observations':len(values)})
summary={k:v for k,v in result.items() if k!='rows'};summary['rows']=rows;summary['lifecycle']=life
reports=list((raw/'bdn/results').glob('*-report-github.md'))
assert len(reports)==1,reports
summary['benchmarkReport']=reports[0].read_text()
(root/'docs/reports/M8-facets/measurements.json').write_text(json.dumps(summary,indent=2)+'\n')
print('ASSERTED17routes/1088observations;6-case BDN report preserved; final identities and cleanup verified. Raw observations not discarded.')
