"""Validate/package unchanged raw timing offline; never invokes .NET or measurement."""
import csv, hashlib, json, re, zipfile
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
r = ROOT/'docs/reports/M2-dtd-composition/exclusive-719c630'
s = json.loads((r/'measurement.json').read_text()); text=(r/'benchmark.txt').read_text()
assert s['parentExit']==0 and s['binaryUnchanged'] and s['allocationNotRerun']
assert 'Global total time:' in text and re.search(r'Global total time:.*executed benchmarks: 6',text)
assert 'There are not any results runs' not in text and 'Generate Exception' not in text
build=list(map(int,re.findall(r'// command took .*?exited with (-?\d+)',text)))
children=list(map(int,re.findall(r'// Benchmark Process \d+ has exited with code (-?\d+)',text)))
assert build==[0,0] and children==[0]*6
cases=re.findall(r'^// Benchmark: (\w+)\.(\w+): .*\[CommentScalars=(\d+)\]$',text,re.MULTILINE)
gc=[list(map(int,m)) for m in re.findall(r'^// GC:\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$',text,re.MULTILINE)]
assert len(cases)==6 and len(gc)==6
expected={(c,m,str(n)) for c,m in [('DtdCompositionBenchmarks','SeedAndFramedComposition'),('DtdCompositionBenchmarks','SeedAndModelSnapshot'),('DtdFramedScanBenchmarks','BorrowedCommentScan')] for n in [32,4096]}
assert set(cases)==expected
records=[]
for case,counts in zip(cases,gc):
 assert counts[-1]>0
 if case[1]=='BorrowedCommentScan': assert counts[:4]==[0,0,0,0]
 records.append({'class':case[0],'method':case[1],'commentScalars':int(case[2]),'gcRaw':counts,'childExit':0})
rows=[]
for p in sorted((r/'raw/results').glob('*report.csv')):
 rows.extend(csv.DictReader(p.open(newline='')))
assert len(rows)==6
assert {(row['Method'],row['CommentScalars']) for row in rows}=={(m,n) for c,m,n in expected}
for row in rows:
 assert row['Mean'] not in ['NA','?','']
 assert row['Allocated']=='0 B' if row['Method']=='BorrowedCommentScan' else row['Allocated'] not in ['NA','?','0 B','']
archive=r/'full-raw.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 z.write(r/'benchmark.txt','benchmark.txt')
 for f in sorted((r/'raw').rglob('*')):
  if f.is_file(): z.write(f,str(f.relative_to(r)))
s.update(result='passed',executedBenchmarks=6,generatedRestoreExit=build[0],generatedBuildExit=build[1],childExitCodes=children,rawGcRecords=records,
         resultRows=[{k:row[k] for k in ['Method','CommentScalars','Mean','Error','StdDev','Allocated']} for row in rows],
         rawArchiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
         allocationEvidence='../exclusive-6f65008/measurement.json',allocationPreservedNotRerun=True,
         scope='Arm64 common-only owned seed/model/framed composition and borrowed complete-comment scanning;20correctnesspairs=40ownedoutputs+2048scans outside timing. Coldprobeincludes setup+twooutputs+bothoracles, no subtraction. No full XML/core/resolver conformance, crossarchitecture or AOT claim.')
(r/'measurement.json').write_text(json.dumps(s,indent=2)+'\n'); print(json.dumps({'result':s['result'],'rawArchiveSha256':s['rawArchiveSha256'],'build':build,'children':children,'cases':6},indent=2))
