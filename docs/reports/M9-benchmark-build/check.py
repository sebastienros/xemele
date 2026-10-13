"""Finite aggregate/standalone benchmark build validation; never runs measurements."""
import sys
sys.dont_write_bytecode=True
import argparse,hashlib,importlib.util,json,os,subprocess,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent
p=argparse.ArgumentParser();p.add_argument('--phase',required=True);p.add_argument('--resolver-only',action='store_true');a=p.parse_args()
host=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
out=ROOT/'artifacts/M9-benchmark-build'/a.phase;out.mkdir(parents=True,exist_ok=False)
spec=importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
supervisor=importlib.util.module_from_spec(spec);spec.loader.exec_module(supervisor)
supervisor.ROOT,supervisor.ARTIFACTS=ROOT,out
report=dict(revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),result='failed',commands=[],measurementsExecuted=False)
env=dict(os.environ,DOTNET_ROOT=str(host.parent),DOTNET_ROLL_FORWARD='Disable')
deadline=time.monotonic()+800
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(argv,cap):
    index=len(report['commands']);end=min(deadline,time.monotonic()+cap);argv=list(map(str,argv))
    receipt=supervisor.supervise_command(argv,index,env,end,end+10);log=out/f'command-{index}.log';text=log.read_text(errors='replace')
    report['commands'].append(dict(argv=argv,receipt=receipt,outputSha256=hashlib.sha256(log.read_bytes()).hexdigest(),outputTail=text[-3000:]))
    print(text[-2000:],flush=True)
    assert supervisor.workload_succeeded(receipt),text
    return text
try:
    parent='benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj'
    projects = ['benchmarks/Xemele.Benchmarks/M2-resolver-cancellation/Xemele.ResolverSample.csproj'] if a.resolver_only else [parent,*[f'benchmarks/Xemele.Benchmarks/{name}/Probe.csproj' for name in ['M8-sch-compile','M8-sch-report','M8-sch-rule-scope','M8-sch-validate']], 'benchmarks/Xemele.Benchmarks/M8-rng-validate/Rng.Probe.csproj']
    for project in projects:
        run([host,'restore',project,'--source','/private/tmp',*flags],120)
        run([host,'build',project,'-c','Release','--no-restore',*flags],150)
    # List again after nested builds: generated child obj files must stay excluded.
    run([host,'build',parent,'-c','Release','--no-restore',*flags],150)
    text=run([host,'--fx-version','10.0.11','benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll','--list','flat'],30)
    assert all(name in text for name in ['DomStorageBenchmarks.ParseOwnedDom','XPathOrderBenchmarks.Query','XPathOverlapBenchmarks.ReverseAndOverlappingMerge'])
    report['listedBenchmarks']=text.splitlines()
    run(['python3','tools/planning/tasks.py','validate'],30)
    report['result']='passed'
finally:
    (HERE/('checks-'+a.phase+'.json')).write_text(json.dumps(report,indent=2)+'\n')
