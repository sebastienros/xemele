"""Verify the common-only DTD composition repair and cumulative predecessor behavior."""
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
REPORT = ROOT / 'docs/reports/M2-dtd-composition'
CLAIM = '7081ff55f81feaffc41a4008424ada14a8551113'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report-prefix', required=True)
    parser.add_argument('--audit-base', default=CLAIM)
    parser.add_argument('--expected-branch')
    parser.add_argument('--expected-runtime-tests', type=int, default=4496)
    parser.add_argument('--expected-conformance-tests', type=int, default=255)
    parser.add_argument('--expected-differential-tests', type=int, default=299)
    parser.add_argument('--expected-public-api', type=int, default=272)
    parser.add_argument('--expected-fixtures', type=int, default=3388)
    args = parser.parse_args()
    if not re.fullmatch('[a-z0-9-]+', args.report_prefix): parser.error('invalid report prefix')
    report = REPORT / args.report_prefix
    report.mkdir()
    raw = ROOT / 'artifacts/M2-dtd-composition' / args.report_prefix
    raw.mkdir(parents=True)
    dotnet = Path(shutil.which('dotnet') or 'dotnet-unavailable').resolve(strict=True)
    env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', PATH=str(dotnet.parent)+os.pathsep+os.environ.get('PATH', ''),
               XEMELE_DOTNET=str(dotnet), XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',
               XEMELE_CORPUS_RESULTS=str(raw / 'case-results'))
    logs = []
    summary = {'result': 'failed', 'host': platform.platform(), 'architecture': platform.machine(), 'timing': 'scoped new measurement recorded separately; no inherited timing rerun'}

    def run(*command, expected=0):
        command = list(map(str, command))
        if command[0] == 'dotnet': command[0] = str(dotnet)
        print('$ ' + shlex.join(command), flush=True)
        result = subprocess.run(command, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=600)
        logs.append('$ '+shlex.join(command)+'\n'+result.stdout+f'\nexit={result.returncode}; expected={expected}\n')
        print(result.stdout[-1400:], flush=True)
        assert result.returncode == expected, logs[-1]
        return result.stdout

    try:
        revision = run('git', 'rev-parse', 'HEAD').strip()
        summary['sourceRevision'] = revision
        run('git', 'merge-base', '--is-ancestor', CLAIM, 'HEAD')
        run('git', 'merge-base', '--is-ancestor', 'ab2fcabaf3cc984901f484c6fce1069c385e8c2f', 'HEAD')
        run('git', 'merge-base', '--is-ancestor', args.audit_base, 'HEAD')
        branch = run('git', 'branch', '--show-current').strip()
        if args.expected_branch: assert branch == args.expected_branch
        summary.update(branch=branch, auditBase=args.audit_base)
        assert run('dotnet', '--version').strip() == '10.0.400'
        runtime_info = run('dotnet', '--info')
        assert 'Microsoft.NETCore.App 10.0.11 ' in runtime_info, 'required target runtime unavailable'
        summary['dotnet'] = str(dotnet)
        summary['dotnetSha256'] = hashlib.sha256(dotnet.read_bytes()).hexdigest()
        task = json.loads((ROOT/'docs/tasks/M2-dtd-composition.json').read_text())
        for dependency in task['depends_on']:
            record = json.loads((ROOT/f'docs/tasks/{dependency}.json').read_text())
            assert record['status'] == 'verified'
            run('git', 'merge-base', '--is-ancestor', record['integration_revision'], 'HEAD')
        source_paths = ['src/Xemele/Parsing/Dtd/Grammar/Parser', 'tests/Xemele.Tests/M2-dtd-composition', 'samples/M2-dtd-composition', 'benchmarks/Xemele.Benchmarks/M2-dtd-composition', 'docs/reports/M2-dtd-composition/verify.py', 'docs/reports/M2-dtd-composition/BenchmarkDriver.cs']
        assert not run('git', 'diff', '--name-only', 'HEAD', '--', *source_paths).strip(), 'uncommitted tested sources'
        assert not run('git', 'ls-files', '--others', '--exclude-standard', '--', *source_paths).strip(), 'untracked tested sources'
        sample = ROOT/'samples/M2-dtd-composition/bin/Release/net10.0/Xemele.DtdCompositionSample.dll'
        run('dotnet', 'build', ROOT/'samples/M2-dtd-composition/Sample.csproj', *FLAGS)
        runtime = run('dotnet', sample, '--runtime').strip()
        assert runtime == '10.0.11', 'unexpected actual net10 executable runtime'
        summary['runtime'] = runtime
        (report/'internal-contract.txt').write_text(run('dotnet', sample, '--contract'))
        fixture = (ROOT/'tests/Contracts/Fixtures/M2/Dtd.cs.fixture').read_text()
        approved = raw/'approved'; approved.mkdir()
        (approved/'Dtd.cs').write_text(fixture)
        (approved/'Program.cs').write_text('Inventory.Print();')
        (approved/'Approved.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><Compile Include="'+str(ROOT/'samples/M2-dtd/Inventory.cs')+'" /><Compile Include="'+str(ROOT/'src/Xemele/Options/Common.cs')+'" /><Compile Include="'+str(ROOT/'src/Xemele/Diagnostics/Common.cs')+'" /></ItemGroup></Project>')
        run('dotnet', 'build', approved/'Approved.csproj', *FLAGS)
        approved_inventory = run('dotnet', approved/'bin/Release/net10.0/Approved.dll', '--contract')
        assert approved_inventory == (report/'internal-contract.txt').read_text(), 'actual parser API differs from independently compiled approved declarations'
        canonical_inventory = ROOT/'docs/reports/M2-dtd/internal-api.txt'
        if canonical_inventory.exists():
            assert canonical_inventory.read_text() == approved_inventory, 'published internal inventory mismatch'
        summary['internalApi'] = {'matched': True, 'types': 20, 'inventory': 'internal-contract.txt', 'nullableAndDefaults': True}
        mutated = fixture.replace('public string? DefaultValue { get; }', 'public string DefaultValue { get; }').replace('DefaultValue = defaultValue;', 'DefaultValue = defaultValue!;')
        assert mutated != fixture
        (approved/'Dtd.cs').write_text(mutated)
        run('dotnet', 'build', approved/'Approved.csproj', *FLAGS)
        assert run('dotnet', approved/'bin/Release/net10.0/Approved.dll', '--contract') != approved_inventory, 'nullable mutation not detected'
        summary['internalApi']['compiledNullableMutationDetected'] = True
        hooks = run('dotnet', sample, '--hooks')
        (report/'hook-contract.txt').write_text(hooks)
        summary['nonpublicHooks'] = {'exactSignatureAccessNullability': True, 'count': 3}
        mutation = raw/'hook-mutation'; mutation.mkdir()
        compile_paths = [ROOT/'src/Xemele/Options/Common.cs', ROOT/'src/Xemele/Diagnostics/Common.cs', ROOT/'src/Xemele/Internal/Operations/ScalarLocation.cs', ROOT/'src/Xemele/Parsing/Dtd/IDtdBudget.cs']
        for directory in ['Model', 'Framing', 'Grammar/Values', 'Grammar/Parser']:
            compile_paths.extend((ROOT/'src/Xemele/Parsing/Dtd'/directory).glob('*.cs'))
        composition = ROOT/'src/Xemele/Parsing/Dtd/Grammar/Parser/Composition.cs'
        mutated_hook = composition.read_text().replace('internal void SeedPriorModel', 'public void SeedPriorModel')
        assert mutated_hook != composition.read_text()
        (mutation/'Composition.cs').write_text(mutated_hook)
        compile_paths.remove(composition)
        compile_paths.extend((ROOT/'samples/M2-dtd-composition').glob('*.cs'))
        compile_paths.append(ROOT/'samples/M2-dtd/Inventory.cs')
        compile_paths.append(ROOT/'benchmarks/Xemele.Benchmarks/M2-dtd-composition/OperationLedger.cs')
        includes = ''.join('<Compile Include="'+str(path)+'" />' for path in compile_paths)
        (mutation/'Mutation.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>'+includes+'</ItemGroup></Project>')
        run('dotnet', 'build', mutation/'Mutation.csproj', *FLAGS)
        mutated_output = run('dotnet', mutation/'bin/Release/net10.0/Mutation.dll', '--hooks', expected=1)
        assert mutated_output != hooks and 'SeedPriorModel|internal=False' in mutated_output
        summary['nonpublicHooks']['compiledAccessMutationDetected'] = True
        default_mutation = composition.read_text().replace('CancellationToken cancellationToken)', 'CancellationToken cancellationToken = default)')
        assert default_mutation != composition.read_text()
        (mutation/'Composition.cs').write_text(default_mutation)
        run('dotnet', 'build', mutation/'Mutation.csproj', *FLAGS)
        default_output = run('dotnet', mutation/'bin/Release/net10.0/Mutation.dll', '--hooks', expected=1)
        assert default_output != hooks and ':optional=True:default=null' in default_output
        summary['nonpublicHooks']['compiledOptionalDefaultMutationDetected'] = True

        pinned_metadata = json.loads((ROOT/'tests/fixtures/M2-dtd-grammar/pinned.json').read_text())
        upstream = json.loads((ROOT/'tests/corpora/xml/inventory.json').read_text())
        assert pinned_metadata['archiveSha256'] == upstream['archiveSha256'] and pinned_metadata['sourceUrl'] == upstream['sourceUrl']
        upstream_files = {f['path']: f for f in upstream['files']}
        upstream_cases = {c['id']: c for c in upstream['cases']}
        for case in pinned_metadata['cases']:
            assert upstream_cases[case['id']]['inputPath'] == case['resource'].replace('.dtd', '.xml')
            assert case['sha256'] == upstream_files[case['resource']]['sha256'] and case['bytes'] == upstream_files[case['resource']]['bytes']
        summary['pinnedProvenance'] = pinned_metadata
        # The inherited pinned tests run unchanged. Count executed split cases from actual private bytes.
        cache = Path(env['XEMELE_CORPUS_RESOURCES'])
        split_parses = 0
        for case in pinned_metadata['cases']:
            data = (cache/case['resource']).read_bytes()
            assert len(data) == case['bytes'] and hashlib.sha256(data).hexdigest() == case['sha256']
            text = data.decode('utf-8')
            split_parses += len(text.encode('utf-16-le')) // 2 + 1
        summary['pinnedSplitParses'] = split_parses
        focused = raw/'focused.xml'
        run('dotnet', 'build', 'tests/Xemele.Tests/Xemele.Tests.csproj', *FLAGS)
        run('dotnet', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll', '-noColor', '-class', 'Xemele.Tests.Dtd*',
            '-class', 'Xemele.Tests.ValueTests', '-class', 'Xemele.Tests.FramingTests', '-result-xml', focused)
        cases = list(ET.parse(focused).iter('test'))
        assert cases and all(t.get('result') == 'Pass' for t in cases)
        (report/'focused.xml.gz').write_bytes(gzip.compress(focused.read_bytes(), mtime=0))
        focused_names = [(t.get('type'), t.get('method'), t.get('name')) for t in cases]
        summary['focusedTests'] = len(cases)
        sample_output = run('dotnet', sample)
        (report/'sample.txt').write_text(sample_output)
        assert sample_output == 'elements=child;default=inside;priorIdentity=True;source=https://fixture.invalid/effective.dtd;line=2\nsecondDeclaration=Rejected;code=DTD.Syntax;line=2\n', sample_output
        summary['sampleOracle'] = 'Exact seeded PE visibility/effective internal general default/identity/final URI and second-declaration rejection outputs matched.'
        # The parser changed: inherited timing is preservation evidence only.
        archive = ROOT/'docs/reports/M2-dtd-grammar/exclusive-d1dfbac/full-raw.zip'
        assert hashlib.sha256(archive.read_bytes()).hexdigest() == '067634a93eee38804668c4c2048534594dd0d8151bbea506bab8981aa86e8545'
        run('dotnet', 'build', ROOT/'docs/reports/M2-dtd-composition/Benchmarks.csproj', *FLAGS)
        run('dotnet', 'build', ROOT/'benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj', *FLAGS)
        benchmark = ROOT/'docs/reports/M2-dtd-composition/bin/Release/net10.0/Xemele.DtdCompositionBenchmarks.dll'
        summary['benchmarkCorrectness'] = run('dotnet', benchmark, '--correctness').strip()
        summary['benchmarkDiscovery'] = run('dotnet', benchmark, '--list', 'flat').splitlines()
        assert sum('Xemele.Benchmarks.' in line for line in summary['benchmarkDiscovery']) == 3
        run('python3', '-B', 'docs/reports/M1-corpus-import/provision.py')
        run('dotnet', 'build', 'tools/ReferenceRunners/Bcl/BclReference.csproj', *FLAGS)
        original = ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
        shutil.copytree(original.parent, raw/'pinned-reference')
        pinned = raw/'pinned-reference'/original.name
        env['XEMELE_BCL_RUNNER'] = str(pinned)
        runner_hash = hashlib.sha256(pinned.read_bytes()).hexdigest()
        inherited = run('python3', '-B', 'docs/reports/M1-contracts/verify.py')
        totals = re.findall(r'^\s*total:\s*(\d+)\s*$', inherited, re.MULTILINE)
        expected_total = args.expected_runtime_tests + args.expected_conformance_tests + args.expected_differential_tests
        assert totals and int(totals[-1]) == expected_total, ('MTP count', totals, expected_total)
        summary['mtpTests'] = {'total': expected_total, 'failed': 0, 'skipped': 0}
        run('python3', '-B', 'tools/ApiBaseline/compare.py', '--report', report/'runtime-api.json')
        api = json.loads((report/'runtime-api.json').read_text())
        assert api['matched'] and len(api['provenance']) == args.expected_public_api
        summary['publicApiMembers'] = len(api['provenance'])
        counts = {}
        for assembly, expected in [('Xemele.Tests', args.expected_runtime_tests), ('Xemele.Conformance.Tests', args.expected_conformance_tests), ('Xemele.Differential.Tests', args.expected_differential_tests)]:
            discovered_tests = run('dotnet', 'test', '--project', ROOT/f'tests/{assembly}', '-c', 'Release', '--no-build', '--no-restore', '--list-tests')
            (report/(assembly+'-discovery.txt.gz')).write_bytes(gzip.compress(discovered_tests.encode(), mtime=0))
            discoveries = re.findall(r'Discovered (\d+) tests\.', discovered_tests)
            assert discoveries and int(discoveries[-1]) == expected, (assembly, discoveries, expected)
            xml = raw/(assembly+'.xml')
            run('dotnet', ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll', '-noColor', '-result-xml', xml)
            tests = list(ET.parse(xml).iter('test'))
            assert len(tests) == expected and all(t.get('result') == 'Pass' for t in tests), (assembly, len(tests), expected)
            counts[assembly] = {'total': len(tests), 'failed': 0, 'skipped': 0}
            (report/(assembly+'.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(), mtime=0))
        summary['runtimeTests'] = counts
        runtime_tests = list(ET.parse(raw/'Xemele.Tests.xml').iter('test'))
        cumulative_names = {(t.get('type'), t.get('method'), t.get('name')) for t in runtime_tests}
        assert set(focused_names).issubset(cumulative_names), 'focused rows missing in cumulative run'
        groups = {}
        for t in cases: groups[t.get('type')] = groups.get(t.get('type'), 0) + 1
        summary['focusedClassCounts'] = groups
        assert sum(n for name, n in groups.items() if name.startswith('Xemele.Tests.DtdGrammar')) == 19
        assert groups['Xemele.Tests.DtdCompositionTests'] == 27
        inherited = set()
        with gzip.open(ROOT/'docs/reports/M2-dtd/integration-attempt1/Xemele.Tests.xml.gz', 'rt') as f:
            inherited = {(t.get('type'), t.get('method'), t.get('name')) for t in ET.fromstring(f.read()).iter('test')}
        assert inherited.issubset(cumulative_names), 'predecessor tests missing'
        discovery = json.loads(run('python3', '-B', 'tools/ApiBaseline/discover.py'))
        assert discovery['apiDeclarations'] == args.expected_public_api and discovery['fixtures'] == args.expected_fixtures
        summary['discovery'] = {'apiDeclarations': discovery['apiDeclarations'], 'fixtures': discovery['fixtures']}
        records = [json.loads(p.read_text()) for p in (raw/'case-results').glob('reference-*.json')]
        assert len(records) == 242 and len({r['id'] for r in records}) == 242
        assert hashlib.sha256(pinned.read_bytes()).hexdigest() == runner_hash
        for record in records:
            assert record['runnerSha256'] == runner_hash
            assert record['response']['engine']['build'] == '1.0.0+'+revision
        summary['referenceProvenance'] = {'records': 242, 'runnerSha256': runner_hash, 'build': '1.0.0+'+revision, 'completeFreshOutputCopied': True, 'unchangedAfterChecks': True}
        (report/'reference-provenance.json').write_text(json.dumps(summary['referenceProvenance'], indent=2)+'\n')
        run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tools/planning', '-p', 'test_tasks.py', '-v')
        run('python3', '-B', 'tools/planning/tasks.py', 'validate')
        task = json.loads((ROOT/'docs/tasks/M2-dtd-composition.json').read_text())
        owned = task['owns']+['docs/tasks/M2-dtd-composition.json']
        paths = []
        for command in [('git', 'diff', '--name-only', args.audit_base+'..HEAD'), ('git', 'diff', '--name-only'),
                        ('git', 'diff', '--cached', '--name-only'), ('git', 'ls-files', '--others', '--exclude-standard')]:
            paths.extend(run(*command).splitlines())
        assert all(any(path.startswith(own) if own.endswith('/') else path == own for own in owned) for path in paths), paths
        for suffix in [args.audit_base+'..HEAD', None, '--cached']:
            run('git', 'diff', '--check', *([suffix] if suffix else []))
        summary['ownedPaths'] = sorted(set(paths))
        summary['result'] = 'passed'
        summary['scope'] = 'Common-only seed/preframed DTD composition repair; preserved constructor/public-method inventory and all predecessor rows. New allocation/timing evidence separate. No core/entity implementation or full XML conformance claim. Windows/x64/AOT unrun.'
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(), mtime=0))
        (report/'summary.json').write_text(json.dumps(summary, indent=2)+'\n')


if __name__ == '__main__': main()
