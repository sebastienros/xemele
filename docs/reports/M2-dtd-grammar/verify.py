"""Verify actual parser, independently compiled approved parser contract and cumulative M1 behavior."""
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
REPORT = ROOT / 'docs/reports/M2-dtd-grammar'
CLAIM = 'f8da650052ced1bfa741f378e324d443ee078fe4'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report-prefix', required=True)
    parser.add_argument('--audit-base', default=CLAIM)
    parser.add_argument('--expected-branch')
    parser.add_argument('--expected-runtime-tests', type=int, default=4441)
    parser.add_argument('--expected-conformance-tests', type=int, default=255)
    parser.add_argument('--expected-differential-tests', type=int, default=299)
    parser.add_argument('--expected-public-api', type=int, default=251)
    parser.add_argument('--expected-fixtures', type=int, default=3388)
    args = parser.parse_args()
    if not re.fullmatch('[a-z0-9-]+', args.report_prefix): parser.error('invalid report prefix')
    report = REPORT / args.report_prefix
    report.mkdir()
    raw = ROOT / 'artifacts/M2-dtd-grammar' / args.report_prefix
    raw.mkdir(parents=True)
    dotnet = Path(shutil.which('dotnet') or 'dotnet-unavailable').resolve(strict=True)
    env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', PATH=str(dotnet.parent)+os.pathsep+os.environ.get('PATH', ''),
               XEMELE_DOTNET=str(dotnet), XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',
               XEMELE_CORPUS_RESULTS=str(raw / 'case-results'))
    logs = []
    summary = {'result': 'failed', 'host': platform.platform(), 'architecture': platform.machine(), 'timing': 'not run; exclusive coordinator grant required'}

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
        assert json.loads((ROOT/'docs/tasks/M2-contracts.json').read_text())['status'] == 'verified'
        for dependency in ['M2-pi-allocation-probe', 'M2-tree-allocation-probe']:
            prerequisite = json.loads((ROOT/f'docs/tasks/{dependency}.json').read_text())
            assert prerequisite['status'] == 'verified'
            run('git', 'merge-base', '--is-ancestor', prerequisite['integration_revision'], 'HEAD')
        assert run('dotnet', '--version').strip() == '10.0.400'
        summary['dotnet'] = str(dotnet)
        summary['dotnetSha256'] = hashlib.sha256(dotnet.read_bytes()).hexdigest()
        task = json.loads((ROOT/'docs/tasks/M2-dtd-grammar.json').read_text())
        for dependency in task['depends_on']:
            record = json.loads((ROOT/f'docs/tasks/{dependency}.json').read_text())
            assert record['status'] == 'verified'
            run('git', 'merge-base', '--is-ancestor', record['integration_revision'], 'HEAD')
        assert not run('git', 'diff', '--name-only', CLAIM+'..HEAD', '--', 'src').strip(), 'join must not edit runtime'
        source_paths = ['tests/Xemele.Tests/M2-dtd-grammar', 'samples/M2-dtd-grammar',
                        'benchmarks/Xemele.Benchmarks/M2-dtd-grammar', 'tests/fixtures/M2-dtd-grammar',
                        'docs/reports/M2-dtd-grammar/verify.py', 'docs/reports/M2-dtd-grammar/BenchmarkDriver.cs',
                        'docs/reports/M2-dtd-grammar/Xemele.DtdGrammarJoinBenchmarks.csproj']
        assert not run('git', 'diff', '--name-only', 'HEAD', '--', *source_paths).strip(), 'uncommitted tested sources'
        assert not run('git', 'ls-files', '--others', '--exclude-standard', '--', *source_paths).strip(), 'untracked tested sources'
        sample = ROOT/'samples/M2-dtd-grammar/bin/Release/net10.0/Xemele.DtdGrammarJoinSample.dll'
        run('dotnet', 'build', ROOT/'samples/M2-dtd-grammar/Sample.csproj', *FLAGS)
        (report/'internal-contract.txt').write_text(run('dotnet', sample, '--contract'))
        fixture = (ROOT/'tests/Contracts/Fixtures/M2/Dtd.cs.fixture').read_text()
        approved = raw/'approved'; approved.mkdir()
        (approved/'Dtd.cs').write_text(fixture)
        (approved/'Program.cs').write_text('Inventory.Print();')
        (approved/'Approved.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><Compile Include="'+str(ROOT/'samples/M2-dtd-grammar/Inventory.cs')+'" /><Compile Include="'+str(ROOT/'src/Xemele/Options/Common.cs')+'" /><Compile Include="'+str(ROOT/'src/Xemele/Diagnostics/Common.cs')+'" /></ItemGroup></Project>')
        run('dotnet', 'build', approved/'Approved.csproj', *FLAGS)
        approved_inventory = run('dotnet', approved/'bin/Release/net10.0/Approved.dll', '--contract')
        assert approved_inventory == (report/'internal-contract.txt').read_text(), 'actual parser API differs from independently compiled approved declarations'
        summary['internalApi'] = {'matched': True, 'types': 20, 'inventory': 'internal-contract.txt', 'nullableAndDefaults': True}
        mutated = fixture.replace('public string? DefaultValue { get; }', 'public string DefaultValue { get; }').replace('DefaultValue = defaultValue;', 'DefaultValue = defaultValue!;')
        assert mutated != fixture
        (approved/'Dtd.cs').write_text(mutated)
        run('dotnet', 'build', approved/'Approved.csproj', *FLAGS)
        assert run('dotnet', approved/'bin/Release/net10.0/Approved.dll', '--contract') != approved_inventory, 'nullable mutation not detected'
        summary['internalApi']['compiledNullableMutationDetected'] = True
        own_sources = [p.read_text() for p in (ROOT/'tests/Xemele.Tests/M2-dtd-grammar').glob('*.cs')]
        fact_rows = sum(s.count('[Fact]') for s in own_sources)
        inline_rows = sum(s.count('[InlineData(') for s in own_sources)
        pinned_rows = len(json.loads((ROOT/'tests/fixtures/M2-dtd-grammar/pinned.json').read_text())['cases'])
        own_total = fact_rows + inline_rows + pinned_rows
        summary['derivedOwnTests'] = {'facts': fact_rows, 'inlineRows': inline_rows, 'pinnedRows': pinned_rows, 'total': own_total}
        args.expected_runtime_tests += own_total
        focused = raw/'focused.xml'
        run('dotnet', 'build', 'tests/Xemele.Tests/Xemele.Tests.csproj', *FLAGS)
        run('dotnet', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll', '-noColor', '-class', 'Xemele.Tests.DtdGrammar*', '-result-xml', focused)
        cases = list(ET.parse(focused).iter('test'))
        assert len(cases) == own_total and all(t.get('result') == 'Pass' for t in cases)
        (report/'focused.xml.gz').write_bytes(gzip.compress(focused.read_bytes(), mtime=0))
        summary['focusedTests'] = len(cases)
        sample = ROOT/'samples/M2-dtd-grammar/bin/Release/net10.0/Xemele.DtdGrammarJoinSample.dll'
        (report/'sample.txt').write_text(run('dotnet', sample))
        allocations = json.loads(run('dotnet', sample, '--allocations').strip().splitlines()[-1])
        (report/'allocations.json').write_text(json.dumps(allocations, indent=2)+'\n')
        summary['allocations'] = allocations
        run('dotnet', 'build', REPORT/'Xemele.DtdGrammarJoinBenchmarks.csproj', *FLAGS)
        summary['benchmarkCorrectness'] = run('dotnet', REPORT/'bin/Release/net10.0/Xemele.DtdGrammarJoinBenchmarks.dll', '--correctness').strip()
        discovered = run('dotnet', REPORT/'bin/Release/net10.0/Xemele.DtdGrammarJoinBenchmarks.dll', '--list', 'flat', '--filter', '*DtdGrammarJoin*')
        assert len(discovered.strip().splitlines()) == 1
        summary['benchmarksPrepared'] = discovered.strip().splitlines()
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
        values = [t for t in runtime_tests if t.get('type', '').startswith('Xemele.Tests.DtdGrammar')]
        assert len(values) == own_total and all(t.get('result') == 'Pass' for t in values)
        summary['ownJoinTests'] = len(values)
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
        task = json.loads((ROOT/'docs/tasks/M2-dtd-grammar.json').read_text())
        owned = task['owns']+['docs/tasks/M2-dtd-grammar.json']
        paths = []
        for command in [('git', 'diff', '--name-only', args.audit_base+'..HEAD'), ('git', 'diff', '--name-only'),
                        ('git', 'diff', '--cached', '--name-only'), ('git', 'ls-files', '--others', '--exclude-standard')]:
            paths.extend(run(*command).splitlines())
        assert all(any(path.startswith(own) if own.endswith('/') else path == own for own in owned) for path in paths), paths
        for suffix in [args.audit_base+'..HEAD', None, '--cached']:
            run('git', 'diff', '--check', *([suffix] if suffix else []))
        summary['ownedPaths'] = sorted(set(paths))
        summary['result'] = 'passed'
        summary['scope'] = 'Owned join validation/sample of verified common-only DTD children; no production runtime edits. Sample demonstrates effective-table composition without implementing the later production DOCTYPE/general-entity owner. Pinned subsets do not claim full XML conformance. Windows/x64/AOT unrun; new composition timing requires exclusive grant. Existing child timing is not rerun.'
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(), mtime=0))
        (report/'summary.json').write_text(json.dumps(summary, indent=2)+'\n')


if __name__ == '__main__': main()
