"""Verify actual framing, independent approved signatures and cumulative M1 behavior."""
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
REPORT = ROOT / 'docs/reports/M2-dtd-framing'
CLAIM = '2e3d6012e664baf7bf5b3f3085bcb04de7dfc02e'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report-prefix', required=True)
    parser.add_argument('--audit-base', default=CLAIM)
    parser.add_argument('--expected-branch')
    args = parser.parse_args()
    if not re.fullmatch('[a-z0-9-]+', args.report_prefix): parser.error('invalid report prefix')
    report = REPORT / args.report_prefix
    report.mkdir()
    raw = ROOT / 'artifacts/M2-dtd-framing' / args.report_prefix
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
        assert run('dotnet', '--version').strip() == '10.0.400'
        summary['dotnet'] = str(dotnet)
        summary['dotnetSha256'] = hashlib.sha256(dotnet.read_bytes()).hexdigest()
        source_paths = ['src/Xemele/Parsing/Dtd/IDtdBudget.cs', 'src/Xemele/Parsing/Dtd/Framing',
                        'tests/Xemele.Tests/M2-dtd-framing', 'benchmarks/Xemele.Benchmarks/M2-dtd-framing',
                        'samples/M2-dtd-framing', 'docs/reports/M2-dtd-framing/Independent', 'docs/reports/M2-dtd-framing/verify.py']
        assert not run('git', 'diff', '--name-only', 'HEAD', '--', *source_paths).strip(), 'uncommitted tested sources'
        assert not run('git', 'ls-files', '--others', '--exclude-standard', '--', *source_paths).strip(), 'untracked tested sources'
        run('dotnet', 'build', REPORT/'Independent/Independent.csproj', *FLAGS)
        inventory = run('dotnet', REPORT/'Independent/bin/Release/net10.0/Independent.dll')
        (report/'internal-contract.txt').write_text(inventory)
        # Compile only the reviewed budget/framing declarations as a separate model;
        # never run the fixture's placeholder bodies. Compare all declared public
        # members and the internal shared constructor against actual implementation.
        fixture = (ROOT/'tests/Contracts/Fixtures/M2/Dtd.cs.fixture').read_text()
        budget = fixture[fixture.index('internal interface IDtdBudget'):fixture.index('internal sealed class DtdParser')]
        framing = fixture[fixture.index('internal enum TextDeclarationStep'):]
        model = raw/'model'; model.mkdir()
        (model/'Model.cs').write_text('namespace Xemele.Parsing.Dtd;\n'+budget+framing)
        program = REPORT/'Independent/Program.cs'
        (model/'Model.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
            '<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>Exe</OutputType>'
            '</PropertyGroup><ItemGroup><Compile Include="'+str(program)+'" />'
            '<Compile Include="'+str(ROOT/'src/Xemele/Options/Common.cs')+'" />'
            '<Compile Include="'+str(ROOT/'src/Xemele/Diagnostics/Common.cs')+'" /></ItemGroup></Project>')
        run('dotnet', 'build', model/'Model.csproj', *FLAGS)
        approved = run('dotnet', model/'bin/Release/net10.0/Model.dll', '--contract-inventory')
        assert inventory == approved, 'approved internal contract differs'
        summary['independentInternalContract'] = 'approved and implemented types/members/shared constructor match; no decoder/resolver/DOM/grammar sources'
        run('dotnet', 'build', ROOT/'samples/M2-dtd-framing/Sample.csproj', *FLAGS)
        sample = ROOT/'samples/M2-dtd-framing/bin/Release/net10.0/Xemele.DtdFramingSample.dll'
        (report/'sample.txt').write_text(run('dotnet', sample))
        allocations = json.loads(run('dotnet', sample, '--allocations').strip())
        (report/'allocations.json').write_text(json.dumps(allocations, indent=2)+'\n')
        summary['allocations'] = allocations
        run('dotnet', 'build', ROOT/'benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj', *FLAGS)
        discovered = run('dotnet', ROOT/'benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll', '--list', 'flat', '--filter', '*DtdFraming*')
        assert len(discovered.strip().splitlines()) == 3
        summary['benchmarksPrepared'] = discovered.strip().splitlines()
        run('python3', '-B', 'docs/reports/M1-corpus-import/provision.py')
        run('dotnet', 'build', 'tools/ReferenceRunners/Bcl/BclReference.csproj', *FLAGS)
        original = ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
        shutil.copytree(original.parent, raw/'pinned-reference')
        pinned = raw/'pinned-reference'/original.name
        env['XEMELE_BCL_RUNNER'] = str(pinned)
        runner_hash = hashlib.sha256(pinned.read_bytes()).hexdigest()
        run('python3', '-B', 'docs/reports/M1-contracts/verify.py')
        run('python3', '-B', 'tools/ApiBaseline/compare.py', '--report', report/'runtime-api.json')
        counts = {}
        for assembly, minimum in [('Xemele.Tests', 1487), ('Xemele.Conformance.Tests', 255), ('Xemele.Differential.Tests', 299)]:
            xml = raw/(assembly+'.xml')
            run('dotnet', ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll', '-noColor', '-result-xml', xml)
            tests = list(ET.parse(xml).iter('test'))
            assert len(tests) >= minimum and all(t.get('result') == 'Pass' for t in tests), (assembly, len(tests))
            counts[assembly] = {'total': len(tests), 'failed': 0, 'skipped': 0}
            (report/(assembly+'.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(), mtime=0))
        summary['runtimeTests'] = counts
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
        task = json.loads((ROOT/'docs/tasks/M2-dtd-framing.json').read_text())
        owned = task['owns']+['docs/tasks/M2-dtd-framing.json']
        paths = []
        for command in [('git', 'diff', '--name-only', args.audit_base+'..HEAD'), ('git', 'diff', '--name-only'),
                        ('git', 'diff', '--cached', '--name-only'), ('git', 'ls-files', '--others', '--exclude-standard')]:
            paths.extend(run(*command).splitlines())
        assert all(any(path.startswith(own) if own.endswith('/') else path == own for own in owned) for path in paths), paths
        for suffix in [args.audit_base+'..HEAD', None, '--cached']:
            run('git', 'diff', '--check', *([suffix] if suffix else []))
        summary['ownedPaths'] = sorted(set(paths))
        summary['result'] = 'passed'
        summary['scope'] = 'Incremental external declaration framing only. Full DTD grammar/live resolver/additional encoding unimplemented; Windows/x64/AOT unrun.'
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(), mtime=0))
        (report/'summary.json').write_text(json.dumps(summary, indent=2)+'\n')


if __name__ == '__main__': main()
