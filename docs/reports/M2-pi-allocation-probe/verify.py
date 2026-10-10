#!/usr/bin/env python3
"""Bounded fresh PI measurement and cumulative checks; each prefix is immutable."""
import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'docs/reports/M2-pi-allocation-probe'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
CLAIM = 'c85e5994b3e82f7340849dd915b08ef1a84ece17'
PI = 'Xemele.Tests.PiTargets.PiTargetTests.RepeatedBorrowedPiTargetsAllocateZeroAfterWarmup'

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['report-prefix', 'expected-branch', 'audit-base']:
        parser.add_argument('--' + name, required=True)
    for name in ['runtime-count', 'conformance-count', 'differential-count', 'api-count', 'fixture-count', 'planning-count', 'task-count']:
        parser.add_argument('--' + name, required=True, type=int)
    args = parser.parse_args()
    if not re.fullmatch('[a-z0-9-]+', args.report_prefix): parser.error('invalid prefix')
    report = REPORT / args.report_prefix
    report.mkdir()  # Fail on reuse, including unsuccessful attempts.
    raw = ROOT / 'artifacts/M2-pi-allocation-probe' / args.report_prefix
    raw.mkdir(parents=True)
    dotnet = Path(shutil.which('dotnet') or 'dotnet-unavailable').resolve(strict=True)
    env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', XEMELE_DOTNET=str(dotnet),
               XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',
               XEMELE_CORPUS_RESULTS=str(raw / 'case-results'))
    logs, failures = [], []
    summary = dict(result='failed', host=platform.platform(), architecture=platform.machine(),
                   timing='not run', runtimeSettings='default JIT/GC/test parallelism', counts=vars(args), checks={})
    def run(*command, expected=0):
        command = list(map(str, command))
        if command[0] == 'dotnet': command[0] = str(dotnet)
        print('$ ' + shlex.join(command), flush=True)
        result = subprocess.run(command, cwd=ROOT, env=env, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=300)
        logs.append('$ ' + shlex.join(command) + '\n' + result.stdout + f'\nexit={result.returncode}; expected={expected}\n')
        print(result.stdout[-1400:], flush=True)
        if result.returncode != expected: raise AssertionError(logs[-1])
        return result.stdout
    def check(name, action):
        try:
            summary['checks'][name] = {'result': 'passed', 'detail': action()}
        except Exception as error:
            failures.append(name)
            summary['checks'][name] = {'result': 'failed', 'detail': str(error)}
            print('FAILED ' + name + ': ' + str(error)[-1400:], flush=True)
    def require(condition, detail):
        if not condition: raise AssertionError(detail)
    def audit():
        owned = json.loads((ROOT / 'docs/tasks/M2-pi-allocation-probe.json').read_text())['owns'] + ['docs/tasks/M2-pi-allocation-probe.json']
        commands = {
            'committed': ('git', 'diff', '--name-only', args.audit_base, 'HEAD'),
            'working': ('git', 'diff', '--name-only'),
            'staged': ('git', 'diff', '--cached', '--name-only'),
            'untracked': ('git', 'ls-files', '--others', '--exclude-standard')}
        scopes = {name: run(*command).splitlines() for name, command in commands.items()}
        for paths in scopes.values():
            require(all(any(p.startswith(o) if o.endswith('/') else p == o for o in owned) for p in paths), paths)
        source = ['tests/Xemele.Tests/M1-pi-targets/PiTargetTests.cs', 'tests/Xemele.Tests/M2-pi-allocation-probe/', 'docs/reports/M2-pi-allocation-probe/Diagnostics/', 'docs/reports/M2-pi-allocation-probe/verify.py']
        require(not run('git', 'diff', '--name-only', 'HEAD', '--', *source).strip(), 'uncommitted source')
        require(not run('git', 'ls-files', '--others', '--exclude-standard', '--', *source).strip(), 'untracked source')
        run('git', 'diff', '--check', args.audit_base, 'HEAD')
        run('git', 'diff', '--check'); run('git', 'diff', '--cached', '--check')
        (report / 'audit.json').write_text(json.dumps(scopes, indent=2) + '\n')
        return scopes
    def direct(assembly, count):
        xml = raw / (assembly + '.xml')
        try:
            run('dotnet', ROOT / f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll', '-noColor', '-result-xml', xml)
        finally:
            if xml.exists():
                (report / (assembly + '.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(), mtime=0))
        tests = list(ET.parse(xml).iter('test'))
        require(len(tests) == count and all(t.get('result') == 'Pass' for t in tests), (assembly, len(tests)))
        return dict(total=len(tests), failed=0, skipped=0)
    try:
        revision = run('git', 'rev-parse', 'HEAD').strip(); summary['sourceRevision'] = revision
        require(run('git', 'branch', '--show-current').strip() == args.expected_branch, 'wrong branch')
        run('git', 'merge-base', '--is-ancestor', CLAIM, 'HEAD')
        run('git', 'merge-base', '--is-ancestor', args.audit_base, 'HEAD')
        for dependency in ['M2-contracts', 'M1-pi-targets']:
            require(json.loads((ROOT / f'docs/tasks/{dependency}.json').read_text())['status'] == 'verified', dependency)
        audit()
        require(run('dotnet', '--version').strip() == '10.0.400', 'wrong SDK')
        summary['dotnetInfo'] = run('dotnet', '--info')
        summary['runtimeDllSourceSha256'] = hashlib.sha256((ROOT / 'tests/Xemele.Tests/M1-pi-targets/PiTargetTests.cs').read_bytes()).hexdigest()
        run('python3', '-B', 'docs/reports/M1-corpus-import/provision.py')
        run('dotnet', 'build', 'tools/ReferenceRunners/Bcl/BclReference.csproj', *FLAGS)
        runner = ROOT / 'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
        shutil.copytree(runner.parent, raw / 'pinned-reference')
        pinned = raw / 'pinned-reference' / runner.name
        env['XEMELE_BCL_RUNNER'] = str(pinned)
        runner_hash = hashlib.sha256(pinned.read_bytes()).hexdigest()
        run('dotnet', 'build', 'Xemele.slnx', *FLAGS)
        run('dotnet', 'build', REPORT / 'Diagnostics/Diagnostics.csproj', *FLAGS)
        for mode in ['original', 'equivalent']:
            def diagnostic(mode=mode):
                output = run('dotnet', REPORT / 'Diagnostics/bin/Release/net10.0/Xemele.SyntaxBenchmarks.dll', mode)
                data = json.loads(output.strip().splitlines()[-1])
                (report / (mode + '-allocations.json')).write_text(json.dumps(data, indent=2) + '\n')
                return data
            check('diagnostic-' + mode, diagnostic)
        def focused():
            output = run('dotnet', 'test', '--project', 'tests/Xemele.Tests', '-c', 'Release', '--no-build', '--no-restore', '--filter-method', PI)
            require(re.search(r'total:\s*1\b', output) is not None, 'focused discovery must execute exactly one test')
            return output
        check('focused-mtp', focused)
        check('cumulative-mtp', lambda: run('dotnet', 'test', '--solution', 'Xemele.slnx', '-c', 'Release', '--no-build', '--no-restore'))
        for assembly, count in [('Xemele.Tests', args.runtime_count), ('Xemele.Conformance.Tests', args.conformance_count), ('Xemele.Differential.Tests', args.differential_count)]:
            check('direct-' + assembly, lambda assembly=assembly, count=count: direct(assembly, count))
        def provenance():
            records = [json.loads(p.read_text()) for p in (raw / 'case-results').glob('reference-*.json')]
            require(len(records) == 242 and len({r['id'] for r in records}) == 242, 'reference record count')
            require(hashlib.sha256(pinned.read_bytes()).hexdigest() == runner_hash, 'reference runner changed')
            require(all(r['runnerSha256'] == runner_hash and r['response']['engine']['build'] == '1.0.0+' + revision for r in records), 'stale reference provenance')
            data = dict(records=242, sourceRevision=revision, runnerSha256=runner_hash, runner=str(pinned), dotnet=str(dotnet))
            (report / 'reference-provenance.json').write_text(json.dumps(data, indent=2) + '\n')
            return data
        check('reference-provenance', provenance)
        run('dotnet', 'build', 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', *FLAGS)
        def api():
            output = run('python3', '-B', 'tools/ApiBaseline/compare.py', '--report', report / 'runtime-api.json')
            require(json.loads(output)['members'] == args.api_count, 'API count')
            return json.loads(output)
        check('compiled-api', api)
        def discovery():
            data = json.loads(run('python3', '-B', 'tools/ApiBaseline/discover.py'))
            require(data['apiDeclarations'] == args.api_count and data['fixtures'] == args.fixture_count, data)
            return data
        check('discovery', discovery)
        def planning():
            output = run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tools/planning', '-p', 'test_tasks.py', '-v')
            require(f'Ran {args.planning_count} tests' in output, 'planning count')
            return output
        check('planning-regressions', planning)
        def validation():
            output = run('python3', '-B', 'tools/planning/tasks.py', 'validate')
            require(f'Validated {args.task_count} tasks' in output, 'task count')
            return output
        check('planning-validation', validation)
        check('final-audit', audit)
        summary['result'] = 'failed' if failures else 'passed'
        summary['failedChecks'] = failures
    finally:
        (report / 'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(), mtime=0))
        (report / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    return bool(failures)

if __name__ == '__main__': raise SystemExit(main())
