"""Current non-fuzz generated-stream live-memory requalification."""
import sys
sys.dont_write_bytecode = True
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
OUT = ROOT / 'artifacts/M9-performance/current-memory2'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', required=True)
    args = parser.parse_args()
    host = Path(args.dotnet).resolve()
    OUT.mkdir(parents=True)
    spec = importlib.util.spec_from_file_location('supervision', ROOT / 'docs/reports/M8-xsd-corpus/supervision.py')
    supervisor = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(supervisor)
    supervisor.ROOT, supervisor.ARTIFACTS = ROOT, OUT
    environment = dict(os.environ, DOTNET_ROOT=str(host.parent), DOTNET_ROLL_FORWARD='Disable',
                       DOTNET_TieredCompilation='0', DOTNET_TieredPGO='0', DOTNET_ReadyToRun='0',
                       DOTNET_gcServer='0', DOTNET_GCConcurrent='0', PYTHONDONTWRITEBYTECODE='1')
    report = dict(revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                  result='failed', commands=[], rows=[], environment={k: v for k, v in environment.items() if k.startswith('DOTNET_')},
                  hostSha256=sha(host), frozenProfileSha256=sha(ROOT / 'tests/corpora/streaming/generated-profile.json'),
                  originalRunnerSourceSha256=sha(ROOT / 'docs/reports/M3-corpus/Runner/Program.cs'),
                  fuzzExecuted=False, dedicatedTimingCredit=False)
    deadline = time.monotonic() + 1200

    def run(argv, seconds):
        index = len(report['commands'])
        end = min(time.monotonic() + seconds, deadline)
        receipt = supervisor.supervise_command(list(map(str, argv)), index, environment, end, end + 10)
        log = OUT / f'command-{index}.log'
        report['commands'].append(dict(argv=list(map(str, argv)), receipt=receipt, outputSha256=sha(log)))
        if not supervisor.workload_succeeded(receipt):
            report['commands'][-1]['failureTail'] = log.read_text(errors='replace')[-4000:]
            raise RuntimeError(f'Command {index} failed: ' + report['commands'][-1]['failureTail'])
        print(f'PASS command {index}', flush=True)
        return log.read_text(errors='replace')

    try:
        assert run([host, '--version'], 10).strip() == '10.0.400'
        project = HERE / 'MemoryRunner/MemoryRunner.csproj'
        run([host, 'restore', project, '--source', '/private/tmp', '--disable-build-servers', '-p:NuGetAudit=false'], 120)
        run([host, 'build', project, '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'], 180)
        dll = HERE / 'MemoryRunner/bin/Release/net10.0/MemoryRunner.dll'
        product = ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll'
        report.update(runnerSha256=sha(dll), productSha256=sha(product))
        for mode, size, cap in [('negative', 8192, 30), ('sax', 1073741824, 180),
                                ('sax-async', 1073741824, 240), ('pull', 1073741824, 360),
                                ('pull-async', 1073741824, 480), ('push', 1073741824, 180)]:
            target = OUT / (mode + '.json')
            # The reused runner's fuzz-control entry point is never invoked.
            output = run(['/usr/bin/time', '-l', host, '--fx-version', '10.0.11', dll, mode, size, target], cap)
            row = json.loads(target.read_bytes())
            assert row['assemblySha256'] == report['productSha256'] and row['runnerSha256'] == report['runnerSha256']
            assert row['inputBytes'] == row['BytesRead'] == size and row['runtime'] == '.NET 10.0.11'
            assert row['withinLimit'] is (mode != 'negative') and row['earlyCallback'] and row['Completed'] == 1
            assert row['conservativeMaximumWholeHeapLiveBytes'] == max(row['liveSamples'])
            if mode != 'negative':
                assert row['Starts'] == row['Ends'] == 262144 and row['Characters'] == 1070333958
                assert len(row['liveSamples']) == 65 and row['parserLiveUpperBoundBytes'] <= 8388608
            rss = [line.split()[0] for line in output.splitlines() if 'maximum resident set size' in line]
            row['externalPeakResidentSetBytes'] = int(rss[0]) if len(rss) == 1 else None
            report['rows'].append(row)
            print(f"PASS {mode}: {row['parserLiveUpperBoundBytes']} conservative managed live bytes", flush=True)
            (HERE / 'current-memory2.json').write_text(json.dumps(report, indent=2) + '\n')
        report['result'] = 'passed'
    finally:
        (HERE / 'current-memory2.json').write_text(json.dumps(report, indent=2) + '\n')


if __name__ == '__main__':
    main()
