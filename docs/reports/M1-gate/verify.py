"""M1 strict XML checkpoint; isolated outputs, no fixture publication or timings."""
import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import platform
import shlex
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[3]
REPORT_ROOT = ROOT / 'docs/reports/M1-gate'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']


def pinned_environment(inherited, raw, dotnet, runner):
    # Caller convenience overrides cannot choose the evidence-producing engine.
    return dict(inherited, DOTNET_CLI_USE_MSBUILD_SERVER='0',
                PATH=str(dotnet.parent)+os.pathsep+inherited.get('PATH',''),
                XEMELE_DOTNET=str(dotnet), XEMELE_BCL_RUNNER=str(runner),
                XEMELE_CORPUS_RESULTS=str(raw/'case-results'),
                XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/')


def validate_reference_provenance(records, runner_hash, revision):
    for record in records:
        assert record['runnerSha256'] == runner_hash, 'Reference runner hash differs from freshly built checkout'
        assert record['response']['engine']['build'] == '1.0.0+'+revision, 'Reference adapter build is not the tested source revision'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit-base', default='ed13ab8d34d42618944f6a7c6fc24634c787264e')
    parser.add_argument('--report-prefix', required=True, help='new owned directory; existing reports cannot be overwritten')
    args = parser.parse_args()
    if not args.report_prefix.replace('-', '').isalnum(): parser.error('invalid report prefix')
    report = REPORT_ROOT / args.report_prefix
    report.mkdir()  # Every attempt, including failure, remains visible.
    raw = ROOT / 'artifacts/M1-gate' / args.report_prefix
    raw.mkdir(parents=True)
    logs = []
    dotnet = Path(shutil.which('dotnet') or 'dotnet-unavailable').resolve(strict=True)
    runner = ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
    env = pinned_environment(os.environ, raw, dotnet, runner)
    def run(*command, cwd=ROOT, timeout=300):
        if command[0] == 'dotnet': command = (dotnet, *command[1:])
        print('$ ' + shlex.join(map(str, command)), flush=True)
        result = subprocess.run(list(map(str, command)), cwd=cwd, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)
        entry = '$ ' + shlex.join(map(str, command)) + '\n' + result.stdout + f'\nExit:{result.returncode}\n'
        logs.append(entry)
        print(result.stdout[-1800:], flush=True)
        if result.returncode: raise RuntimeError(entry)
        return result.stdout
    def tests(dll, label, expected=None):
        output = raw / (label + '.xml')
        run('dotnet', dll, '-noColor', '-result-xml', output, timeout=300)
        rows = list(ET.parse(output).iter('test'))
        assert rows and (expected is None or len(rows) == expected), (label, len(rows), expected)
        skipped = [t for t in rows if t.get('result') == 'Skip']
        assert all('SymlinkResourceAndRoot' in t.get('name', '') for t in skipped)
        assert all(t.get('result') in ('Pass', 'Skip') for t in rows)
        (report / (label + '.xml.gz')).write_bytes(gzip.compress(output.read_bytes(), mtime=0))
        return dict(total=len(rows), passed=sum(t.get('result') == 'Pass' for t in rows),
                    failed=sum(t.get('result') == 'Fail' for t in rows), skipped=len(skipped),
                    skipReasons=[t.findtext('reason/message') for t in skipped])
    def audit():
        owned = ('docs/reports/M1-gate/', 'docs/tasks/M1-gate.json')
        paths = []
        for command in [('git','diff','--name-only',args.audit_base+'..HEAD'),('git','diff','--name-only'),('git','diff','--cached','--name-only'),('git','ls-files','--others','--exclude-standard')]:
            paths.extend(run(*command).splitlines())
        assert all(p.startswith(owned[0]) or p == owned[1] for p in paths), paths
        for suffix in [args.audit_base+'..HEAD', None, '--cached']:
            run('git', 'diff', '--check', *([suffix] if suffix else []))
        (report / 'owned-paths.json').write_text(json.dumps(sorted(set(paths)), indent=2)+'\n')
    try:
        revision = run('git','rev-parse','HEAD').strip()
        logs.append('Host: '+platform.platform()+' '+platform.machine()+'\nSource: '+revision)
        run('git','merge-base','--is-ancestor',args.audit_base,'HEAD')
        assert run('dotnet','--version').strip() == '10.0.400'
        audit()
        run(sys.executable,'-B','docs/reports/M1-corpus-import/provision.py')
        run(sys.executable,'-B','docs/reports/M1-corpus-import/import_suite.py','--check')
        inventory_before = json.loads((ROOT/'tests/corpora/xml/inventory.json').read_text())
        resource_root = Path(env['XEMELE_CORPUS_RESOURCES']).resolve(strict=True)
        resources = []
        for file in inventory_before['files']:
            path = resource_root / file['path']
            assert path.resolve(strict=True).is_relative_to(resource_root), file['path']
            cursor = path
            while cursor != resource_root:
                assert not cursor.is_symlink(), file['path']
                cursor = cursor.parent
            data = path.read_bytes()
            assert len(data) == file['bytes'] and hashlib.sha256(data).hexdigest() == file['sha256'], file['path']
            resources.append(dict(file, result='verified-original-bytes'))
        assert len(resources) == 3386
        (report/'resource-checks.json').write_text(json.dumps(dict(sourceRevision=revision,resources=resources),indent=2)+'\n')
        run('dotnet','build','Xemele.slnx',*FLAGS)
        run('dotnet','build','docs/reports/M1-corpus/Tests/Tests.csproj',*FLAGS)
        run('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS)
        runner_hash = hashlib.sha256(runner.read_bytes()).hexdigest()
        counts = {'owned':tests(ROOT/'docs/reports/M1-corpus/Tests/bin/Release/net10.0/Xemele.XmlSubset.Tests.dll','owned',501)}
        for name, expected in [('Xemele.Tests',247),('Xemele.Conformance.Tests',255),('Xemele.Differential.Tests',299)]:
            counts[name] = tests(ROOT/f'tests/{name}/bin/Release/net10.0/{name}.dll', name, expected)
        assert sum(counts[name]['total'] for name in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']) == 801
        inventory = json.loads((ROOT/'tests/corpora/xml/inventory.json').read_text())
        cases = inventory['cases']
        product = {r['id']: r for p in (raw/'case-results').glob('product-*.json') for r in [json.loads(p.read_text())]}
        reference = {r['id']: r for p in (raw/'case-results').glob('reference-*.json') for r in [json.loads(p.read_text())]}
        assert len(product) == len(reference) == 242
        assert hashlib.sha256(runner.read_bytes()).hexdigest() == runner_hash, 'Fresh reference runner changed during tests'
        validate_reference_provenance(reference.values(), runner_hash, revision)
        joined = []
        for case in cases:
            supported = case['m1']['outcome'] == 'rejected'
            row = dict(id=case['id'], inputPath=case['inputPath'], inputSha256=case['inputSha256'],
                       profile='xml-1.0-fifth-edition-m1', expected=case['m1']['outcome'], reasons=case['m1']['reasons'],
                       productExecuted=supported, outcome='rejected' if supported else 'unrun-capability-gap',
                       standardsComparison='not-claimed', product=product.get(case['id']), reference=reference.get(case['id']))
            if supported:
                assert row['product']['outcome'] == 'rejected' and row['product']['sha256'] == case['inputSha256']
                assert row['reference']['sha256'] == case['inputSha256']
            joined.append(row)
        assert len(joined) == len({r['id'] for r in joined}) == 2585
        comparisons = [r['comparison'] for r in reference.values()]
        assert comparisons.count('matched-rejection') == 239 and comparisons.count('documented-reference-divergence') == 3
        (report/'case-results.json').write_text(json.dumps(dict(sourceRevision=revision, counts={'all':2585,'executed':242,'unrunCapabilityGaps':2343,'referenceMatches':239,'referenceDivergences':3},cases=joined),indent=2)+'\n')
        discovery = json.loads(run(sys.executable,'-B','tools/ApiBaseline/discover.py'))
        assert discovery['fixtures']==3388 and discovery['apiDeclarations']==222
        run('dotnet','build','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj',*FLAGS)
        run(sys.executable,'-B','tools/ApiBaseline/compare.py','--report',report/'runtime-api.json')
        expected = {}
        for lane in ['Dom','Serializer']:
            for line in (ROOT/f'tests/Contracts/Fixtures/M1/{lane}.golden.txt').read_text().splitlines()[1:]:
                row = json.loads(line)
                if row['member'] in expected: assert expected[row['member']]==row
                expected[row['member']] = row
        assert len(expected)==222
        (report/'approved-union.txt').write_text('xemele-api-v1\n'+''.join(json.dumps(expected[k],ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n' for k in sorted(expected)))
        run('dotnet',ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare',ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll',report/'approved-union.txt')
        # Preserve foundation shapes and independently compiled M1 stages.
        run('dotnet','build','docs/reports/M0-contracts/Checks/Checks.csproj',*FLAGS)
        counts['foundation'] = tests(ROOT/'docs/reports/M0-contracts/Checks/bin/Release/net10.0/Checks.dll','foundation',11)
        checker = ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
        for stage in ['M1','M2','ApiProbe']:
            run('dotnet','build',f'docs/reports/M0-contracts/Fixtures/{stage}/{stage}.csproj',*FLAGS)
            name = stage if stage == 'ApiProbe' else 'Foundation'+stage
            run('dotnet',checker, 'compare',ROOT/f'docs/reports/M0-contracts/Fixtures/{stage}/bin/Release/net10.0/{name}.dll',ROOT/f'tests/Contracts/Fixtures/foundation/{stage}.golden.txt')
        for lane in ['Common','Tree','Input','Syntax','Serializer','Dom','Protocol']:
            run('dotnet','build',f'docs/reports/M1-contracts/Projects/{lane}/{lane}.csproj',*FLAGS)
            dll = ROOT/f'docs/reports/M1-contracts/Projects/{lane}/bin/Release/net10.0/M1{lane}.dll'
            if lane == 'Protocol': run('dotnet',dll)
            else: run('dotnet',checker,'compare',dll,ROOT/f'tests/Contracts/Fixtures/M1/{lane}.golden.txt')
        run(sys.executable,'-B','-m','unittest','discover','-s','tests/Xemele.TestUtilities/Build','-p','test_*.py','-v')
        run('dotnet','build',REPORT_ROOT/'Demo/Demo.csproj',*FLAGS)
        source_demo = run('dotnet',REPORT_ROOT/'Demo/bin/Release/net10.0/Demo.dll')
        (report/'source-demo.txt').write_text(source_demo)
        # Config-only inspection; unchanged workload measurements remain dependency evidence.
        benchmark_files = [ROOT/p for p in ['experiments/Tree/Benchmarks.cs','experiments/Input/Benchmarks.cs','benchmarks/Xemele.Benchmarks/M1-common/CommonBenchmarks.cs','benchmarks/Xemele.Benchmarks/M1-input/InputBenchmarks.cs','benchmarks/Xemele.Benchmarks/M1-serialize/SerializerBenchmarks.cs']]
        benchmark_classes = []
        import re
        for path in benchmark_files:
            text = path.read_text()
            for match in re.finditer(r'((?:\[[^\]]+\]\s*)+)(?:public |internal )?(?:sealed )?class (\w+)',text):
                if 'MemoryDiagnoser' in match[1] or 'ShortRunJob' in match[1]:
                    assert 'MemoryDiagnoser' in match[1] and 'ShortRunJob' in match[1], str(path)
                    benchmark_classes.append(str(path.relative_to(ROOT))+':'+match[2])
        assert len(benchmark_classes)==16, benchmark_classes
        (report/'benchmark-config.json').write_text(json.dumps(benchmark_classes,indent=2)+'\n')
        packages = raw/'packages'
        run('dotnet','pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','--no-restore','-o',packages)
        package = next(packages.glob('Xemele.*.nupkg'))
        with zipfile.ZipFile(package) as archive:
            assert 'lib/net10.0/Xemele.dll' in archive.namelist()
            assert not any('Contracts' in p or 'Foundation' in p for p in archive.namelist())
            nuspec = ET.fromstring(archive.read(next(p for p in archive.namelist() if p.endswith('.nuspec'))))
            version = next(n.text for n in nuspec.iter() if n.tag.endswith('}version'))
        with tempfile.TemporaryDirectory(prefix='xemele-corpus-consumer-') as directory:
            directory = Path(directory)
            (directory/'Consumer.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="{version}" /></ItemGroup></Project>')
            (directory/'Program.cs').write_bytes((REPORT_ROOT/'Demo/Program.cs').read_bytes())
            package_demo = run('dotnet','run','--project',directory/'Consumer.csproj','-c','Release','--disable-build-servers','-p:UseSharedCompilation=false',f'-p:RestoreSources={packages}',f'-p:RestorePackagesPath={directory/"isolated-packages"}')
        assert package_demo == source_demo, 'Source/package demo behavior differs'
        (report/'package-demo.txt').write_text(package_demo)
        run(sys.executable,'-B','tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py')
        run(sys.executable,'-B','-m','unittest','discover','-s','docs/reports/M1-gate','-p','test_verify.py','-v')
        run(sys.executable,'-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v')
        run(sys.executable,'-B','tools/planning/tasks.py','validate')
        audit()
        (report/'summary.json').write_text(json.dumps(dict(sourceRevision=revision,host=platform.platform(),referenceProvenance=dict(runner=str(runner),runnerSha256=runner_hash,expectedBuild='1.0.0+'+revision,dotnet=str(dotnet),dotnetSha256=hashlib.sha256(dotnet.read_bytes()).hexdigest()),tests=counts,discovery=discovery,packageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),allocationEvidence='Not applicable: verification harness only; no runtime edits or timing claims.',result='passed'),indent=2)+'\n')
    finally:
        if sys.exc_info()[0] is not None:
            import traceback
            logs.append(traceback.format_exc())
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0))
        print('Durable report: '+str(report),flush=True)

if __name__ == '__main__': main()
