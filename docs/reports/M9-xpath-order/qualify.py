"""Qualify the unchanged complete frozen managed release fixture operations."""
import sys
sys.dont_write_bytecode = True
import hashlib,importlib.util,json,os,subprocess,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent
host=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
out=ROOT/'artifacts/M9-xpath-order/qualification';out.mkdir(parents=True,exist_ok=False)
spec=importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
supervisor=importlib.util.module_from_spec(spec);spec.loader.exec_module(supervisor)
supervisor.ROOT,supervisor.ARTIFACTS=ROOT,out
env=dict(os.environ,DOTNET_ROOT=str(host.parent),DOTNET_ROLL_FORWARD='Disable',
         DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0')
manifest=ROOT/'benchmarks/baselines/release/fixtures.json'
assert hashlib.sha256(manifest.read_bytes()).hexdigest()=='d14e74167c1d552be721c121e6a6c6a267d30e2ad4a53d3ef71ce82031363fb0'
report=dict(revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),result='failed',
            fixtureManifestSha256=hashlib.sha256(manifest.read_bytes()).hexdigest(),commands=[],rows=[],controlledTimingCredit=False)
deadline=time.monotonic()+1500
def run(argv,cap):
    index=len(report['commands']);end=min(deadline,time.monotonic()+cap);argv=list(map(str,argv))
    receipt=supervisor.supervise_command(argv,index,env,end,end+10);log=out/f'command-{index}.log'
    row=dict(argv=argv,receipt=receipt,outputSha256=hashlib.sha256(log.read_bytes()).hexdigest());report['commands'].append(row)
    if not supervisor.workload_succeeded(receipt):
        row['failureTail']=log.read_text(errors='replace')[-4000:];raise RuntimeError(row['failureTail'])
try:
    project=ROOT/'docs/reports/M9-performance/ManagedWorker/ManagedWorker.csproj'
    run([host,'restore',project,'--source','/private/tmp','-p:NuGetAudit=false','--disable-build-servers'],180)
    run([host,'build',project,'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false'],180)
    binary=project.parent/'bin/Release/net10.0/ManagedWorker.dll'
    for fixture in json.loads(manifest.read_bytes())['fixtures']:
        target=out/(fixture['id']+'.json')
        run([host,'--fx-version','10.0.11',binary,'xemele',manifest.parent/fixture['path'],manifest,target,'qualify'],180)
        actual=json.loads(target.read_bytes());assert actual['qualification'] and actual['digest']==fixture['semantic']['sha256'] and actual['expectedXPath']==fixture['xpathExpected']
        report['rows'].append(dict(fixture=fixture['id'],inputSha256=fixture['sha256'],actual=actual))
        print('PASS',fixture['id'],actual['expectedXPath'],flush=True)
    assert len(report['rows'])==10
    report['result']='passed'
finally:
    (HERE/'qualification.json').write_text(json.dumps(report,indent=2)+'\n')
