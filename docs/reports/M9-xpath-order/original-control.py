"""Reproduce the original finite-work failure on the exact pre-repair runtime."""
import sys
sys.dont_write_bytecode = True
import hashlib,importlib.util,json,os,shutil,subprocess,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent
baseline=ROOT/'artifacts/M9-xpath-order/allocations2/baseline'
assert baseline.is_dir()
source=ROOT/'docs/reports/M9-performance/ManagedWorker'
assert not subprocess.check_output(['git','diff','2d38e67fc','HEAD','--',str(source.relative_to(ROOT))],cwd=ROOT)
target=baseline/'docs/reports/M9-performance/ManagedWorker'
shutil.copytree(source,target,ignore=shutil.ignore_patterns('bin','obj'))
host=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
out=ROOT/'artifacts/M9-xpath-order/original-control';out.mkdir(parents=True,exist_ok=False)
spec=importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
supervisor=importlib.util.module_from_spec(spec);spec.loader.exec_module(supervisor)
supervisor.ROOT,supervisor.ARTIFACTS=baseline,out
env=dict(os.environ,DOTNET_ROOT=str(host.parent),DOTNET_ROLL_FORWARD='Disable',
         DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0')
report=dict(result='failed',baselineRevision=subprocess.check_output(['git','rev-parse','2d38e67fc'],cwd=ROOT,text=True).strip(),commands=[])
deadline=time.monotonic()+500
def run(argv,cap,expectedFailure=False):
    index=len(report['commands']);end=min(deadline,time.monotonic()+cap);argv=list(map(str,argv))
    receipt=supervisor.supervise_command(argv,index,env,end,end+10);log=out/f'command-{index}.log';text=log.read_text(errors='replace')
    report['commands'].append(dict(argv=argv,receipt=receipt,outputSha256=hashlib.sha256(log.read_bytes()).hexdigest(),outputTail=text[-3000:]))
    if expectedFailure:
        assert receipt['reason']=='failed' and receipt['exit']!=0 and receipt['directChildReaped'] and receipt['ownedGroupAbsent'] and receipt['logClosed']
        assert 'XPath evaluation limit exceeded' in text and ('Normalize' in text or 'SelectAxis' in text or 'AddUnique' in text),text
    else:assert supervisor.workload_succeeded(receipt),text
try:
    project=target/'ManagedWorker.csproj'
    run([host,'restore',project,'--source','/private/tmp','-p:NuGetAudit=false','--disable-build-servers'],120)
    run([host,'build',project,'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false'],180)
    manifest=ROOT/'benchmarks/baselines/release/fixtures.json'
    fixture=next(r for r in json.loads(manifest.read_bytes())['fixtures'] if r['id']=='entity-medium')
    run([host,'--fx-version','10.0.11',target/'bin/Release/net10.0/ManagedWorker.dll','xemele',manifest.parent/fixture['path'],manifest,out/'original.json','qualify'],180,True)
    report.update(result='passed',originalQueryLimitReproduced=True,originalFixtureSha256=fixture['sha256'],originalMaxOperations=100000000)
finally:
    (HERE/'original-control.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS original100million-operation rejection reproduced; current3146 result separately qualified')
