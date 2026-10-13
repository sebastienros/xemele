"""Finite owned-process correctness checks for the DOM storage repair."""
import sys
sys.dont_write_bytecode = True
import argparse, hashlib, importlib.util, json, os, subprocess, time
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--phase', required=True)
args = parser.parse_args()
host = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
out = ROOT / 'artifacts/M9-dom-memory' / ('checks-' + args.phase)
out.mkdir(parents=True, exist_ok=True)
spec = importlib.util.spec_from_file_location('supervision', ROOT / 'docs/reports/M8-xsd-corpus/supervision.py')
supervisor = importlib.util.module_from_spec(spec); spec.loader.exec_module(supervisor)
supervisor.ROOT, supervisor.ARTIFACTS = ROOT, out
env = dict(os.environ, DOTNET_ROOT=str(host.parent), DOTNET_ROLL_FORWARD='Disable',
           DOTNET_TieredCompilation='0', DOTNET_TieredPGO='0', DOTNET_ReadyToRun='0',
           DOTNET_gcServer='0', DOTNET_GCConcurrent='0', XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources')
report = dict(revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
              phase=args.phase, result='failed', commands=[])
commands = [
    ([host,'restore','tests/Xemele.Tests/Xemele.Tests.csproj','--source','/private/tmp','-p:NuGetAudit=false','-p:RuntimeFrameworkVersion=10.0.11','--disable-build-servers'],300),
    ([host,'build','tests/Xemele.Tests/Xemele.Tests.csproj','-c','Release','-t:Rebuild','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:RuntimeFrameworkVersion=10.0.11'],300),
    ([host,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-parallelMode','none','-culture','invariant','-noColor','-result-xml',out/'tests.xml'],300),
    (['python3','tools/planning/tasks.py','validate'],30),
]
deadline = time.monotonic() + 1000
try:
    for argv, cap in commands:
        if len(report['commands']) == 2:
            config = json.loads((ROOT/'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.runtimeconfig.json').read_bytes())
            assert config['runtimeOptions']['framework']['version'] == '10.0.11', config
        index = len(report['commands']); end = min(deadline,time.monotonic()+cap)
        argv = list(map(str,argv))
        receipt = supervisor.supervise_command(argv,index,env,end,end+10)
        log = out / f'command-{index}.log'
        row = dict(argv=argv,receipt=receipt,outputSha256=hashlib.sha256(log.read_bytes()).hexdigest(),
                   outputTail=log.read_text(errors='replace')[-4000:])
        report['commands'].append(row)
        print(row['outputTail'],flush=True)
        if not supervisor.workload_succeeded(receipt): raise RuntimeError('Owned check failed')
    report['result']='passed'
finally:
    (HERE/('checks-'+args.phase+'.json')).write_text(json.dumps(report,indent=2)+'\n')
