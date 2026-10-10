#!/usr/bin/env python3
"""Bounded contract checks; no parser execution, corpus import or measurements."""
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
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[3]
CLAIM = '26996cf53412f6d440b6dd72b31fa30280132c53'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
SDK = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
FIXTURES = ROOT/'tests/Contracts/Fixtures'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit-base', default=CLAIM)
    parser.add_argument('--expected-branch', default='codex/m2-undeclared-contract')
    parser.add_argument('--output', type=Path, default=ROOT/'docs/reports/M2-undeclared-contract/latest-summary.json')
    args = parser.parse_args()
    env = dict(os.environ, DOTNET_ROOT=str(SDK), DOTNET_CLI_USE_MSBUILD_SERVER='0')
    env['PATH'] = str(SDK) + os.pathsep + env.get('PATH', '')
    summary = {'result': 'failed', 'scope': 'contract/model and metadata preservation only', 'checks': []}
    temporary = None

    def run(*command, expected=0):
        command = list(map(str, command))
        process = subprocess.run(command, cwd=ROOT, env=env, text=True,
                                 stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=300)
        summary['checks'].append({'command': command, 'exit': process.returncode, 'expectedExit': expected})
        if process.returncode != expected:
            print(process.stdout[-6000:], file=sys.stderr, flush=True)
            raise RuntimeError('Unexpected command exit: ' + str(command))
        return process.stdout

    def digest(path):
        return hashlib.sha256(Path(path).read_bytes()).hexdigest()

    def project(path, sources, executable=False):
        path.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
            '<EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>enable</Nullable>'
            '<ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>' +
            ('<OutputType>Exe</OutputType>' if executable else '') + '</PropertyGroup><ItemGroup>' +
            ''.join('<Compile Include="' + escape(str(p)) + '" />' for p in sources) +
            '</ItemGroup></Project>')

    try:
        summary['revision'] = run('git', 'rev-parse', 'HEAD').strip()
        assert run('git', 'branch', '--show-current').strip() == args.expected_branch
        run('git', 'merge-base', '--is-ancestor', args.audit_base, 'HEAD')
        run('git', 'merge-base', '--is-ancestor', CLAIM, 'HEAD')
        own = json.loads((ROOT/'docs/tasks/M2-undeclared-contract.json').read_text())
        for dep in own['depends_on']:
            record = json.loads((ROOT/f'docs/tasks/{dep}.json').read_text())
            assert record['status'] == 'verified'
            run('git', 'merge-base', '--is-ancestor', record['integration_revision'], 'HEAD')
        assert not run('git', 'diff', args.audit_base, '--', 'src', 'docs/api', 'Directory.Build.props',
                       'Directory.Packages.props', 'global.json', 'NuGet.config').strip(), 'preserved inputs changed'
        committed = run('git', 'ls-files', '--', 'src/Xemele', 'docs/api',
                        'tests/Contracts/Fixtures', 'samples/M2-undeclared-contract',
                        'Directory.Build.props', 'Directory.Packages.props', 'global.json', 'NuGet.config').splitlines()
        source_hashes = {}
        for name in committed:
            data = subprocess.check_output(['git', 'show', f"{summary['revision']}:{name}"], cwd=ROOT)
            assert (ROOT/name).read_bytes() == data, 'uncommitted source: ' + name
            source_hashes[name] = hashlib.sha256(data).hexdigest()
        summary['sourceInputCount'] = len(source_hashes)
        summary['sourceInputSha256'] = hashlib.sha256(json.dumps(source_hashes, sort_keys=True).encode()).hexdigest()
        summary['proposalSourceSha256'] = {p: h for p, h in source_hashes.items() if '/Undeclared/' in p}

        # Read exactly the original resource, plus its inventory/ledger entries.
        relative = 'xmlconf/eduni/errata-3e/E13.xml'
        resource = Path(os.environ.get('XEMELE_CORPUS_RESOURCES',
            '/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources'))/relative
        data = resource.read_bytes()
        observed = digest(resource)
        assert observed == '40470ff5f2281476ed4521e066d85122e9cc240f1fa916f97bd095644540fa14'
        inventory = json.loads((ROOT/'tests/corpora/xml/inventory.json').read_text())
        originals = [c for c in inventory['cases'] if c['inputPath'] == relative]
        assert len(originals) == 1
        original = originals[0]
        ledger = json.loads((ROOT/'tests/corpora/xml/m2-applicability.json').read_text())
        applicable = next(c for c in ledger['cases'] if c['id'] == original['id'])
        assert original['inputSha256'] == applicable['inputSha256'] == observed
        assert applicable['inputPath'] == relative and applicable['expectedOutcomes'] == ['accepted']
        assert b'%pe;' in data and b'&ent2;' in data and b'<?xml' not in data
        index = data.index(b'&ent2;')
        line = data[:index].count(b'\n') + 1
        column = index - data.rfind(b'\n', 0, index)
        summary['originalE13'] = {'id': original['id'], 'inputPath': relative, 'sha256': observed,
            'bytes': len(data), 'sourceUri': applicable['sourceUri'], 'expectedOutcomes': applicable['expectedOutcomes'],
            'reference': {'name': 'ent2', 'lexical': '&ent2;', 'line': line, 'column': column, 'byteOffset': index},
            'assessment': 'internal PE referenced; standalone absent; missing content entity validity-only'}
        summary['sdk'] = {'version': run('dotnet', '--version').strip(), 'hostSha256': digest(SDK/'dotnet'),
            'runtimes': run('dotnet', '--list-runtimes').strip().splitlines()}
        assert summary['sdk']['version'] == '10.0.400'

        with tempfile.TemporaryDirectory(prefix='tmp-', dir=ROOT/'docs/reports/M2-undeclared-contract') as directory:
            temporary = Path(directory)
            artifacts = temporary/'build'
            build = [*FLAGS, '--artifacts-path', artifacts]
            run('dotnet', 'build', ROOT/'src/Xemele/Xemele.csproj', *build)
            runtime = artifacts/'bin/Xemele/release/Xemele.dll'
            run('dotnet', 'build', ROOT/'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', *build)
            checker = artifacts/'bin/ApiCheck/release/ApiCheck.dll'
            api_root = temporary/'api-snapshot'
            (api_root/'docs/api').mkdir(parents=True)
            for source in (ROOT/'docs/api').glob('*.txt'):
                shutil.copyfile(source, api_root/'docs/api'/source.name)
            sys.path.insert(0, str(ROOT/'tools/ApiBaseline'))
            from compare import compose
            union, report = compose(api_root)
            golden = temporary/'api.txt'; golden.write_text(union)
            run('dotnet', checker, 'compare', runtime, golden)
            summary['actualPublicApi'] = {'members': len(report['provenance']), 'assemblySha256': digest(runtime),
                'baselineSha256': digest(golden)}

            models = {}
            for lane in ['Resolver', 'Dtd', 'Encoding', 'Entities', 'Recovery']:
                run('dotnet', 'build', ROOT/f'docs/reports/M2-contracts/Projects/{lane}/{lane}.csproj', *build)
                binary = artifacts/f'bin/{lane}/release/M2{lane}.dll'
                baseline = FIXTURES/f'M2/{lane}.golden.txt'
                run('dotnet', checker, 'compare', binary, baseline)
                models[lane] = {'members': len(baseline.read_text().splitlines()) - 1,
                    'binarySha256': digest(binary), 'baselineSha256': digest(baseline)}
            summary['frozenPublicModels'] = models
            run('dotnet', 'build', ROOT/'docs/reports/M2-contracts/Projects/Checks/Checks.csproj', *build)
            output = run('dotnet', artifacts/'bin/Checks/release/M2Checks.dll')
            summary['inheritedModelAssertions'] = int(re.search(r'Passed (\d+)', output)[1])

            sample = ROOT/'samples/M2-undeclared-contract/Sample.csproj'
            run('dotnet', 'build', sample, *build)
            consumer = artifacts/'bin/Sample/release/UndeclaredContract.dll'
            output = run('dotnet', consumer, runtime)
            summary['independentContractAssertions'] = int(re.search(r'Passed (\d+)', output)[1])
            summary['eventViewsPreserved'] = ['XmlDeclarationView', 'XmlNameView', 'XmlAttributeView', 'XmlNamespaceView']
            summary['consumerSha256'] = digest(consumer)

            negatives = [
                ('borrowed', 'class Probe { Xemele.Parsing.Syntax.XmlUnresolvedReferenceView Retained; }', 'CS8345'),
                ('owned', 'class Probe { void Use(Xemele.XmlUnresolvedReference r) { r.Name = "wrong"; } }', 'CS8852')]
            for name, source, error in negatives:
                probe = temporary/(name+'.cs'); probe.write_text(source)
                output = run('dotnet', 'build', sample, *build, f'-p:ProbeSource={probe}', expected=1)
                assert error in output, 'wrong expected compiler failure: ' + name
            summary['compilerNegativeControls'] = [n[2] for n in negatives]

            # Compile genuinely altered frozen metadata; the independent consumer
            # must identify the exact altered shape, not accept a source-text check.
            sources = [FIXTURES/f'{p}.cs.fixture' for p in
                ['M2/Common', 'M2/Options', 'M2/Resolver', 'M2/Dtd', 'M1/Tree', 'M2/Entities', 'M2/Syntax']]
            mutations = [('namespace-readonly', 'internal readonly ref struct XmlNamespaceView',
                          'internal ref struct XmlNamespaceView', 'XmlNamespaceView'),
                         ('declaration-nullability', 'XmlTextDeclaration(string? Version',
                          'XmlTextDeclaration(string Version', 'XmlTextDeclaration')]
            for name, before, after, type_name in mutations:
                folder = temporary/name; folder.mkdir()
                paths, changed = [], 0
                for i, source in enumerate(sources):
                    content = source.read_text()
                    if before in content:
                        content = content.replace(before, after); changed += 1
                    target = folder/f'{i}.cs'; target.write_text(content); paths.append(target)
                assert changed == 1
                proj = folder/(name+'.csproj'); project(proj, paths)
                run('dotnet', 'build', proj, *build)
                binary = artifacts/f'bin/{name}/release/{name}.dll'
                output = run('dotnet', consumer, binary, expected=1)
                assert 'Frozen ABI mismatch: ' in output and type_name in output
            summary['compiledAbiMutationsDetected'] = [m[0] for m in mutations]
            probe = temporary/'public-mutation.cs'
            probe.write_text('namespace Xemele; public class UnexpectedContract { }')
            run('dotnet', 'build', ROOT/'docs/reports/M2-contracts/Projects/Resolver/Resolver.csproj',
                *build, f'-p:ProbeSource={probe}')
            run('dotnet', checker, 'compare', artifacts/'bin/Resolver/release/M2Resolver.dll',
                FIXTURES/'M2/Resolver.golden.txt', expected=1)
            summary['publicMetadataNegativeDetected'] = True
            summary['outputBinarySha256'] = {str(p.relative_to(artifacts)): digest(p)
                for p in sorted(artifacts.glob('bin/*/release/*.dll'))}

        summary['temporaryOutputsRemoved'] = not temporary.exists()
        output = run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tools/planning', '-p', 'test_tasks.py')
        summary['planningTests'] = int(re.search(r'Ran (\d+) tests', output)[1])
        summary['planningValidation'] = run('python3', '-B', 'tools/planning/tasks.py', 'validate').strip()
        run('git', 'diff', '--check', args.audit_base, 'HEAD')
        run('git', 'diff', '--check')
        run('git', 'diff', '--cached', '--check')
        paths = set(run('git', 'diff', '--name-only', args.audit_base, 'HEAD').splitlines())
        paths.update(run('git', 'diff', '--name-only', 'HEAD').splitlines())
        paths.update(run('git', 'ls-files', '--others', '--exclude-standard').splitlines())
        owned = own['owns'] + ['docs/tasks/M2-undeclared-contract.json']
        assert all(any(p.startswith(o) if o.endswith('/') else p == o for o in owned) for p in paths), sorted(paths)
        summary['ownedPaths'] = sorted(paths)
        summary['result'] = 'passed'
    except Exception as error:
        summary['failure'] = str(error)
        summary['temporaryOutputsRemoved'] = temporary is None or not temporary.exists()
    args.output.write_text(json.dumps(summary, indent=2) + '\n')
    print(json.dumps(summary, indent=2), flush=True)
    return 0 if summary['result'] == 'passed' else 1


if __name__ == '__main__':
    sys.exit(main())
