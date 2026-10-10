#!/usr/bin/env python3
"""One controlled baseline/direct/sample comparison; no retries or solution rerun."""
import argparse
import gzip
import json
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
REPORT = Path(__file__).resolve().parent
FLAGS = ['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--report-prefix',required=True);args=parser.parse_args()
    report=REPORT / args.report_prefix;report.mkdir()
    raw=ROOT / 'artifacts/M2-streaming-allocation-probe' / args.report_prefix;raw.mkdir(parents=True)
    dotnet=Path(shutil.which('dotnet')).resolve(strict=True)
    env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0');env.pop('XEMELE_STREAMING_ALLOCATION_SAMPLE_OUTPUT',None)
    logs=[];summary=dict(sourceRevision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),checks={},scope='bounded allocation-location experiment; no fix/verification claim')
    def run(name,*command,extra=None):
        command=list(map(str,command));command[0]=str(dotnet) if command[0]=='dotnet' else command[0]
        print('$ '+shlex.join(command),flush=True)
        p=subprocess.run(command,cwd=ROOT,env=dict(env,**(extra or {})),stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=300)
        logs.append(name+'\n$ '+shlex.join(command)+'\nenvExtra='+repr(extra)+'\nexit='+str(p.returncode)+'\n'+p.stdout)
        summary['checks'][name]=dict(exit=p.returncode,result='passed' if p.returncode==0 else 'failed')
        print(p.stdout[-1800:],flush=True);return p
    def samples(path):
        data=json.loads(path.read_text());out={}
        for phase,expected in [('warmSamples',100),('measuredSamples',10000)]:
            rows=data[phase];assert len(rows)==expected
            assert all(r['Iteration']==i for i,r in enumerate(rows))
            fields=['AppendBytes','ValidationBytes','ObservationBytes','GapBytes']
            out[phase]=dict(count=len(rows),nonzero=[r for r in rows if any(r[k] for k in fields)],totals={k:sum(r[k] for r in rows) for k in fields},first=rows[0],last=rows[-1],threadIds=sorted({r['ThreadId'] for r in rows}),consumptionSteps=sorted({(r['Consumed'],r['Step']) for r in rows}))
        (report / (path.name+'.gz')).write_bytes(gzip.compress(path.read_bytes(),mtime=0));return out
    try:
        p=run('runtime-build','dotnet','build','tests/Xemele.Tests/Xemele.Tests.csproj',*FLAGS)
        if p.returncode:return 1
        p=run('diagnostic-build','dotnet','build',REPORT / 'Diagnostics',*FLAGS)
        if p.returncode:return 1
        for mode in ['baseline','sampled']:
            xml=raw / (mode+'.xml');sample=raw / (mode+'-samples.json')
            extra={} if mode=='baseline' else {'XEMELE_STREAMING_ALLOCATION_SAMPLE_OUTPUT':str(sample)}
            p=run('direct-'+mode,'dotnet',ROOT / 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-noColor','-result-xml',xml,extra=extra)
            if xml.exists():
                (report / (xml.name+'.gz')).write_bytes(gzip.compress(xml.read_bytes(),mtime=0));tests=list(ET.parse(xml).iter('test'))
                summary['checks']['direct-'+mode]['counts']=dict(total=len(tests),failed=sum(t.get('result')=='Fail' for t in tests),skipped=sum(t.get('result')=='Skip' for t in tests))
                summary['checks']['direct-'+mode]['streaming']=[dict(result=t.get('result'),output=t.findtext('output'),failure=t.findtext('failure/message')) for t in tests if t.get('name','').endswith('WarmRepeatedTagsAndStreamingUnicodeDispatchAllocateZero')]
            if sample.exists():summary['checks']['direct-'+mode]['samples']=samples(sample)
        sample=raw / 'independent-samples.json'
        p=run('independent-sampled','dotnet',REPORT / 'Diagnostics/bin/Release/net10.0/Xemele.SyntaxBenchmarks.dll','sampled',extra={'XEMELE_STREAMING_ALLOCATION_SAMPLE_OUTPUT':str(sample)})
        summary['checks']['independent-sampled']['output']=p.stdout
        if sample.exists():summary['checks']['independent-sampled']['samples']=samples(sample)
        return int(any(c['exit'] for c in summary['checks'].values()))
    finally:
        (report / 'checks.txt.gz').write_bytes(gzip.compress(('source='+summary['sourceRevision']+'\n'+'\n'.join(logs)).encode(),mtime=0))
        (report / 'summary.json').write_text(json.dumps(summary,indent=2)+'\n')

if __name__=='__main__':raise SystemExit(main())
