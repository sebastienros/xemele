"""Run identical ShortRun memory-diagnosed workloads against frozen and repaired DOMs."""
import sys
sys.dont_write_bytecode = True
import hashlib, importlib.util, json, os, shutil, subprocess, tarfile, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
BASE = '68c7523bf4451152a52e16c522c4a7738c5fe6dc'
host = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
out = ROOT / 'artifacts/M9-dom-memory/allocations'
out.mkdir(parents=True, exist_ok=False)
baseline = out / 'baseline'; baseline.mkdir()
archive = out / 'baseline.tar'
with archive.open('wb') as target:
    subprocess.run(['git','archive',BASE,'Directory.Build.props','Directory.Packages.props','global.json','src/Xemele'],
                   cwd=ROOT, stdout=target, check=True, timeout=30)
with tarfile.open(archive) as source:
    source.extractall(baseline, filter='data')
for relative in ['docs/reports/M9-DOM-memory/BdnWorker','benchmarks/Xemele.Benchmarks/M9-DOM-memory']:
    shutil.copytree(ROOT/relative,baseline/relative,ignore=shutil.ignore_patterns('bin','obj'))
spec = importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
supervisor = importlib.util.module_from_spec(spec); spec.loader.exec_module(supervisor)
supervisor.ARTIFACTS = out
env = dict(os.environ, PATH=str(host.parent)+os.pathsep+os.environ.get('PATH',''), DOTNET_ROOT=str(host.parent), DOTNET_ROLL_FORWARD='Disable',
           DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',
           DOTNET_gcServer='0',DOTNET_GCConcurrent='0',NuGetAudit='false',PYTHONDONTWRITEBYTECODE='1')
report = dict(baselineRevision=BASE,candidateRevision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
              result='failed',controlledTimingCredit=False,commands=[],summaries={},
              baselineArchiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
              benchmarkSha256=hashlib.sha256((ROOT/'benchmarks/Xemele.Benchmarks/M9-DOM-memory/DomStorageBenchmarks.cs').read_bytes()).hexdigest(),
              environment={k:v for k,v in env.items() if k.startswith('DOTNET_')})
deadline = time.monotonic()+1500
try:
    for label, root in [('baseline',baseline),('candidate',ROOT)]:
        supervisor.ROOT=root
        project=root/'docs/reports/M9-DOM-memory/BdnWorker/BdnWorker.csproj'
        results=out/(label+'-bdn')
        commands=[
            ([host,'restore',project,'--source','/private/tmp','-p:NuGetAudit=false','--disable-build-servers'],180),
            ([host,'build',project,'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false'],180),
            ([host,'--fx-version','10.0.11',project.parent/'bin/Release/net10.0/BdnWorker.dll','--filter','*DomStorageBenchmarks*','--artifacts',results,'--exporters','json'],550)]
        for argv,cap in commands:
            index=len(report['commands']); end=min(deadline,time.monotonic()+cap)
            argv=list(map(str,argv)); receipt=supervisor.supervise_command(argv,index,env,end,end+10)
            log=out/f'command-{index}.log'
            row=dict(label=label,argv=argv,receipt=receipt,outputSha256=hashlib.sha256(log.read_bytes()).hexdigest())
            report['commands'].append(row)
            if not supervisor.workload_succeeded(receipt):
                row['failureTail']=log.read_text(errors='replace')[-4000:]; raise RuntimeError(row['failureTail'])
            print('PASS',label,argv[1],flush=True)
        markdown=list((results/'results').glob('*-report-github.md'))
        assert len(markdown)==1
        text=markdown[0].read_text()
        assert text.count('ParseOwnedDom')==4 and 'NA' not in text
        report['summaries'][label]=text
        print(text,flush=True)
    report['result']='passed'
finally:
    (HERE/'allocations.json').write_text(json.dumps(report,indent=2)+'\n')
