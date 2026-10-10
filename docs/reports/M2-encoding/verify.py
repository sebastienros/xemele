"""Task-owned verification; per-attempt output, no historical report rewriting."""
import argparse, gzip, hashlib, json, os, pathlib, platform, re, shlex, shutil, subprocess, tempfile, zipfile
import xml.etree.ElementTree as ET
ROOT=pathlib.Path(__file__).resolve().parents[3]
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
CLAIM='2e3d6012e664baf7bf5b3f3085bcb04de7dfc02e'
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--report-prefix',required=True);parser.add_argument('--audit-base',default=CLAIM);parser.add_argument('--expected-branch',required=True)
    parser.add_argument('--runtime-tests',type=int,default=255);parser.add_argument('--conformance-tests',type=int,default=255);parser.add_argument('--differential-tests',type=int,default=299);parser.add_argument('--reference-records',type=int,default=242)
    args=parser.parse_args();assert re.fullmatch('[a-z0-9-]+',args.report_prefix)
    report=ROOT/'docs/reports/M2-encoding'/args.report_prefix;report.mkdir()
    raw=ROOT/'artifacts/M2-encoding'/args.report_prefix;raw.mkdir(parents=True)
    dotnet=pathlib.Path(shutil.which('dotnet')).resolve(strict=True)
    env=dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', XEMELE_DOTNET=str(dotnet),XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',XEMELE_CORPUS_RESULTS=str(raw/'case-results'))
    logs=[];summary={'result':'failed','host':platform.platform(),'architecture':platform.machine()}
    def run(*command):
        command=list(map(str,command));command[0]=str(dotnet) if command[0]=='dotnet' else command[0]
        print('$ '+shlex.join(command),flush=True)
        result=subprocess.run(command,cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=600)
        logs.append('$ '+shlex.join(command)+'\n'+result.stdout+f'\nexit={result.returncode}\n');print(result.stdout[-1200:],flush=True)
        assert result.returncode==0,logs[-1];return result.stdout
    try:
        revision=run('git','rev-parse','HEAD').strip();summary['sourceRevision']=revision
        assert run('git','branch','--show-current').strip()==args.expected_branch
        for base in [CLAIM,args.audit_base,'ab2fcabaf3cc984901f484c6fce1069c385e8c2f']:run('git','merge-base','--is-ancestor',base,'HEAD')
        assert json.loads((ROOT/'docs/tasks/M2-contracts.json').read_text())['status']=='verified'
        assert run('dotnet','--version').strip()=='10.0.400'
        task=json.loads((ROOT/'docs/tasks/M2-encoding.json').read_text());owned=task['owns']+['docs/tasks/M2-encoding.json']
        source=['src/Xemele/Parsing/Input','tests/Xemele.Tests/M2-encoding','benchmarks/Xemele.Benchmarks/M2-encoding','samples/M2-encoding','docs/reports/M2-encoding/Program.cs','docs/reports/M2-encoding/verify.py','docs/reports/M2-encoding/Xemele.EncodingBenchmarks.csproj','tests/Xemele.Tests/M1-dom/DomTests.cs']
        assert not run('git','diff','HEAD','--name-only','--',*source).strip()
        assert not run('git','ls-files','--others','--exclude-standard','--',*source).strip()
        run('python3','-B','docs/reports/M1-corpus-import/provision.py')
        run('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS)
        runner=ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll';shutil.copytree(runner.parent,raw/'pinned-reference')
        pinned=raw/'pinned-reference'/runner.name;runner_hash=hashlib.sha256(pinned.read_bytes()).hexdigest();env['XEMELE_BCL_RUNNER']=str(pinned)
        run('python3','-B','docs/reports/M1-contracts/verify.py')
        run('python3','-B','tools/ApiBaseline/discover.py')
        run('python3','-B','tools/ApiBaseline/compare.py','--report',report/'runtime-api.json')
        counts={}
        for assembly,expected in [('Xemele.Tests',args.runtime_tests),('Xemele.Conformance.Tests',args.conformance_tests),('Xemele.Differential.Tests',args.differential_tests)]:
            xml=raw/(assembly+'.xml');run('dotnet',ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll','-noColor','-result-xml',xml)
            tests=list(ET.parse(xml).iter('test'));assert len(tests)==expected and all(t.get('result')=='Pass' for t in tests),(assembly,len(tests))
            counts[assembly]={'total':len(tests),'failed':0,'skipped':0};(report/(assembly+'.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(),mtime=0))
        summary['tests']=counts
        records=[json.loads(p.read_text()) for p in (raw/'case-results').glob('reference-*.json')];assert len(records)==args.reference_records and len({r['id'] for r in records})==args.reference_records
        assert hashlib.sha256(pinned.read_bytes()).hexdigest()==runner_hash
        assert all(r['runnerSha256']==runner_hash and r['response']['engine']['build']=='1.0.0+'+revision for r in records)
        summary['reference']={'count':len(records),'runnerSha256':runner_hash,'build':'1.0.0+'+revision,'dotnet':str(dotnet),'dotnetSha256':hashlib.sha256(dotnet.read_bytes()).hexdigest()}
        checker=ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
        run('dotnet','build','docs/reports/M2-contracts/Projects/Encoding/Encoding.csproj',*FLAGS)
        model=ROOT/'docs/reports/M2-contracts/Projects/Encoding/bin/Release/net10.0/M2Encoding.dll'
        run('dotnet',checker,'compare',model,ROOT/'tests/Contracts/Fixtures/M2/Encoding.golden.txt')
        project='docs/reports/M2-encoding/Xemele.EncodingBenchmarks.csproj';run('dotnet','build',project,*FLAGS)
        run('dotnet','run','--project',project,'-c','Release','--no-build','--','--contract',model,report/'internal-contract.txt')
        run('dotnet','run','--project',project,'-c','Release','--no-build','--','--oracle')
        allocation=run('dotnet','run','--project',project,'-c','Release','--no-build','--','--allocation');(report/'allocations.txt').write_text(allocation)
        run('dotnet','run','--project','samples/M2-encoding/Sample.csproj',*FLAGS)
        run('dotnet','pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','--no-restore','-o',raw/'packages')
        package=next((raw/'packages').glob('Xemele.*.nupkg'))
        with zipfile.ZipFile(package) as archive:
            nuspec=ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))));version=next(n.text for n in nuspec.iter() if n.tag.endswith('}version'));assert 'lib/net10.0/Xemele.dll' in archive.namelist()
        with tempfile.TemporaryDirectory(prefix='xemele-m2-encoding-package-') as directory:
            directory=pathlib.Path(directory)
            (directory/'Consumer.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="{version}" /></ItemGroup></Project>')
            (directory/'Program.cs').write_text((ROOT/'samples/M2-encoding/Program.cs').read_text())
            run('dotnet','run','--project',directory/'Consumer.csproj','-c','Release','--disable-build-servers','-p:UseSharedCompilation=false',f'-p:RestoreSources={raw/"packages"}',f'-p:RestorePackagesPath={directory/"packages"}')
        run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v')
        run('python3','-B','tools/planning/tasks.py','validate')
        paths=[]
        for command in [('git','diff','--name-only',args.audit_base+'..HEAD'),('git','diff','--name-only'),('git','diff','--cached','--name-only'),('git','ls-files','--others','--exclude-standard')]:paths.extend(run(*command).splitlines())
        assert all(any(path.startswith(own) if own.endswith('/') else path==own for own in owned) for path in paths),paths
        for suffix in [args.audit_base+'..HEAD',None,'--cached']:run('git','diff','--check',*([suffix] if suffix else []))
        summary['ownedPaths']=sorted(set(paths));summary['auditBase']=args.audit_base;summary['expectedBranch']=args.expected_branch;summary['result']='passed'
    finally:
        (raw/'checks.txt').write_text('\n'.join(logs));(report/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
        (report/'checks.txt').write_text('\n'.join(line for log in logs for line in log.splitlines() if line.startswith(('$ ','exit=','  total:','Matched ','Exact internal','Cold setup','Warm setup','Informational retained','Validated ','All six','  failed:','  succeeded:','  skipped:','Build succeeded','Build FAILED')))+'\n')
if __name__=='__main__':main()
