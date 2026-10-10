#!/usr/bin/env python3
"""Bounded known-source PI checks. Prefixes are immutable; failures are retained."""
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
REPORT = Path(__file__).resolve().parent
CLAIM = '0a64aeb75f16d3670c2a165aba8e2d877db459f1'
TASK = 'M2-tree-allocation-probe'
PI = 'tests/Xemele.Tests/M1-tree/TreeTests.cs'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']

def archive_child_files(directories, report):
    """Copy all raw directories before any report JSON is parsed."""
    for directory in directories:
        dest = report / 'child-runs' / directory.name
        dest.mkdir(parents=True, exist_ok=True)
        for path in directory.iterdir():
            if path.is_file():
                (dest / (path.name + '.gz')).write_bytes(gzip.compress(path.read_bytes(), mtime=0))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['report-prefix', 'expected-branch', 'audit-base']:
        parser.add_argument('--' + name, required=True)
    for name in ['runtime-count', 'conformance-count', 'differential-count', 'api-count', 'fixture-count', 'planning-count', 'task-count']:
        parser.add_argument('--' + name, required=True, type=int)
    args = parser.parse_args()
    if not re.fullmatch('[a-z0-9-]+', args.report_prefix): parser.error('invalid prefix')
    report = REPORT / args.report_prefix; report.mkdir()
    raw = ROOT / 'artifacts' / TASK / args.report_prefix; raw.mkdir(parents=True)
    dotnet = Path(shutil.which('dotnet') or 'dotnet-unavailable').resolve(strict=True)
    env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', XEMELE_DOTNET=str(dotnet),
               XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',
               XEMELE_CORPUS_RESULTS=str(raw / 'case-results'))
    child_root = ROOT / 'artifacts/M2-tree-allocation-probe/child-runs'
    child_before = set(child_root.iterdir()) if child_root.exists() else set()
    logs, failures = [], []
    summary = dict(result='failed', host=platform.platform(), architecture=platform.machine(),
                   timing='not run', runtimeSettings='default JIT/GC/test parallelism', arguments=vars(args), checks={})
    def run(*command):
        command = list(map(str, command))
        if command[0] == 'dotnet': command[0] = str(dotnet)
        print('$ ' + shlex.join(command), flush=True)
        p = subprocess.run(command, cwd=ROOT, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=300)
        logs.append('$ ' + shlex.join(command) + '\n' + p.stdout + f'\nexit={p.returncode}\n')
        print(p.stdout[-1600:], flush=True)
        if p.returncode: raise AssertionError(logs[-1])
        return p.stdout
    def require(condition, detail):
        if not condition: raise AssertionError(detail)
    def check(name, action):
        try: summary['checks'][name] = dict(result='passed', detail=action())
        except Exception as e:
            failures.append(name); summary['checks'][name] = dict(result='failed', detail=str(e))
            print('FAILED ' + name + ': ' + str(e)[-1800:], flush=True)
    def audit():
        task = json.loads((ROOT / 'docs/tasks' / (TASK + '.json')).read_text())
        owned = task['owns'] + ['docs/tasks/' + TASK + '.json']
        scopes = {name: run(*command).splitlines() for name, command in {
            'committed': ('git','diff','--name-only', args.audit_base, 'HEAD'),
            'working': ('git','diff','--name-only'), 'staged': ('git','diff','--cached','--name-only'),
            'untracked': ('git','ls-files','--others','--exclude-standard')}.items()}
        require(all(any(p.startswith(o) if o.endswith('/') else p == o for o in owned) for paths in scopes.values() for p in paths), scopes)
        source = [PI, 'tests/Xemele.Tests/' + TASK + '/', str(REPORT.relative_to(ROOT) / 'Diagnostics'), str(Path(__file__).relative_to(ROOT))]
        require(not run('git','diff','--name-only','HEAD','--',*source).strip(), 'uncommitted source')
        require(not run('git','ls-files','--others','--exclude-standard','--',*source).strip(), 'untracked source')
        original = run('git','show',CLAIM + ':' + PI)
        current = (ROOT / PI).read_text()
        # Only imports, the allocation Fact and its helper may change.
        begin = '    [Fact]\n    public void WarmTraversalAndRepeatedNameInternProbesAllocateNothing'
        end = '    [Fact]\n    public void HandlesKeepTheirOwnerAlive'
        require(original[original.index('public class'):original.index(begin)] == current[current.index('public class'):current.index(begin)], 'earlier test changed')
        require(original[original.index(end):] == current[current.index(end):], 'other test changed')
        for suffix in [[args.audit_base, 'HEAD'], [], ['--cached']]: run('git','diff','--check',*suffix)
        (report / 'audit.json').write_text(json.dumps(scopes,indent=2)+'\n')
        return scopes
    def direct(assembly, count):
        xml = raw / (assembly + '.xml')
        error = None
        try: run('dotnet',ROOT / f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll','-noColor','-result-xml',xml)
        except Exception as e: error = str(e)
        if xml.exists():
            (report / (assembly + '.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(),mtime=0))
            tests = list(ET.parse(xml).iter('test'))
            data = dict(total=len(tests),failed=sum(t.get('result') == 'Fail' for t in tests),skipped=sum(t.get('result') == 'Skip' for t in tests))
            data['pi'] = [dict(result=t.get('result'),output=t.findtext('output')) for t in tests if t.get('name','').endswith('WarmTraversalAndRepeatedNameInternProbesAllocateNothing')]
            (report / (assembly + '-counts.json')).write_text(json.dumps(data,indent=2)+'\n')
            require(error is None and len(tests)==count and all(t.get('result')=='Pass' for t in tests), dict(counts=data,error=error))
            return data
        raise AssertionError(error or 'missing XML')
    def children():
        directories = sorted(set(child_root.iterdir()) - child_before) if child_root.exists() else []
        details=[]
        # Two passes: malformed JSON in any pair cannot prevent copying later pairs.
        archive_child_files(directories, report)
        for directory in directories:
            pair={}
            for mode in ['real','positive']:
                path=directory / (mode+'.report.json')
                if path.exists(): pair[mode]=json.loads(path.read_text())
            details.append(dict(directory=directory.name,reports=pair))
        (report / 'child-provenance.json').write_text(json.dumps(details,indent=2)+'\n')
        require(len(directories)==3,'focused/cumulative/direct must each run one real+positive pair: '+str(len(directories)))
        for pair in details:
            reports=pair['reports']; require(set(reports)=={'real','positive'},pair)
            for mode,data in reports.items():
                require(data['Version']==1 and data['Mode']==mode and revision in data['Actual']['TestVersion'] and revision in data['Actual']['ProductVersion'] and data['Actual']['RuntimeVersion']=='10.0.11',data)
                require(data['WarmIterations']==1000 and data['MeasuredIterations']==1000 and data['WarmConsumed']==21000 and data['MeasuredConsumed']==21000 and data['AttributeIdentity'] is True,data)
                require(data['AllocatedBytes']==0 if mode=='real' else data['AllocatedBytes']>0 and data['EscapedObjectLength']==37,data)
                directory=child_root / pair['directory']
                require(json.loads((directory / (mode+'.exit.json')).read_text())['exitCode']==0,'child exit')
                require(json.loads((directory / (mode+'.request.json')).read_text())['Expected']==data['Actual'],'child compiled/runtime provenance')
        return details
    try:
        revision = run('git','rev-parse','HEAD').strip(); summary['sourceRevision'] = revision
        require(run('git','branch','--show-current').strip()==args.expected_branch,'wrong branch')
        run('git','merge-base','--is-ancestor',CLAIM,'HEAD'); run('git','merge-base','--is-ancestor',args.audit_base,'HEAD')
        for dependency in ['M1-tree','M2-pi-allocation-probe']:
            require(json.loads((ROOT / f'docs/tasks/{dependency}.json').read_text())['status']=='verified',dependency)
        audit(); check('archive-regression', lambda: run('python3','-B','-m','unittest','discover','-s',REPORT,'-p','test_archive.py','-v')); require(run('dotnet','--version').strip()=='10.0.400','SDK')
        summary['dotnetInfo'] = run('dotnet','--info')
        summary['sourceHashes'] = {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [ROOT / PI, ROOT / 'tests/Xemele.Tests/M2-tree-allocation-probe/TreeAllocationProbe.cs', ROOT / 'tests/Xemele.Tests/M2-tree-allocation-probe/TreeAllocationChild.cs', REPORT / 'Diagnostics/Program.cs']}
        run('python3','-B','docs/reports/M1-corpus-import/provision.py')
        run('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS)
        runner = ROOT / 'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
        shutil.copytree(runner.parent,raw / 'pinned-reference'); pinned=raw / 'pinned-reference' / runner.name
        env['XEMELE_BCL_RUNNER'] = str(pinned); runner_hash=hashlib.sha256(pinned.read_bytes()).hexdigest()
        run('dotnet','build','Xemele.slnx',*FLAGS)
        run('dotnet','build',REPORT / 'Diagnostics',*FLAGS)
        for mode in ['original','equivalent','helper']:
            def diagnostic(mode=mode):
                data=json.loads(run('dotnet',REPORT / 'Diagnostics/bin/Release/net10.0/Xemele.SyntaxBenchmarks.dll',mode).strip().splitlines()[-1])
                (report / (mode+'-allocations.json')).write_text(json.dumps(data,indent=2)+'\n')
                require(data['runtime']=='10.0.11' and data['warmIterations']==1000 and data['measuredIterations']==1000 and data['warmConsumed']==21000 and data['measuredConsumed']==21000 and data['attributeIdentity'] is True,data)
                if mode=='helper': require(data['primitiveBytes']==0,data)
                return data
            check('diagnostic-'+mode,diagnostic)
        def focused():
            output=run('dotnet','test','--project','tests/Xemele.Tests','-c','Release','--no-build','--no-restore','--filter-method','*WarmTraversalAndRepeatedNameInternProbesAllocateNothing*')
            require(re.search(r'total:\s*1\b',output) is not None,'focused must execute one test')
            return output
        check('focused-mtp',focused)
        def cumulative():
            output=run('dotnet','test','--solution','Xemele.slnx','-c','Release','--no-build','--no-restore')
            require(re.search(r'total:\s*'+str(args.runtime_count+args.conformance_count+args.differential_count)+r'\b',output) is not None,'cumulative count')
            return output
        check('cumulative-mtp',cumulative)
        for assembly,count in [('Xemele.Tests',args.runtime_count),('Xemele.Conformance.Tests',args.conformance_count),('Xemele.Differential.Tests',args.differential_count)]:
            check('direct-'+assembly,lambda assembly=assembly,count=count: direct(assembly,count))
        def provenance():
            records=[json.loads(p.read_text()) for p in (raw / 'case-results').glob('reference-*.json')]
            require(len(records)==242 and len({r['id'] for r in records})==242,'reference count')
            require(hashlib.sha256(pinned.read_bytes()).hexdigest()==runner_hash,'runner changed')
            require(all(r['runnerSha256']==runner_hash and r['response']['engine']['build']=='1.0.0+'+revision for r in records),'stale runner provenance')
            data=dict(records=242,sourceRevision=revision,runnerSha256=runner_hash,runner=str(pinned),dotnet=str(dotnet))
            (report / 'reference-provenance.json').write_text(json.dumps(data,indent=2)+'\n'); return data
        check('reference-provenance',provenance)
        check('child-provenance',children)
        def negative():
            requests=sorted(p for d in (set(child_root.iterdir()) - child_before) for p in d.glob('real.request.json'))
            require(bool(requests), 'no valid child request for bounded negative controls')
            return run('python3','-B', REPORT / 'protocol_negative.py', '--report-prefix', args.report_prefix+'-protocol-negative', '--known-request', requests[0], '--source-revision', revision)
        check('protocol-negatives', negative)
        run('dotnet','build','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj',*FLAGS)
        def api():
            data=json.loads(run('python3','-B','tools/ApiBaseline/compare.py','--report',report / 'runtime-api.json'))
            require(data['members']==args.api_count,data);return data
        check('compiled-api',api)
        def discovery():
            data=json.loads(run('python3','-B','tools/ApiBaseline/discover.py'))
            require(data['apiDeclarations']==args.api_count and data['fixtures']==args.fixture_count,data);return data
        check('discovery',discovery)
        def planning():
            output=run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v')
            require(f'Ran {args.planning_count} tests' in output,'planning count');return output
        check('planning-regressions',planning)
        def validation():
            output=run('python3','-B','tools/planning/tasks.py','validate')
            require(f'Validated {args.task_count} tasks' in output,'task count');return output
        check('planning-validation',validation);check('final-audit',audit)
        summary['result']='failed' if failures else 'passed';summary['failedChecks']=failures
        summary['scope']='Isolated synchronous Tree traversal/borrowed intern primitive; exact0B/no concurrent allocation guarantee. Full joint MTP/direct suites mandatory; historical failure causes unresolved.'
    finally:
        directories = sorted(set(child_root.iterdir()) - child_before) if child_root.exists() else []
        archive_child_files(directories, report)
        (report / 'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0))
        (report / 'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    return bool(failures)

if __name__ == '__main__': raise SystemExit(main())
