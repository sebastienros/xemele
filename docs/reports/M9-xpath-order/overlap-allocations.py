"""Run identical ShortRun memory-diagnosed workloads against frozen and repaired DOMs."""
import sys
sys.dont_write_bytecode = True
import argparse, hashlib, importlib.util, json, os, shutil, subprocess, tarfile, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
BASE = '2d38e67fc809341fef1cdd2f9aeb9f6ef13e1059'
host = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
out=ROOT/'artifacts/M9-xpath-order/overlap-allocations'
out.mkdir(parents=True,exist_ok=False)
candidateRevision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
stages={}; archives={}
for label,revision in [('baseline',BASE),('candidate',candidateRevision)]:
    stage=out/label;stage.mkdir();archive=out/(label+'.tar')
    with archive.open('wb') as target:
        subprocess.run(['git','archive',revision,'Directory.Build.props','Directory.Packages.props','global.json','src/Xemele'],
                       cwd=ROOT,stdout=target,check=True,timeout=30)
    with tarfile.open(archive) as source:source.extractall(stage,filter='data')
    for relative in ['docs/reports/M9-xpath-order/BdnWorker','benchmarks/Xemele.Benchmarks/M9-xpath-order']:
        shutil.copytree(ROOT/relative,stage/relative,ignore=shutil.ignore_patterns('bin','obj'))
    stages[label]=stage;archives[label]=hashlib.sha256(archive.read_bytes()).hexdigest()
spec = importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
supervisor = importlib.util.module_from_spec(spec); spec.loader.exec_module(supervisor)
supervisor.ARTIFACTS = out
env = dict(os.environ, PATH=str(host.parent)+os.pathsep+os.environ.get('PATH',''), DOTNET_ROOT=str(host.parent), DOTNET_ROLL_FORWARD='Disable',
           DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',
           DOTNET_gcServer='0',DOTNET_GCConcurrent='0',NuGetAudit='false',PYTHONDONTWRITEBYTECODE='1')
report=dict(baselineRevision=BASE,candidateRevision=candidateRevision,result='failed',controlledTimingCredit=False,
            commands=[],summaries={},sourceArchives=archives,
            benchmarkSha256=hashlib.sha256((ROOT/'benchmarks/Xemele.Benchmarks/M9-xpath-order/XPathOverlapBenchmarks.cs').read_bytes()).hexdigest(),
            environment={k:v for k,v in env.items() if k.startswith('DOTNET_')})
deadline = time.monotonic()+1500
try:
    for label, root in stages.items():
        supervisor.ROOT=root
        project=root/'docs/reports/M9-xpath-order/BdnWorker/Xemele.Benchmarks.csproj'
        results=out/(label+'-bdn')
        commands=[
            ([host,'restore',project,'--source','/private/tmp','-p:NuGetAudit=false','--disable-build-servers'],180),
            ([host,'build',project,'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false'],180),
            ([host,'--fx-version','10.0.11',project.parent/'bin/Release/net10.0/Xemele.Benchmarks.dll','--filter','*XPathOverlapBenchmarks*','--artifacts',results,'--exporters','json'],550)]
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
        assert sum('ReverseAndOverlappingMerge' in line and line.startswith('|') for line in text.splitlines())==2 and ' NA ' not in text
        report['summaries'][label]=text
        print(text,flush=True)

    report['result']='passed'
finally:
    (HERE/'overlap-allocations.json').write_text(json.dumps(report,indent=2)+'\n')
