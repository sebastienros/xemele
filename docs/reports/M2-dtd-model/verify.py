"""Bounded independent DTD model and cumulative integrated runtime verification."""
import argparse, gzip, hashlib, json, os, platform, re, shlex, shutil, subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT/'docs/reports/M2-dtd-model'
CLAIM = '5e114bcce7a3aabd8fb9b7bd267f2e205a863d8c'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--report-prefix', required=True); p.add_argument('--audit-base', default=CLAIM)
    p.add_argument('--expected-branch', default='codex/m2-dtd-model'); p.add_argument('--runtime-count', type=int, default=306)
    p.add_argument('--task-count', type=int, default=118)
    a=p.parse_args(); assert re.fullmatch('[a-z0-9-]+', a.report_prefix)
    report=REPORT/a.report_prefix; report.mkdir()
    raw=ROOT/'artifacts/M2-dtd-model'/a.report_prefix; raw.mkdir(parents=True)
    dotnet=Path(shutil.which('dotnet')).resolve(strict=True)
    env=dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', XEMELE_DOTNET=str(dotnet),
        XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/', XEMELE_CORPUS_RESULTS=str(raw/'case-results'))
    logs=[]; s={'result':'failed', 'host':platform.platform(), 'architecture':platform.machine(), 'auditBase':a.audit_base, 'failures':[]}
    def run(*command, allow_failure=False):
        command=list(map(str,command));command[0]=str(dotnet) if command[0]=='dotnet' else command[0]
        print('$ '+shlex.join(command), flush=True)
        r=subprocess.run(command,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=600)
        logs.append('$ '+shlex.join(command)+'\n'+r.stdout+f'\nexit={r.returncode}\n'); print(r.stdout[-1400:],flush=True)
        if r.returncode:
            s['failures'].append({'command':shlex.join(command),'exit':r.returncode})
            if not allow_failure: raise AssertionError(logs[-1])
        return r.stdout, r.returncode
    def passed(*command): return run(*command)[0]
    def tests(assembly, label, expected, extra=()):
        xml=raw/(label+'.xml')
        _,code=run('dotnet',ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll','-noColor',*extra,'-result-xml',xml,allow_failure=True)
        entries=list(ET.parse(xml).iter('test'))
        counts={'total':len(entries),'failed':sum(t.get('result')=='Fail' for t in entries),'skipped':sum(t.get('result')=='Skip' for t in entries), 'exit':code}
        (report/(label+'.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(),mtime=0))
        s.setdefault('tests',{})[label]=counts
        assert len(entries)==expected, (label,counts,expected)
        if counts['failed'] or counts['skipped']: s['failures'].append({'suite':label,'counts':counts})
    try:
        revision=passed('git','rev-parse','HEAD').strip(); s['sourceRevision']=revision
        passed('git','merge-base','--is-ancestor',CLAIM,'HEAD');passed('git','merge-base','--is-ancestor',a.audit_base,'HEAD')
        passed('git','merge-base','--is-ancestor','ab2fcabaf3cc984901f484c6fce1069c385e8c2f','HEAD')
        assert passed('git','branch','--show-current').strip()==a.expected_branch
        assert json.loads((ROOT/'docs/tasks/M2-contracts.json').read_text())['status']=='verified'
        assert passed('dotnet','--version').strip()=='10.0.400';s['runtimes']=passed('dotnet','--list-runtimes')
        assert 'Microsoft.NETCore.App 10.0.11' in s['runtimes']
        source=['src/Xemele/Parsing/Dtd/Model','tests/Xemele.Tests/M2-dtd-model','samples/M2-dtd-model','docs/reports/M2-dtd-model/Approved','docs/reports/M2-dtd-model/verify.py']
        assert not passed('git','diff','--name-only','HEAD','--',*source).strip()
        assert not passed('git','ls-files','--others','--exclude-standard','--',*source).strip()
        fixture=(ROOT/'tests/Contracts/Fixtures/M2/Dtd.cs.fixture').read_text().split('internal enum DtdParseStep')[0]
        assert (REPORT/'Approved/ApprovedModel.cs').read_text()==fixture
        s['approvedModelSectionSha256']=hashlib.sha256(fixture.encode()).hexdigest()
        passed('dotnet','build',REPORT/'Approved/Approved.csproj',*FLAGS)
        passed('dotnet','build',ROOT/'samples/M2-dtd-model/ModelSample.csproj',*FLAGS)
        sample=passed('dotnet',ROOT/'samples/M2-dtd-model/bin/Release/net10.0/DtdModelSample.dll',REPORT/'Approved/bin/Release/net10.0/ApprovedDtdModel.dll')
        s['commonOnlyInventoryAndAllocation']=json.loads(sample.splitlines()[-1])
        passed('dotnet','build',ROOT/'tests/Xemele.Tests/Xemele.Tests.csproj',*FLAGS)
        tests('Xemele.Tests','focused',8,('-class','Xemele.Tests.DtdModel.ModelTests'))
        passed('python3','-B','docs/reports/M1-corpus-import/provision.py')
        discovery=json.loads(passed('python3','-B','tools/ApiBaseline/discover.py'));assert discovery['fixtures']==3388;s['discovery']=discovery
        passed('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS)
        source_runner=ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
        shutil.copytree(source_runner.parent,raw/'pinned-reference');pinned=raw/'pinned-reference'/source_runner.name
        env['XEMELE_BCL_RUNNER']=str(pinned);runner_hash=hashlib.sha256(pinned.read_bytes()).hexdigest()
        _,historical_code=run('python3','-B','docs/reports/M1-contracts/verify.py',allow_failure=True)
        s['historicalM1AndCurrentMtpExit']=historical_code
        historical=ROOT/'artifacts/M1-contracts/checks.txt'
        (report/'historical-checks.txt.gz').write_bytes(gzip.compress(historical.read_bytes(),mtime=0))
        passed('python3','-B','tools/ApiBaseline/compare.py','--report',report/'runtime-api.json')
        assert len(json.loads((report/'runtime-api.json').read_text())['provenance'])==251;s['publicApiMembers']=251
        tests('Xemele.Tests','Xemele.Tests',a.runtime_count)
        tests('Xemele.Conformance.Tests','Xemele.Conformance.Tests',255)
        tests('Xemele.Differential.Tests','Xemele.Differential.Tests',299)
        records=[json.loads(path.read_text()) for path in (raw/'case-results').glob('reference-*.json')]
        assert len(records)==242 and len({r['id'] for r in records})==242
        assert hashlib.sha256(pinned.read_bytes()).hexdigest()==runner_hash
        assert all(r['runnerSha256']==runner_hash and r['response']['engine']['build']=='1.0.0+'+revision for r in records)
        s['referenceProvenance']={'records':242,'runnerSha256':runner_hash,'sourceBuild':'1.0.0+'+revision,'pinnedOutput':str(pinned),'sdk':str(dotnet),'sdkSha256':hashlib.sha256(dotnet.read_bytes()).hexdigest()}
        (report/'reference-provenance.json').write_text(json.dumps([{'id':r['id'],'runnerSha256':r['runnerSha256'],'build':r['response']['engine']['build']} for r in sorted(records,key=lambda r:r['id'])],indent=2)+'\n')
        planning=passed('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v');assert 'Ran 14 tests' in planning
        assert f'Validated {a.task_count} tasks:' in passed('python3','-B','tools/planning/tasks.py','validate')
        task=json.loads((ROOT/'docs/tasks/M2-dtd-model.json').read_text());owned=task['owns']+['docs/tasks/M2-dtd-model.json'];paths=[]
        for cmd in [('git','diff','--name-only',a.audit_base+'..HEAD'),('git','diff','--name-only'),('git','diff','--cached','--name-only'),('git','ls-files','--others','--exclude-standard')]:paths+=passed(*cmd).splitlines()
        assert all(any(path.startswith(own) if own.endswith('/') else path==own for own in owned) for path in paths),paths
        for suffix in [a.audit_base+'..HEAD',None,'--cached']:passed('git','diff','--check',*([suffix] if suffix else []))
        s['ownedPaths']=sorted(set(paths)); s['result']='failed' if s['failures'] else 'passed'
        s['limitations']='Immutable owned persistence only; no parser, framing, resolver, XML DTD processing, timing gain or zero-allocation claim; host macOS Arm64.'
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0))
        (report/'summary.json').write_text(json.dumps(s,indent=2)+'\n')
    if s['failures']: raise SystemExit(1)
if __name__=='__main__':main()
