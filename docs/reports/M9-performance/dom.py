"""Measure held versus released managed DOM graphs on the complete frozen corpus."""
import sys
sys.dont_write_bytecode = True
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import statistics
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--attempt', required=True)
    args = parser.parse_args()
    host = Path(args.dotnet).resolve()
    out = ROOT / 'artifacts/M9-performance' / ('dom-' + args.attempt)
    out.mkdir(parents=True)
    spec = importlib.util.spec_from_file_location('supervision', ROOT / 'docs/reports/M8-xsd-corpus/supervision.py')
    supervisor = importlib.util.module_from_spec(spec); spec.loader.exec_module(supervisor)
    supervisor.ROOT, supervisor.ARTIFACTS = ROOT, out
    environment = dict(os.environ, DOTNET_ROOT=str(host.parent), DOTNET_ROLL_FORWARD='Disable',
                       DOTNET_TieredCompilation='0', DOTNET_TieredPGO='0', DOTNET_ReadyToRun='0',
                       DOTNET_gcServer='0', DOTNET_GCConcurrent='0', PYTHONDONTWRITEBYTECODE='1')
    manifest_path = ROOT / 'benchmarks/baselines/release/fixtures.json'
    manifest = json.loads(manifest_path.read_bytes())
    report = dict(revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
                  fixtureManifestSha256=hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
                  result='failed', commands=[], rows=[], controlledTimingCredit=False,
                  environment={k:v for k,v in environment.items() if k.startswith('DOTNET_')})
    deadline = time.monotonic() + 1800
    def run(argv, cap):
        index = len(report['commands']); end = min(deadline, time.monotonic() + cap)
        receipt = supervisor.supervise_command(list(map(str,argv)), index, environment, end, end+10)
        log = out / f'command-{index}.log'
        row = dict(argv=list(map(str,argv)), receipt=receipt, outputSha256=hashlib.sha256(log.read_bytes()).hexdigest())
        report['commands'].append(row)
        if not supervisor.workload_succeeded(receipt):
            row['failureTail'] = log.read_text(errors='replace')[-4000:]; raise RuntimeError(row['failureTail'])
    try:
        project = HERE / 'ManagedWorker/ManagedWorker.csproj'
        run([host,'build',project,'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false'],300)
        binary = HERE / 'ManagedWorker/bin/Release/net10.0/ManagedWorker.dll'
        for fixture in manifest['fixtures']:
            pair = {}
            for engine in ['xemele','bcl']:
                target = out / (fixture['id']+'-'+engine+'.json')
                run([host,'--fx-version','10.0.11',binary,engine,manifest_path.parent/fixture['path'],manifest_path,target],180)
                row = json.loads(target.read_bytes())
                assert row['controlLive'] >= 9*1024*1024 and row['runtime'] == '.NET 10.0.11' and not row['qualificationExecuted']
                pair[engine] = row
                print('PASS',fixture['id'],engine,flush=True)
            candidate = statistics.median(r['retainedBytes'] for r in pair['xemele']['rows'])
            reference = statistics.median(r['retainedBytes'] for r in pair['bcl']['rows'])
            report['rows'].append(dict(fixture=fixture['id'],candidateMedianRetainedBytes=candidate,
                                      bclMedianRetainedBytes=reference,ratio=candidate/reference,actual=pair))
            (HERE/'dom-retention.json').write_text(json.dumps(report,indent=2)+'\n')
            print('RATIO',fixture['id'],candidate/reference,flush=True)
        report.update(aggregateRatio=sum(r['candidateMedianRetainedBytes'] for r in report['rows'])/sum(r['bclMedianRetainedBytes'] for r in report['rows']),
                      maximumRatioRequired=0.75,result='measurement-passed')
        report['targetPassed'] = report['aggregateRatio'] <= 0.75
        print('AGGREGATE',report['aggregateRatio'],report['targetPassed'],flush=True)
    finally:
        (HERE/'dom-retention.json').write_text(json.dumps(report,indent=2)+'\n')


if __name__ == '__main__': main()
