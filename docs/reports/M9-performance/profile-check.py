"""Qualify only the prior input-profile rejection; preserve all nine valid qualifications."""
import sys
sys.dont_write_bytecode=True
import hashlib,importlib.util,json,os,subprocess,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent
host=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
out=ROOT/'artifacts/M9-performance/profile-check';out.mkdir(parents=True,exist_ok=False)
spec=importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
supervisor=importlib.util.module_from_spec(spec);spec.loader.exec_module(supervisor)
supervisor.ROOT,supervisor.ARTIFACTS=ROOT,out
env=dict(os.environ,DOTNET_ROOT=str(host.parent),DOTNET_ROLL_FORWARD='Disable',
         DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0')
old=json.loads((ROOT/'docs/reports/M9-xpath-order/qualification.json').read_bytes())
assert len(old['rows'])==9 and old['result']=='failed'
assert not subprocess.check_output(['git','diff',old['revision'],'HEAD','--','src/Xemele','benchmarks/baselines/release/fixtures','benchmarks/baselines/release/fixtures.json','Directory.Build.props','Directory.Packages.props','global.json'],cwd=ROOT)
worker='docs/reports/M9-performance/ManagedWorker/Program.cs'
previous=subprocess.check_output(['git','show',old['revision']+':'+worker],cwd=ROOT,text=True)
assert previous.replace('MaxInputCharacters = 16777216','MaxInputCharacters = 33554432')==(ROOT/worker).read_text()
manifest=ROOT/'benchmarks/baselines/release/fixtures.json';fixtures=json.loads(manifest.read_bytes())['fixtures']
assert hashlib.sha256(manifest.read_bytes()).hexdigest()==old['fixtureManifestSha256']
report=dict(result='failed',revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
            fixtureManifestSha256=old['fixtureManifestSha256'],commands=[],rows=old['rows'].copy(),
            reusedNineFrom='docs/reports/M9-xpath-order/qualification.json',originalFailurePreserved=True,
            profileSha256=hashlib.sha256((ROOT/'benchmarks/baselines/release/operations.json').read_bytes()).hexdigest(),controlledTimingCredit=False)
deadline=time.monotonic()+600
def run(argv,cap):
    index=len(report['commands']);end=min(deadline,time.monotonic()+cap);argv=list(map(str,argv))
    receipt=supervisor.supervise_command(argv,index,env,end,end+10);log=out/f'command-{index}.log'
    row=dict(argv=argv,receipt=receipt,outputSha256=hashlib.sha256(log.read_bytes()).hexdigest());report['commands'].append(row)
    if not supervisor.workload_succeeded(receipt):
        row['failureTail']=log.read_text(errors='replace')[-4000:];raise RuntimeError(row['failureTail'])
try:
    project=HERE/'ManagedWorker/ManagedWorker.csproj'
    run([host,'restore',project,'--source','/private/tmp','-p:NuGetAudit=false','--disable-build-servers'],120)
    run([host,'build',project,'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false'],180)
    fixture=next(r for r in fixtures if r['id']=='text-large');target=out/'text-large.json'
    run([host,'--fx-version','10.0.11',project.parent/'bin/Release/net10.0/ManagedWorker.dll','xemele',manifest.parent/fixture['path'],manifest,target,'qualify'],180)
    actual=json.loads(target.read_bytes());assert actual['qualification'] and actual['digest']==fixture['semantic']['sha256'] and actual['expectedXPath']==fixture['xpathExpected']
    report['rows'].append(dict(fixture=fixture['id'],inputSha256=fixture['sha256'],actual=actual))
    assert {r['fixture'] for r in report['rows']}=={r['id'] for r in fixtures}
    report.update(result='passed',qualifiedFixtures=10,excludedFixtures=0)
finally:
    (HERE/'managed-qualification-current.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS all10 unchanged managed fixtures; only prior text-large input-profile rejection repeated')
