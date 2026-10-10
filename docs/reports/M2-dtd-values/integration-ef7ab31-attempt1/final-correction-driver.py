import subprocess,json,gzip,hashlib,zipfile,xml.etree.ElementTree as E
from pathlib import Path
w=Path('/private/tmp/xemele-integrate-m2-dtd-values');p=w/'docs/reports/M2-dtd-values/integration-ef7ab31-attempt1'
def run(*args):
 r=subprocess.run(args,cwd=w,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT);assert r.returncode==0,r.stdout;return r.stdout
revision=run('git','rev-parse','HEAD').strip()
s=json.loads((p/'summary.json').read_text());assert s['result']=='failed' and s['sourceRevision']=='ef7ab314867fec4e33005043ffdc7aae6125e907'
source=['src/Xemele/Parsing/Dtd/Grammar/Values','tests/Xemele.Tests/M2-dtd-values','benchmarks/Xemele.Benchmarks/M2-dtd-values','samples/M2-dtd-values','docs/reports/M2-dtd-values/verify.py','docs/reports/M2-dtd-values/BenchmarkDriver.cs','docs/reports/M2-dtd-values/Xemele.DtdValuesBenchmarks.csproj']
assert not run('git','diff','--name-only',s['sourceRevision'],'HEAD','--',*source).strip()
assert s['mtpTests']=={'total':3097,'failed':0,'skipped':0}
for name,n in [('focused',994),('Xemele.Tests',2543),('Xemele.Conformance.Tests',255),('Xemele.Differential.Tests',299)]:
 rows=list(E.fromstring(gzip.decompress((p/(name+'.xml.gz')).read_bytes())).iter('test'));assert len(rows)==n and all(t.get('result')=='Pass' for t in rows)
assert s['discovery']=={'apiDeclarations':251,'fixtures':3388}
assert s['referenceProvenance']['records']==242 and s['referenceProvenance']['build']=='1.0.0+'+s['sourceRevision'] and s['referenceProvenance']['unchangedAfterChecks']
log=gzip.decompress((p/'checks.txt.gz').read_bytes()).decode();assert log.count('exit=0; expected=0')>30 and 'Ran 14 tests' in log and 'Validated 120 tasks' in log
b=w/'docs/reports/M2-dtd-values/exclusive-65edbdd';timing=json.loads((b/'summary.json').read_text());z=b/'full-raw.zip';assert hashlib.sha256(z.read_bytes()).hexdigest()==timing['rawArchiveSha256']
for f,h in timing['sourceSha256'].items():assert hashlib.sha256((w/f).read_bytes()).hexdigest()==h
with zipfile.ZipFile(z) as a:
 raw=a.read(next(n for n in a.namelist() if n.endswith('.log'))).decode();assert raw.count('WorkloadResult')==6 and raw.count('has exited with code 0.')==2 and 'executed benchmarks: 2' in raw
run('git','diff','--check','641f5e050713ee35d411f71ae031233208a357e0..HEAD');run('git','diff','--check');run('git','diff','--cached','--check')
validation=run('python3','-B','tools/planning/tasks.py','validate')
r={'result':'passed','revision':revision,'runtimeChecksRevision':s['sourceRevision'],'runtimeSourceUnchanged':True,'focusedTests':994,'cumulativeTests':3097,'directTests':[2543,255,299],'publicApi':251,'freshReferences':242,'planningTests':14,'validation':validation.strip(),'whitespaceCorrection':'Display-only Markdown whitespace corrected; actual failed monolithic attempt preserved. All completed runtime checks retained at exact runtime revision; final whitespace checks now pass.','timingArchiveSha256':timing['rawArchiveSha256'],'actualTimingSamples':6}
(p/'final-correction-check.json').write_text(json.dumps(r,indent=2)+'\n');print(json.dumps(r))
