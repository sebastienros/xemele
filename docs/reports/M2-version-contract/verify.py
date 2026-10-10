#!/usr/bin/env python3
"""Scoped version-contract verification; raw outputs exist only in auto-cleaned tmp.

Prints concise counts and identities. Never writes logs/XML/baselines to the repo.
Stops on the first unexpected result; failed/skipped checks are not passes.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[3]
CLAIM = '3105bf07e6ecd86eca88e823b6a0c75a3016caf5'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
FIXTURE = ROOT / 'tests/Contracts/Fixtures/M2'


def main():
    env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0')
    results = []
    summary = {'result': 'failed', 'checks': results}
    def run(*command, expected=0):
        command = list(map(str, command))
        process = subprocess.run(command, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                                 stderr=subprocess.STDOUT, text=True, timeout=300)
        results.append({'command': command, 'exit': process.returncode, 'expectedExit': expected})
        if process.returncode != expected:
            print(process.stdout[-5000:], flush=True)
            raise RuntimeError(f'Unexpected exit {process.returncode}: {command}')
        return process.stdout
    def digest(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()
    try:
        revision = run('git', 'rev-parse', 'HEAD').strip()
        summary['revision'] = revision
        run('git', 'merge-base', '--is-ancestor', CLAIM, 'HEAD')
        assert json.loads((ROOT/'docs/tasks/M2-contracts.json').read_text())['status'] == 'verified'
        run('git', 'merge-base', '--is-ancestor', 'ab2fcabaf3cc984901f484c6fce1069c385e8c2f', 'HEAD')
        assert run('dotnet', '--version').strip() == '10.0.400'
        # Must verify committed sources; evidence metadata may subsequently change.
        assert not run('git', 'diff', 'HEAD', '--', 'docs/specs/parser.md',
                       'docs/decisions/M2/xml-resources.md', 'tests/Contracts/Fixtures/M2/Version',
                       'docs/reports/M2-version-contract/verify.py').strip()
        with tempfile.TemporaryDirectory(prefix='xemele-version-contract-') as tmp:
            tmp = Path(tmp)
            artifacts = tmp/'build'
            output_args = [*FLAGS, '--artifacts-path', artifacts]
            run('dotnet', 'build', 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', *output_args)
            checker = artifacts/'bin/ApiCheck/release/ApiCheck.dll'
            assert checker.is_file()
            models = {}
            for lane in ['Resolver', 'Dtd', 'Encoding', 'Entities', 'Recovery', 'Checks']:
                project = ROOT/f'docs/reports/M2-contracts/Projects/{lane}/{lane}.csproj'
                run('dotnet', 'build', project, *output_args)
                assembly = artifacts/f'bin/{lane}/release/M2{lane}.dll'
                if lane == 'Checks':
                    text = run('dotnet', assembly)
                    summary['inheritedModelAssertions'] = int(re.search(r'Passed (\d+)', text)[1])
                else:
                    golden = FIXTURE/(lane+'.golden.txt')
                    run('dotnet', checker, 'compare', assembly, golden)
                    models[lane] = {'members': len(golden.read_text().splitlines())-1,
                                    'assemblySha256': digest(assembly), 'baselineSha256': digest(golden)}
            summary['preservedPublicModels'] = models
            # Independent explicit-source project, no runtime project reference.
            sources = ['M2/Common', 'M2/Options', 'M2/Resolver', 'M2/Dtd', 'M1/Tree',
                       'M2/Entities', 'M2/Syntax', 'M2/Version/VersionChecks', 'M2/Version/AbiChecks']
            paths = [ROOT/f'tests/Contracts/Fixtures/{source}.cs.fixture' for source in sources]
            project = tmp/'VersionModels.csproj'
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>'
                '<EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>enable</Nullable>'
                '<ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>'
                '</PropertyGroup><ItemGroup>' + ''.join(f'<Compile Include="{escape(str(p))}" />' for p in paths) +
                '<Compile Include="$(ProbeSource)" Condition="\'$(ProbeSource)\' != \'\'" />'
                '</ItemGroup></Project>')
            run('dotnet', 'build', project, *output_args)
            model = artifacts/'bin/VersionModels/release/VersionModels.dll'
            # Actual product/tests preserve the inherited integrated implementation;
            # version correction itself deliberately has no runtime changes.
            run('dotnet', 'build', 'tests/Xemele.Tests/Xemele.Tests.csproj', *output_args)
            runtime = artifacts/'bin/Xemele/release/Xemele.dll'
            text = run('dotnet', model, runtime)
            summary['versionModelAssertions'] = int(re.search(r'Passed (\d+)', text)[1])
            summary['internalAbiShapes'] = 4
            summary['modelSourceSha256'] = {str(p.relative_to(ROOT)): digest(p) for p in paths}
            summary['modelAssemblySha256'] = digest(model)
            # Two specific compiler failures enforce the existing borrowed/owned shapes.
            negatives = [
                ('borrowed', 'class Probe { Xemele.Parsing.Syntax.XmlDeclarationView retained; }', 'CS8345'),
                ('owned', 'class Probe { void Use(Xemele.Parsing.Dtd.XmlTextDeclaration d) { d.Version = "1.0"; } }', 'CS8852')]
            for name, source, error in negatives:
                probe = tmp/(name+'.cs'); probe.write_text(source)
                text = run('dotnet', 'build', project, *output_args, f'-p:ProbeSource={probe}', expected=1)
                assert error in text, (name, error)
            summary['expectedCompilerRejections'] = [n[2] for n in negatives]
            sys.path.insert(0, str(ROOT/'tools/ApiBaseline'))
            from compare import compare
            api = compare(ROOT, runtime, checker)
            assert api['matched'], api['checkerOutput']
            results.append({'command': ['tools/ApiBaseline/compare.py::compare', str(runtime), str(checker)], 'exit': api['checkerExit'], 'expectedExit': 0})
            summary['actualApi'] = {'members': len(api['provenance']), 'assemblySha256': api['assemblySha256']}
            # A real public metadata mutation must be rejected by the frozen baseline.
            # Model baseline preservation above is exact, not a source-string claim.
            mutation = tmp/'mutation.cs'; mutation.write_text('namespace Xemele; public class UnexpectedContract { }')
            text = run('dotnet', 'build', ROOT/'docs/reports/M2-contracts/Projects/Resolver/Resolver.csproj',
                       *output_args, f'-p:ProbeSource={mutation}')
            run('dotnet', checker, 'compare', artifacts/'bin/Resolver/release/M2Resolver.dll',
                FIXTURE/'Resolver.golden.txt', expected=1)
            summary['apiMutationDetected'] = True
            xml = tmp/'runtime-tests.xml'
            tests = artifacts/'bin/Xemele.Tests/release/Xemele.Tests.dll'
            text = run('dotnet', tests, '-noColor', '-result-xml', xml)
            tests_xml = list(ET.parse(xml).iter('test'))
            assert tests_xml and all(t.get('result') == 'Pass' for t in tests_xml), 'runtime failure/skip or empty results'
            summary['runtimePreservation'] = {'total': len(tests_xml), 'passed': len(tests_xml), 'failed': 0, 'skipped': 0}
            summary['testsAssemblySha256'] = digest(tests)
            text = run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tools/planning', '-p', 'test_tasks.py')
            summary['planningTests'] = int(re.search(r'Ran (\d+) tests', text)[1])
            summary['planningValidation'] = run('python3', '-B', 'tools/planning/tasks.py', 'validate').strip()
            run('git', 'diff', '--check')
            # Committed, staged, working and untracked paths must remain owned.
            paths = set(run('git', 'diff', '--name-only', CLAIM, 'HEAD').splitlines())
            paths.update(run('git', 'diff', '--name-only', 'HEAD').splitlines())
            paths.update(run('git', 'ls-files', '--others', '--exclude-standard').splitlines())
            owned = json.loads((ROOT/'docs/tasks/M2-version-contract.json').read_text())['owns']
            owned.append('docs/tasks/M2-version-contract.json')
            assert all(any(p.startswith(o) if o.endswith('/') else p == o for o in owned) for p in paths), sorted(paths)
            summary['ownedPaths'] = sorted(paths)
        summary['temporaryOutputsRemoved'] = True
        summary['result'] = 'passed'
    except Exception as error:
        summary['failure'] = str(error)
    print(json.dumps(summary, indent=2), flush=True)
    return 0 if summary['result'] == 'passed' else 1


if __name__ == '__main__':
    sys.exit(main())
