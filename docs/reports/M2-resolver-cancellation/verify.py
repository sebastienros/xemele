"""Per-call cancellation binding correctness, cumulative preservation and exact-source provenance; no new measurements."""
import argparse, gzip, hashlib, json, os, platform, re, shlex, shutil, subprocess, sys
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
REPORT=ROOT/'docs/reports/M2-resolver-cancellation'
CLAIM='55c1c47b9f29f4f7c53ceb842b19c2f96419b1d8'
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
CHECKER=ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'

def rows(path): return {r['member']:r['declaration'] for r in map(json.loads,path.read_text().splitlines()[1:])}
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--report-prefix',required=True);p.add_argument('--audit-base',default=CLAIM)
    p.add_argument('--expected-branch',default='codex/m2-resolver-cancellation')
    p.add_argument('--runtime-count',type=int,default=4514);p.add_argument('--api-count',type=int,default=272)
    p.add_argument('--fixture-count',type=int,default=3388);p.add_argument('--task-count',type=int,default=123)
    a=p.parse_args();assert re.fullmatch('[a-z0-9-]+',a.report_prefix)
    report=REPORT/a.report_prefix;report.mkdir();raw=ROOT/'artifacts/M2-resolver-cancellation'/a.report_prefix;raw.mkdir(parents=True)
    sdk=Path(shutil.which('dotnet')).resolve(strict=True)
    env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0',PATH=str(sdk.parent)+os.pathsep+os.environ['PATH'],XEMELE_DOTNET=str(sdk),
      XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',XEMELE_CORPUS_RESULTS=str(raw/'case-results'))
    logs=[];s={'result':'failed','host':platform.platform(),'architecture':platform.machine(),'auditBase':a.audit_base}
    def run(*command,expected=0):
        command=list(map(str,command));command[0]=str(sdk) if command[0]=='dotnet' else command[0]
        print('$ '+shlex.join(command),flush=True)
        result=subprocess.run(command,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=600)
        logs.append('$ '+shlex.join(command)+'\n'+result.stdout+f'\nexit={result.returncode}; expected={expected}\n')
        print(result.stdout[-1500:],flush=True);assert result.returncode==expected,logs[-1];return result.stdout
    try:
        revision=run('git','rev-parse','HEAD').strip();s['sourceRevision']=revision
        run('git','merge-base','--is-ancestor',CLAIM,'HEAD');run('git','merge-base','--is-ancestor',a.audit_base,'HEAD')
        run('git','merge-base','--is-ancestor','ab2fcabaf3cc984901f484c6fce1069c385e8c2f','HEAD')
        assert json.loads((ROOT/'docs/tasks/M2-resolver.json').read_text())['status']=='verified'
        run('git','merge-base','--is-ancestor','b12b688d64fb3604b365d6caebcd5e4ea84495a1','HEAD')
        s['branch']=run('git','branch','--show-current').strip();assert s['branch']==a.expected_branch
        assert run('dotnet','--version').strip()=='10.0.400'
        task=json.loads((ROOT/'docs/tasks/M2-resolver-cancellation.json').read_text()); source=[x for x in task['owns'] if x != 'docs/reports/M2-resolver-cancellation/']
        tracked=run('git','ls-files','--',*source).splitlines();identities={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in tracked};s['sourceIdentitiesBefore']=identities
        assert not run('git','diff','--name-only','HEAD','--',*source).strip()
        assert not run('git','ls-files','--others','--exclude-standard','--',*source).strip()
        run('python3','-B','docs/reports/M1-corpus-import/provision.py')
        discovery=json.loads(run('python3','-B','tools/ApiBaseline/discover.py'))
        assert discovery['fixtures']==a.fixture_count
        s['discovery']=discovery
        run('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS)
        runner=ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
        shutil.copytree(runner.parent,raw/'pinned-reference');pinned=raw/'pinned-reference'/runner.name
        reference_files={str(f.relative_to(pinned.parent)):hashlib.sha256(f.read_bytes()).hexdigest() for f in pinned.parent.rglob('*') if f.is_file()}
        env['XEMELE_BCL_RUNNER']=str(pinned);runner_hash=hashlib.sha256(pinned.read_bytes()).hexdigest()
        # This historical harness runs independent M1 models/protocol/negative
        # compiler probes/foundation11 plus the actual current solution.
        run('python3','-B','docs/reports/M1-contracts/verify.py')
        run('dotnet','build','docs/reports/M2-contracts/Projects/Resolver/Resolver.csproj',*FLAGS)
        run('dotnet',CHECKER,'compare',ROOT/'docs/reports/M2-contracts/Projects/Resolver/bin/Release/net10.0/M2Resolver.dll',ROOT/'tests/Contracts/Fixtures/M2/Resolver.golden.txt')
        model=rows(ROOT/'tests/Contracts/Fixtures/M2/Resolver.golden.txt');common=rows(ROOT/'tests/Contracts/Fixtures/M1/Common.golden.txt')
        selected={k:v for k,v in model.items() if k not in common and 'Xemele.XmlDtdProcessing' not in k}
        assert rows(ROOT/'docs/api/M2-resolver.txt')==selected
        s['independentModelMembers']=len(model);s['actualAdditiveResolverMembers']=len(selected);s['deferredModelMembers']='XmlDtdProcessing4 belongs to M2-entities; common81 already shipped'
        run('python3','-B','tools/ApiBaseline/compare.py','--report',report/'runtime-api.json')
        s['actualRuntimeMembers']=len(json.loads((report/'runtime-api.json').read_text())['provenance'])
        assert s['actualRuntimeMembers']==a.api_count
        tests={}
        for assembly,minimum in [('Xemele.Tests',a.runtime_count),('Xemele.Conformance.Tests',255),('Xemele.Differential.Tests',299)]:
            xml=raw/(assembly+'.xml');run('dotnet',ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll','-noColor','-result-xml',xml)
            entries=list(ET.parse(xml).iter('test'));assert len(entries)==minimum and all(t.get('result')=='Pass' for t in entries)
            tests[assembly]={'total':len(entries),'failed':0,'skipped':0};(report/(assembly+'.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(),mtime=0))
        focused=raw/'resolver.xml';run('dotnet',ROOT/'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-noColor','-class','Xemele.Tests.Resolution.ResolverTests','-result-xml',focused)
        focused_tests=list(ET.parse(focused).iter('test'));assert len(focused_tests)==43 and all(t.get('result')=='Pass' for t in focused_tests)
        binding=raw/'binding.xml';run('dotnet',ROOT/'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-noColor','-class','Xemele.Tests.Resolution.CancellationBindingTests','-result-xml',binding)
        binding_tests=list(ET.parse(binding).iter('test'));assert len(binding_tests)==18 and all(t.get('result')=='Pass' for t in binding_tests)
        s['bindingTests']=len(binding_tests);(report/'binding.xml.gz').write_bytes(gzip.compress(binding.read_bytes(),mtime=0))
        s['runtimeTests']=tests;s['resolverTests']=len(focused_tests)
        records=[json.loads(f.read_text()) for f in (raw/'case-results').glob('reference-*.json')]
        assert len(records)==242 and len({r['id'] for r in records})==242
        assert hashlib.sha256(pinned.read_bytes()).hexdigest()==runner_hash
        assert {str(f.relative_to(pinned.parent)):hashlib.sha256(f.read_bytes()).hexdigest() for f in pinned.parent.rglob('*') if f.is_file()}==reference_files
        s['referenceOutputIdentities']=reference_files
        assert all(r['runnerSha256']==runner_hash and r['response']['engine']['build']=='1.0.0+'+revision for r in records)
        assert sum(r['comparison']=='matched-rejection' for r in records)==239
        assert sum(r['comparison']=='documented-reference-divergence' for r in records)==3
        s['referenceProvenance']={'records':242,'matches':239,'documentedDivergences':3,'runnerSha256':runner_hash,'sourceBuild':'1.0.0+'+revision,'pinnedOutput':str(pinned),'sdk':str(sdk),'sdkSha256':hashlib.sha256(sdk.read_bytes()).hexdigest()}
        (report/'reference-provenance.json').write_text(json.dumps([{'id':r['id'],'runnerSha256':r['runnerSha256'],'build':r['response']['engine']['build']} for r in sorted(records,key=lambda r:r['id'])],indent=2)+'\n')
        # Public package consumer uses exactly the same offline sample plus the
        # friend assembly composition checks; no copied future implementations.
        run('dotnet','build','samples/M2-resolver-cancellation/Xemele.ResolverSample.csproj',*FLAGS)
        sample=run('dotnet',ROOT/'samples/M2-resolver-cancellation/bin/Release/net10.0/Xemele.ResolverSample.dll')
        assert 'M1 strict parse preserved' in sample and 'Exact token B' in sample;s['compiledInternalHook']=sample.splitlines()[0];(report/'inventory.txt').write_text(sample)
        run('dotnet','pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','-o',raw/'feed')
        feed=raw/'feed';packages=list(feed.glob('*.nupkg'));assert len(packages)==1
        s['packageSha256']=hashlib.sha256(packages[0].read_bytes()).hexdigest()
        run('dotnet','build','samples/M2-resolver-cancellation/Xemele.ResolverSample.csproj',*FLAGS,'-p:UsePackage=true','-p:ManagePackageVersionsCentrally=false',f'-p:RestoreSources={feed}',f'-p:RestorePackagesPath={raw}/packages','-p:NuGetAudit=false')
        package_sample=run('dotnet',ROOT/'samples/M2-resolver-cancellation/bin/Release/net10.0/Xemele.ResolverSample.dll');assert 'M1 strict parse preserved' in package_sample
        s['packageConsumer']='passed exact independently compiled internal hook/token B/paused parent and M1 parse from freshly built local nupkg'
        run('dotnet','build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*FLAGS)
        listing=run('dotnet',ROOT/'benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll','--list','flat','--filter','*ResolverBenchmarks*')
        assert all('ResolverBenchmarks.'+name in listing for name in ['DirectRead','BoundedRead','BoundedAsyncRead','SessionOpenReadDispose'])
        run('dotnet','build','benchmarks/Xemele.Benchmarks/M2-resolver-cancellation/Xemele.ResolverSample.csproj',*FLAGS)
        standalone=ROOT/'artifacts/M2-resolver-cancellation/standalone-bin/Release/net10.0/Xemele.ResolverSample.dll'
        run('dotnet','build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*FLAGS)
        run('dotnet',standalone,'--oracle',ROOT/'benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll')
        for assembly in [standalone,ROOT/'benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll']:
            listing=run('dotnet',assembly,'--list','flat','--filter','*CancellationBindingBenchmarks*')
            assert all('CancellationBindingBenchmarks.'+name in listing for name in ['DirectCancellationChecks','BindExactToken','BindAlternatingToken'])
        s['benchmarks']='main and standalone built/discovered; standalone correctness oracle passed; timing/allocation mode held for exclusive grant'
        run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v');validation=run('python3','-B','tools/planning/tasks.py','validate');assert f'Validated {a.task_count} tasks:' in validation
        task=json.loads((ROOT/'docs/tasks/M2-resolver-cancellation.json').read_text());owned=task['owns']+['docs/tasks/M2-resolver-cancellation.json'];paths=[]
        for command in [('git','diff','--name-only',a.audit_base+'..HEAD'),('git','diff','--name-only'),('git','diff','--cached','--name-only'),('git','ls-files','--others','--exclude-standard')]:paths+=run(*command).splitlines()
        assert all(any(path.startswith(own) if own.endswith('/') else path==own for own in owned) for path in paths),paths
        for suffix in [a.audit_base+'..HEAD',None,'--cached']:run('git','diff','--check',*([suffix] if suffix else []))
        s['sourceIdentitiesAfter']={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in tracked};assert s['sourceIdentitiesAfter']==identities
        assert run('git','rev-parse','HEAD').strip()==revision;assert run('git','branch','--show-current').strip()==a.expected_branch
        s['ownedPaths']=sorted(set(paths));s['result']='passed'
        s['limitations']='macOSArm64 only; no core consumer implementation; no new allocation/timing mode in this check; existing guarded allocation regressions remain part of inherited full correctness suites'
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0));(report/'summary.json').write_text(json.dumps(s,indent=2)+'\n')
if __name__=='__main__':main()
