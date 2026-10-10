#!/usr/bin/env python3
"""Scoped version-contract verification; raw outputs exist only in auto-cleaned tmp.

Prints concise counts and identities. Never writes logs/XML/baselines to the repo.
Stops on the first unexpected result; failed/skipped checks are not passes.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
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
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit-base', default=CLAIM)
    parser.add_argument('--expected-branch', default='codex/m2-version-contract')
    args = parser.parse_args()
    if not re.fullmatch('[0-9a-f]{40}', args.audit_base):
        parser.error('--audit-base must be a full revision')
    env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0')
    results = []
    summary = {'result': 'failed', 'checks': results}
    # Allocation children create private raw outputs under artifacts. Inventory
    # existing entries first; preserve all prior files and remove only this run's.
    raw_root = ROOT/'artifacts'
    existing_raw = set(raw_root.rglob('*')) if raw_root.exists() else set()
    raw_root_existed = raw_root.exists()
    def run(*command, expected=0):
        command = list(map(str, command))
        try:
            process = subprocess.run(command, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                                     stderr=subprocess.STDOUT, text=True, timeout=300)
        except subprocess.TimeoutExpired:
            results.append({'command': command, 'result': 'timeout', 'timeoutSeconds': 300})
            raise
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
        branch = run('git', 'branch', '--show-current').strip()
        assert branch == args.expected_branch, (branch, args.expected_branch)
        run('git', 'merge-base', '--is-ancestor', args.audit_base, 'HEAD')
        summary['auditBase'] = args.audit_base
        summary['branch'] = branch
        run('git', 'merge-base', '--is-ancestor', CLAIM, 'HEAD')
        assert json.loads((ROOT/'docs/tasks/M2-contracts.json').read_text())['status'] == 'verified'
        run('git', 'merge-base', '--is-ancestor', 'ab2fcabaf3cc984901f484c6fce1069c385e8c2f', 'HEAD')
        assert run('dotnet', '--version').strip() == '10.0.400'
        dotnet = Path(shutil.which('dotnet')).resolve()
        summary['sdk'] = {'version': '10.0.400', 'host': str(dotnet), 'hostSha256': digest(dotnet),
                          'runtimes': run('dotnet', '--list-runtimes').strip().splitlines()}
        input_roots = ['src/Xemele', 'tests/Xemele.Tests', 'tests/Xemele.TestUtilities']
        inputs = run('git', 'ls-files', '--', *input_roots, 'Directory.Build.props', 'Directory.Build.targets',
                     'Directory.Packages.props', 'global.json', 'NuGet.config').splitlines()
        inputs = [p for p in inputs if Path(p).suffix in ('.cs', '.csproj', '.props', '.targets', '.json', '.config')]
        assert inputs
        assert not run('git', 'diff', args.audit_base, '--', *inputs).strip(), 'runtime/test/build inputs changed'
        source_hashes = {}
        for name in inputs:
            committed = subprocess.check_output(['git', 'show', f'{revision}:{name}'], cwd=ROOT)
            assert (ROOT/name).read_bytes() == committed, name+' differs from Git'
            source_hashes[name] = hashlib.sha256(committed).hexdigest()
        summary['compiledInputCount'] = len(source_hashes)
        summary['compiledInputSha256'] = hashlib.sha256(json.dumps(source_hashes, sort_keys=True).encode()).hexdigest()
        # Must verify committed sources; evidence metadata may subsequently change.
        assert not run('git', 'diff', 'HEAD', '--', 'docs/specs/parser.md',
                       'docs/decisions/M2/xml-resources.md', 'tests/Contracts/Fixtures/M2/Version',
                       'docs/reports/M2-version-contract/verify.py').strip()
        with tempfile.TemporaryDirectory(prefix='tmp-', dir=ROOT/'docs/reports/M2-version-contract') as tmp:
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
            # Real compiled negative metadata: readonly and nullable changes must
            # both be detected against the exact independently compiled fixture.
            for mutation_name in ['nullable', 'readonly']:
                mutation_dir = tmp/mutation_name; mutation_dir.mkdir()
                mutation_paths = []
                for source in paths[:-2]:
                    content = source.read_text()
                    if mutation_name == 'nullable' and source.name == 'Dtd.cs.fixture':
                        assert 'XmlTextDeclaration(string? Version' in content
                        content = content.replace('XmlTextDeclaration(string? Version', 'XmlTextDeclaration(string Version')
                    if mutation_name == 'readonly' and source.name == 'Syntax.cs.fixture':
                        assert 'internal readonly ref struct XmlDeclarationView' in content
                        content = content.replace('internal readonly ref struct XmlDeclarationView', 'internal ref struct XmlDeclarationView')
                    target = mutation_dir/source.name; target.write_text(content); mutation_paths.append(target)
                mutation_project = mutation_dir/f'AbiMutation{mutation_name}.csproj'
                mutation_project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                    '<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>'
                    '<EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>' +
                    ''.join(f'<Compile Include="{escape(str(p))}" />' for p in mutation_paths) + '</ItemGroup></Project>')
                run('dotnet', 'build', mutation_project, *output_args)
                mutated = artifacts/f'bin/AbiMutation{mutation_name}/release/AbiMutation{mutation_name}.dll'
                failure = run('dotnet', model, mutated, expected=1)
                assert 'ABI ' in failure and 'mismatch' in failure, mutation_name
            summary['internalAbiMutationsDetected'] = ['nullable Version', 'readonly declaration view']
            sys.path.insert(0, str(ROOT/'tools/ApiBaseline'))
            from compare import compare
            # The shared discovery helper couples API and optional corpus materialization.
            # Validate an exact API-only snapshot; this task claims no corpus discovery.
            api_root = tmp/'api-snapshot'
            (api_root/'docs/api').mkdir(parents=True)
            api_sources = sorted((ROOT/'docs/api').glob('*.txt'))
            assert api_sources, 'approved API fragments missing'
            for source in api_sources:
                target = api_root/'docs/api'/source.name
                shutil.copyfile(source, target)
                assert source.read_bytes() == target.read_bytes()
            summary['apiSourceSha256'] = {str(p.relative_to(ROOT)): digest(p) for p in api_sources}
            summary['apiScope'] = 'all approved API fragments; no corpus-discovery claim'
            api = compare(api_root, runtime, checker)
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
            closures = [artifacts/'bin/Xemele/release', artifacts/'bin/Xemele.Tests/release']
            def closure():
                return {str(p.relative_to(artifacts)): digest(p) for d in closures for p in sorted(d.rglob('*'))
                        if p.is_file()}
            before = closure()
            assert before
            summary['outputClosureFiles'] = len(before)
            summary['outputClosureSha256'] = hashlib.sha256(json.dumps(before, sort_keys=True).encode()).hexdigest()
            xml = tmp/'runtime-tests.xml'
            tests = artifacts/'bin/Xemele.Tests/release/Xemele.Tests.dll'
            text = run('dotnet', tests, '-noColor', '-result-xml', xml)
            tests_xml = list(ET.parse(xml).iter('test'))
            assert len(tests_xml) == 4594 and all(t.get('result') == 'Pass' for t in tests_xml), 'runtime failure/skip or lost inherited tests'
            identities = sorted(t.get('name') for t in tests_xml)
            assert len(set(identities)) == 4594, 'duplicate runtime test identities'
            summary['runtimeTestIdentitySha256'] = hashlib.sha256(json.dumps(identities).encode()).hexdigest()
            assert closure() == before, 'runtime/test output closure changed during execution'
            summary['outputClosureUnchanged'] = True
            summary['runtimePreservation'] = {'total': len(tests_xml), 'passed': len(tests_xml), 'failed': 0, 'skipped': 0}
            summary['testsAssemblySha256'] = digest(tests)
            text = run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tools/planning', '-p', 'test_tasks.py')
            summary['planningTests'] = int(re.search(r'Ran (\d+) tests', text)[1])
            summary['planningValidation'] = run('python3', '-B', 'tools/planning/tasks.py', 'validate').strip()
            run('git', 'diff', '--check', args.audit_base, 'HEAD')
            run('git', 'diff', '--check')
            run('git', 'diff', '--cached', '--check')
            # Committed, staged, working and untracked paths must remain owned.
            paths = set(run('git', 'diff', '--name-only', args.audit_base, 'HEAD').splitlines())
            paths.update(run('git', 'diff', '--name-only', 'HEAD').splitlines())
            paths.update(run('git', 'diff', '--cached', '--name-only').splitlines())
            paths.update(p for p in run('git', 'ls-files', '--others', '--exclude-standard').splitlines()
                         if not (ROOT/p).is_relative_to(tmp))
            owned = json.loads((ROOT/'docs/tasks/M2-version-contract.json').read_text())['owns']
            owned.append('docs/tasks/M2-version-contract.json')
            assert all(any(p.startswith(o) if o.endswith('/') else p == o for o in owned) for p in paths), sorted(paths)
            summary['ownedPaths'] = sorted(paths)
        summary['temporaryOutputsRemoved'] = True
        summary['result'] = 'passed'
    except Exception as error:
        summary['temporaryOutputsRemoved'] = True
        summary['failure'] = str(error)
    finally:
        # Inspect generated child summaries before discarding this run's raw data.
        new_raw = set(raw_root.rglob('*')) - existing_raw if raw_root.exists() else set()
        child_summaries = []
        for path in sorted(new_raw):
            if path.is_file() and path.name.endswith('.report.json'):
                data = json.loads(path.read_text())
                child_summaries.append({'task': path.relative_to(raw_root).parts[0], 'mode': data.get('Mode'),
                                        'sha256': digest(path)})
        summary['allocationChildSummaries'] = child_summaries
        for path in sorted(new_raw, key=lambda p: len(p.parts), reverse=True):
            if path.is_file() or path.is_symlink(): path.unlink()
            elif path.is_dir(): path.rmdir()
        if not raw_root_existed and raw_root.exists(): raw_root.rmdir()
        summary['newRawOutputsRemoved'] = True
    # Large input-list commands are recorded by their argument identity, not copied raw.
    for check in results:
        if len(check['command']) > 20:
            arguments = check['command']
            check['command'] = arguments[:4] + ['<scoped compiled inputs>']
            check['argumentCount'] = len(arguments)
            check['argumentsSha256'] = hashlib.sha256(json.dumps(arguments).encode()).hexdigest()
    print(json.dumps(summary, indent=2), flush=True)
    return 0 if summary['result'] == 'passed' else 1


if __name__ == '__main__':
    sys.exit(main())
