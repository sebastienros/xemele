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
import signal
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[3]
CLAIM = '5f70770facf88b7670fe38c0ac63283feca3219f'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
FIXTURE = ROOT / 'tests/Contracts/Fixtures/M2'



def finish_raw_outputs(raw_root, existing_raw, raw_root_existed, summary):
    """Inspect then attempt every new-path cleanup; retain any primary failure."""
    inspection_errors, cleanup_errors, child_summaries = [], [], []
    try:
        new_raw = set(raw_root.rglob('*')) - existing_raw if raw_root.exists() else set()
    except Exception as error:
        new_raw = set()
        cleanup_errors.append({'path': str(raw_root), 'operation': 'enumerate', 'error': str(error)})
    for path in sorted(new_raw):
        try:
            if path.is_file() and path.name.endswith('.report.json'):
                data = json.loads(path.read_text())
                child_summaries.append({'task': path.relative_to(raw_root).parts[0], 'mode': data.get('Mode'),
                                        'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        except Exception as error:
            inspection_errors.append({'path': str(path), 'error': str(error)})
    for path in sorted(new_raw, key=lambda p: len(p.parts), reverse=True):
        try:
            if path.is_file() or path.is_symlink(): path.unlink()
            elif path.is_dir(): path.rmdir()
        except Exception as error:
            cleanup_errors.append({'path': str(path), 'operation': 'remove', 'error': str(error)})
    try:
        if not raw_root_existed and raw_root.exists(): raw_root.rmdir()
    except Exception as error:
        cleanup_errors.append({'path': str(raw_root), 'operation': 'remove root', 'error': str(error)})
    summary['allocationChildSummaries'] = child_summaries
    summary['inspectionErrors'] = inspection_errors
    summary['cleanupErrors'] = cleanup_errors
    summary['newRawOutputsRemoved'] = not cleanup_errors
    if inspection_errors or cleanup_errors:
        # A failed check keeps its original cause; cleanup cannot replace it.
        summary.setdefault('failure', 'Raw output inspection/cleanup failed; see scoped errors.')
        summary['result'] = 'failed'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit-base', default=CLAIM)
    parser.add_argument('--expected-branch', default='codex/m2-version-declarations')
    args = parser.parse_args()
    if not re.fullmatch('[0-9a-f]{40}', args.audit_base):
        parser.error('--audit-base must be a full revision')
    env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', XEMELE_DOTNET=str(Path(shutil.which('dotnet')).resolve()), XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/')
    results = []
    summary = {'result': 'failed', 'checks': results}
    # Allocation children create private raw outputs under artifacts. Inventory
    # existing entries first; preserve all prior files and remove only this run's.
    raw_root = ROOT/'artifacts'
    existing_raw = set(raw_root.rglob('*')) if raw_root.exists() else set()
    raw_root_existed = raw_root.exists()
    temp_output = None
    def run(*command, expected=0):
        command = list(map(str, command))
        try:
            child = subprocess.Popen(command, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                                     stderr=subprocess.STDOUT, text=True, start_new_session=True)
            try:
                output, _ = child.communicate(timeout=300)
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGKILL)
                child.communicate()
                raise
            process = subprocess.CompletedProcess(command, child.returncode, output)
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
        for prerequisite in ['481a6090f67bb2191bb7a2d6eeebe19cb9e0b787','98c99e9895c3c4552b612f0e3fc5692324ecef08','bbf3ea65ebe68a2433b867758378b9d0263175d2']:
            run('git','merge-base','--is-ancestor',prerequisite,'HEAD')
        assert run('dotnet', '--version').strip() == '10.0.400'
        dotnet = Path(shutil.which('dotnet')).resolve()
        summary['sdk'] = {'version': '10.0.400', 'host': str(dotnet), 'hostSha256': digest(dotnet),
                          'runtimes': run('dotnet', '--list-runtimes').strip().splitlines()}
        input_roots = ['src/Xemele', 'tests/Xemele.Tests', 'tests/Xemele.TestUtilities', 'tests/Xemele.Conformance.Tests', 'tests/Xemele.Differential.Tests', 'samples/M2-version-declarations', 'benchmarks/Xemele.Benchmarks', 'docs/reports/M2-version-declarations', 'tools/ReferenceRunners/Bcl', 'docs/reports/M0-contracts/ApiCheck']
        inputs = run('git', 'ls-files', '--', *input_roots, 'Directory.Build.props', 'Directory.Build.targets',
                     'Directory.Packages.props', 'global.json', 'NuGet.config').splitlines()
        inputs = [p for p in inputs if Path(p).suffix in ('.cs', '.csproj', '.props', '.targets', '.json', '.config')]
        assert inputs
        assert not run('git', 'diff', 'HEAD', '--', *inputs).strip(), 'runtime/test/build inputs differ from frozen commit'
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
        control_output = run('python3', '-B', 'docs/reports/M2-version-contract/cleanup_checks.py')
        summary['cleanupControlAssertions'] = int(re.search(r'Passed (\d+)', control_output)[1])
        with tempfile.TemporaryDirectory(prefix='tmp-', dir=ROOT/'docs/reports/M2-version-declarations') as tmp:
            tmp = Path(tmp)
            temp_output = tmp
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
            assert all(t.get('result') == 'Pass' for t in tests_xml), 'runtime failure/skip'
            inherited = [t.get('name') for t in tests_xml if t.get('type') != 'Xemele.Tests.Versions.VersionTests']
            assert len(inherited) == 4593
            transitions = [n for n in inherited if "<?xml version='1.1' encoding='X'?>" in n and 'expected: "Declared"' in n]
            assert len(transitions) == 70, len(transitions)
            inherited = [n.replace('expected: "Declared"', 'expected: "Rejected"') if n in transitions else n for n in inherited]
            inherited.append('Xemele.Tests.Syntax.SyntaxTests.InvalidSyntaxRejectsAtEverySplitWithoutReplayingCallbacks(xml: "<?xml version=\'1.1\'?><r/>", code: "XML.Syntax")')
            baseline = json.loads((ROOT/'docs/reports/M2-version-contract/integrated-summary.json').read_text())
            inherited_digest = hashlib.sha256(json.dumps(sorted(inherited)).encode()).hexdigest()
            assert inherited_digest == baseline['runtimeTestIdentitySha256'], inherited_digest
            summary['preservation'] = {'inherited': 4594, 'exactInheritedNameDigest': inherited_digest, 'authorizedSyntaxTransitions': 1, 'authorizedFramingRows': len(transitions), 'newVersionTests': len(tests_xml)-4593}
            identities = sorted(t.get('name') for t in tests_xml)
            assert len(set(identities)) == len(tests_xml), 'duplicate runtime test identities'
            summary['runtimeTestIdentitySha256'] = hashlib.sha256(json.dumps(identities).encode()).hexdigest()
            assert closure() == before, 'runtime/test output closure changed during execution'
            summary['outputClosureUnchanged'] = True
            summary['runtimePreservation'] = {'total': len(tests_xml), 'passed': len(tests_xml), 'failed': 0, 'skipped': 0}
            summary['testsAssemblySha256'] = digest(tests)
            env['XEMELE_CORPUS_RESULTS'] = str(tmp/'case-results')
            run('dotnet', 'build', 'tools/ReferenceRunners/Bcl/BclReference.csproj', *output_args)
            runner = artifacts/'bin/BclReference/release/BclReference.dll'
            env['XEMELE_BCL_RUNNER'] = str(runner)
            summary['additionalSuites'] = {}
            for assembly, expected_count, expected_digest in [('Xemele.Conformance.Tests',255,'a43104c9986029e0680218003fc7d820c2583c42b53fe16a323f2cab04e634dc'),('Xemele.Differential.Tests',299,'845b5c10e9d4952e309b2d419fcc429fe568c8496889dce7fe6ab319389cdc6e')]:
                run('dotnet', 'build', f'tests/{assembly}/{assembly}.csproj', *output_args)
                test_dll = artifacts/f'bin/{assembly}/release/{assembly}.dll'
                suite_xml = tmp/(assembly+'.xml')
                frozen = {str(f): digest(f) for f in test_dll.parent.rglob('*') if f.is_file()}
                run('dotnet', test_dll, '-noColor', '-result-xml', suite_xml)
                rows = list(ET.parse(suite_xml).iter('test'))
                ids = {(r.get('type'),r.get('method'),r.get('name')) for r in rows}
                assert len(ids) == len(rows) == expected_count and all(r.get('result') == 'Pass' for r in rows)
                identity_digest = hashlib.sha256(json.dumps(sorted(ids),ensure_ascii=True,separators=(',',':')).encode()).hexdigest()
                assert identity_digest == expected_digest
                assert frozen == {str(f): digest(f) for f in test_dll.parent.rglob('*') if f.is_file()}
                summary['additionalSuites'][assembly] = {'total':len(rows),'passed':len(rows),'identitySha256':identity_digest,'assemblySha256':digest(test_dll)}
            run('dotnet', 'build', 'samples/M2-version-declarations/Sample.csproj', *output_args)
            sample = artifacts/'bin/Sample/release/Xemele.SyntaxSample.dll'
            sample_output = run('dotnet', sample, '--oracle')
            summary['sourceConsumer'] = sample_output.strip()
            summary['boundaryOracle'] = run('dotnet', sample, '--boundary-oracle').strip()
            summary['sourceConsumerIdentity'] = json.loads(run('dotnet', sample, '--identity'))
            run('dotnet', 'pack', 'src/Xemele/Xemele.csproj', *output_args, '--no-build', '-o', tmp/'feed')
            packages = list((tmp/'feed').glob('*.nupkg')); assert len(packages) == 1
            summary['packageSha256'] = digest(packages[0])
            package_output = tmp/'package-build'
            run('dotnet','build','samples/M2-version-declarations/Sample.csproj',*FLAGS,'--artifacts-path',package_output,'-p:UsePackage=true','-p:ManagePackageVersionsCentrally=false',f'-p:RestoreSources={tmp}/feed',f'-p:RestorePackagesPath={tmp}/packages','-p:NuGetAudit=false')
            package_sample = package_output/'bin/Sample/release/Xemele.SyntaxSample.dll'
            assert run('dotnet',package_sample,'--oracle') == sample_output
            summary['packageConsumerIdentity'] = json.loads(run('dotnet',package_sample,'--identity'))
            assert summary['packageConsumerIdentity']['Product'] == summary['sourceConsumerIdentity']['Product']
            run('dotnet','build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*output_args)
            benchmark = artifacts/'bin/Xemele.Benchmarks/release/Xemele.Benchmarks.dll'
            listing = run('dotnet',benchmark,'--list','flat','--filter','*DeclarationVersionBenchmarks*')
            assert 'DeclarationVersionBenchmarks.ParseDocument' in listing and 'TextDeclarationVersionBenchmarks.FrameOwned' in listing
            summary['benchmarkDiscovery'] = listing.strip()
            run('dotnet','build','docs/reports/M2-version-declarations/BenchmarkDriver.csproj',*output_args)
            summary['benchmarkOracles'] = run('dotnet',artifacts/'bin/BenchmarkDriver/release/Xemele.Benchmarks.dll','--oracle').strip()
            assert all((ROOT/n).read_bytes() == subprocess.check_output(['git','show',f'{revision}:{n}'],cwd=ROOT) for n in source_hashes)
            summary['sourceFreezeUnchanged'] = True

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
            owned = json.loads((ROOT/'docs/tasks/M2-version-declarations.json').read_text())['owns']
            owned.append('docs/tasks/M2-version-declarations.json')
            assert all(any(p.startswith(o) if o.endswith('/') else p == o for o in owned) for p in paths), sorted(paths)
            summary['ownedPaths'] = sorted(paths)
        summary['temporaryOutputsRemoved'] = True
        summary['result'] = 'passed'
    except Exception as error:
        summary['temporaryOutputsRemoved'] = temp_output is None or not temp_output.exists()
        summary['failure'] = str(error)
    finally:
        finish_raw_outputs(raw_root, existing_raw, raw_root_existed, summary)
    # Large input-list commands are recorded by their argument identity, not copied raw.
    for check in results:
        if len(check['command']) > 20:
            arguments = check['command']
            check['command'] = arguments[:4] + ['<scoped compiled inputs>']
            check['argumentCount'] = len(arguments)
            check['argumentsSha256'] = hashlib.sha256(json.dumps(arguments).encode()).hexdigest()
    (ROOT/'docs/reports/M2-version-declarations/latest-summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    print(json.dumps(summary, indent=2), flush=True)
    return 0 if summary['result'] == 'passed' else 1


if __name__ == '__main__':
    sys.exit(main())
