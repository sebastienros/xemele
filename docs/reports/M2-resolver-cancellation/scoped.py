"""Scoped post-review correctness or explicitly granted allocation driver."""
import argparse, gzip, hashlib, json, os, platform, re, shutil, subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
def main():
    p=argparse.ArgumentParser();p.add_argument('--report-prefix',required=True);p.add_argument('--allocation-grant');a=p.parse_args()
    assert re.fullmatch('[a-z0-9-]+',a.report_prefix)
    report=ROOT/'docs/reports/M2-resolver-cancellation'/a.report_prefix;report.mkdir()
    raw=ROOT/'artifacts/M2-resolver-cancellation'/a.report_prefix;raw.mkdir(parents=True)
    sdk=Path(shutil.which('dotnet')).resolve(strict=True);logs=[]
    env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0');s={'result':'failed','host':platform.platform(),'architecture':platform.machine()}
    def run(*args):
        args=list(map(str,args));args[0]=str(sdk) if args[0]=='dotnet' else args[0]
        result=subprocess.run(args,cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=180)
        logs.append('$ '+repr(args)+'\n'+result.stdout+f'\nexit={result.returncode}\n');print(result.stdout[-1000:],flush=True)
        assert result.returncode==0,logs[-1];return result.stdout
    def hashes(paths):return {str(path.relative_to(ROOT)):hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
    try:
        revision=run('git','rev-parse','HEAD').strip();s['sourceRevision']=revision
        assert run('git','branch','--show-current').strip()=='codex/m2-resolver-cancellation'
        assert not run('git','diff','--name-only','HEAD','--','src/Xemele/Resolution/Core/XmlResolutionSession.cs','tests/Xemele.Tests/M2-resolver-cancellation','samples/M2-resolver-cancellation','benchmarks/Xemele.Benchmarks/M2-resolver-cancellation').strip()
        assert not run('git','diff','--name-only','0d059c8..HEAD','--','src/Xemele','tests').strip()
        s['runtimeAndTestsUnchangedFrom']='0d059c8f21b491e0c75994bbf13c991f9c6899b0'
        assert run('dotnet','--version').strip()=='10.0.400'
        paths=[ROOT/name for name in run('git','ls-files','--','src/Xemele/Resolution/Core/XmlResolutionSession.cs','tests/Xemele.Tests/M2-resolver-cancellation','samples/M2-resolver-cancellation','benchmarks/Xemele.Benchmarks/M2-resolver-cancellation').splitlines()]
        before=hashes(paths);s['sourceIdentitiesBefore']=before
        sample=ROOT/'samples/M2-resolver-cancellation/bin/Release/net10.0/Xemele.ResolverSample.dll'
        if a.allocation_grant:
            s['exclusiveGrant']=a.allocation_grant
            assert sample.exists()
            # Builds, discovery and correctness occurred before the grant; do not rebuild during measurements.
            assert len(a.allocation_grant)>10
            dll_before=hashes([sample,sample.parent/'Xemele.dll']);s['binaryIdentitiesBefore']=dll_before
            output=run('dotnet',sample,'--allocation');observation=json.loads(output.splitlines()[-1])
            assert observation['sourceBuild']=='1.0.0+'+revision
            assert observation['borrowed']==0 and observation['escaped']>0 and observation['guard']==0
            s['allocation']=observation;s['binaryIdentitiesAfter']=hashes([sample,sample.parent/'Xemele.dll']);assert s['binaryIdentitiesAfter']==dll_before
        else:
            run('dotnet','build','tests/Xemele.Tests/Xemele.Tests.csproj',*FLAGS)
            xml=raw/'binding.xml';run('dotnet',ROOT/'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-noColor','-class','Xemele.Tests.Resolution.CancellationBindingTests','-result-xml',xml)
            entries=list(ET.parse(xml).iter('test'));assert len(entries)==18 and all(t.get('result')=='Pass' for t in entries)
            s['focusedTests']=18;(report/'binding.xml.gz').write_bytes(gzip.compress(xml.read_bytes(),mtime=0))
            run('dotnet','build','samples/M2-resolver-cancellation/Xemele.ResolverSample.csproj',*FLAGS)
            output=run('dotnet',sample);assert 'four access/default/name mutations passed' in output and 'Runtime source build: 1.0.0+'+revision in output;s['inventoryAndMutations']=output
            run('dotnet','pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','-o',raw/'feed')
            packages=list((raw/'feed').glob('*.nupkg'));assert len(packages)==1;s['packageSha256']=hashlib.sha256(packages[0].read_bytes()).hexdigest()
            run('dotnet','build','samples/M2-resolver-cancellation/Xemele.ResolverSample.csproj',*FLAGS,'-p:UsePackage=true','-p:ManagePackageVersionsCentrally=false',f'-p:RestoreSources={raw}/feed',f'-p:RestorePackagesPath={raw}/packages','-p:NuGetAudit=false')
            assert 'Runtime source build: 1.0.0+'+revision in run('dotnet',sample)
            run('dotnet','build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*FLAGS)
            run('dotnet','build','benchmarks/Xemele.Benchmarks/M2-resolver-cancellation/Xemele.ResolverSample.csproj',*FLAGS)
            standalone=ROOT/'benchmarks/Xemele.Benchmarks/M2-resolver-cancellation/bin/Release/net10.0/Xemele.ResolverSample.dll'
            assert 'oracle passed' in run('dotnet',standalone,'--oracle')
            for dll in [standalone,ROOT/'benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll']:
                listing=run('dotnet',dll,'--list','flat','--filter','*CancellationBindingBenchmarks*')
                assert all('CancellationBindingBenchmarks.'+name in listing for name in ['DirectCancellationChecks','BindExactToken','BindAlternatingToken'])
            s['oracleAndDiscovery']='all3 methods and both alternating tokens checked; main+standalone listing'
            s['sampleBinaryIdentities']=hashes([sample,sample.parent/'Xemele.dll'])
        s['sourceIdentitiesAfter']=hashes(paths);assert s['sourceIdentitiesAfter']==before
        assert run('git','rev-parse','HEAD').strip()==revision
        run('python3','-B','tools/planning/tasks.py','validate');run('git','diff','--check')
        s['result']='passed'
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0));(report/'summary.json').write_text(json.dumps(s,indent=2)+'\n')
if __name__=='__main__':main()
