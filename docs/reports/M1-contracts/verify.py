#!/usr/bin/env python3
"""Compilation and contract-model checks; never product conformance or performance."""
import argparse
import json
import os
from pathlib import Path
import platform
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'docs/reports/M1-contracts'
FIXTURES = ROOT / 'tests/Contracts/Fixtures/M1'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
LANES = ['Common', 'Tree', 'Input', 'Syntax', 'Serializer', 'Dom']
CHECKER = ROOT / 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
LOG = []

def run(*args, expected=0):
    args = [str(a) for a in args]
    print('$ ' + ' '.join(args), flush=True)
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, timeout=60)
    LOG.extend(['$ ' + ' '.join(args), result.stdout, f'exit={result.returncode}; expected={expected}'])
    if result.returncode != expected:
        raise AssertionError('\n'.join(LOG[-3:]))
    return result.stdout

def dll(lane):
    return REPORT / f'Projects/{lane}/bin/Release/net10.0/M1{lane}.dll'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--emit-reviewed-model-baselines', action='store_true')
    args = parser.parse_args()
    LOG.append(f'Host: {platform.platform()}, {platform.machine()}')
    LOG.append('Revision: ' + run('git', 'rev-parse', 'HEAD').strip())
    assert run('dotnet', '--version').strip() == json.loads((ROOT/'global.json').read_text())['sdk']['version']
    run('dotnet', 'build', 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', *FLAGS)
    for lane in LANES:
        run('dotnet', 'build', REPORT/f'Projects/{lane}/{lane}.csproj', *FLAGS)
        golden = FIXTURES / (lane + '.golden.txt')
        run('dotnet', CHECKER, 'emit' if args.emit_reviewed_model_baselines else 'compare', dll(lane), golden)
        rows = [json.loads(row) for row in golden.read_text().splitlines()[1:]]
        assert not any(any(forbidden in row['declaration'] for forbidden in
                          ['XmlResourceResolver', 'XmlDtdProcessing', 'XmlWriter', 'XmlReader', 'ParseWithDiagnostics']) for row in rows)
    common = {json.loads(row)['member']: json.loads(row)['declaration']
              for row in (FIXTURES/'Common.golden.txt').read_text().splitlines()[1:]}
    foundation = {json.loads(row)['member']: json.loads(row)['declaration']
                  for row in (ROOT/'tests/Contracts/Fixtures/foundation/M1.golden.txt').read_text().splitlines()[1:]}
    assert all(foundation.get(k) == v for k,v in common.items()), 'Changed M0 common contract'
    LOG.append(f'All {len(common)} frozen M0 common members preserved.')
    run('dotnet', 'build', REPORT/'Projects/Protocol/Protocol.csproj', *FLAGS)
    run('dotnet', dll('Protocol'))
    negatives = [
        ('Tree', 'tree-no-parser', 'namespace Negative; internal class Probe { void Use() { _ = Xemele.XmlDocument.Parse("<r/>"); } }', 'CS0117'),
        ('Serializer', 'serializer-no-parser', 'namespace Negative; internal class Probe { void Use() { _ = Xemele.XmlDocument.Parse("<r/>"); } }', 'CS0117'),
        ('Input', 'input-no-tree', 'namespace Negative; internal class Probe { void Use() { _ = new Xemele.XmlDocument(); } }', 'CS0234'),
        ('Syntax', 'syntax-no-forward-reader', 'namespace Negative; internal class Probe { void Use() { _ = typeof(Xemele.XmlReader); } }', 'CS0234'),
        ('Syntax', 'no-retained-view', 'namespace Negative; internal class Probe { private Xemele.Parsing.Syntax.XmlNameView retained; }', 'CS8345'),
        ('Syntax', 'no-retained-span', 'namespace Negative; internal class Probe { private ReadOnlySpan<char> retained; }', 'CS8345'),
        ('Syntax', 'reference-sink-required', 'namespace Negative; internal class Probe { void Use(Xemele.Parsing.Syntax.XmlParserCore core) { core.Append<int>([], [], true, 0, default); } }', 'CS0452'),
        ('Input', 'byte-count-not-scalar-count', 'namespace Negative; internal class Probe { int Use(Xemele.Parsing.Input.DecodeResult result) => result.Consumed; }', 'CS1061'),
        ('Syntax', 'offset-width', 'namespace Negative; internal class Probe { void Use(Xemele.Parsing.Syntax.XmlParserCore core, M1Usage.Sink sink) { core.Append([], new int[2], true, sink, default); } }', 'CS1503'),
        ('Dom', 'no-recovery-options', 'namespace Negative; internal class Probe { void Use() { _ = new Xemele.XmlParseOptions { Recover = true }; } }', 'CS0117'),
    ]
    with tempfile.TemporaryDirectory(prefix='xemele-m1-probes-') as directory:
        directory = Path(directory)
        for lane, name, source, code in negatives:
            probe = directory / (name + '.cs')
            probe.write_text(source)
            output = run('dotnet', 'build', REPORT/f'Projects/{lane}/{lane}.csproj', *FLAGS,
                         '-p:ProbeSource=' + str(probe), expected=1)
            assert code in output, (name, code, output)
        # Compile genuinely changed public signatures; use unchanged approved normalizer.
        tree = (FIXTURES/'Tree.cs.fixture').read_text()
        for name, edited, member in [
            ('handle-readonly', tree.replace('public readonly struct XmlNode :', 'public struct XmlNode :'), 'T:Xemele.XmlNode'),
            ('enumerator-kind', tree.replace('public struct Enumerator', 'public sealed class Enumerator'), 'T:Xemele.XmlNodeChildren.Enumerator'),
            ('optional-default', tree.replace('bool deep = true', 'bool deep = false'), 'M:Xemele.XmlNode.Clone(System.Boolean)'),
        ]:
            mutated = directory / (name + '.cs'); mutated.write_text(edited)
            project = directory / (name + '.csproj')
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
                               '<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>'
                               '<EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>Mutated</AssemblyName>'
                               '</PropertyGroup><ItemGroup><Compile Include="' + str(mutated) + '" />'
                               '<Compile Include="' + str(ROOT/'tests/Contracts/Fixtures/foundation/Common.cs.fixture') + '" />'
                               '</ItemGroup></Project>')
            run('dotnet', 'build', project, *FLAGS)
            assembly = directory/'bin/Release/net10.0/Mutated.dll'
            run('dotnet', CHECKER, 'compare', assembly, FIXTURES/'Tree.golden.txt', expected=1)
            fragment = directory / (name + '.txt')
            run('dotnet', CHECKER, 'emit', assembly, fragment)
            before = {json.loads(line)['member']: json.loads(line)['declaration'] for line in (FIXTURES/'Tree.golden.txt').read_text().splitlines()[1:]}
            after = {json.loads(line)['member']: json.loads(line)['declaration'] for line in fragment.read_text().splitlines()[1:]}
            assert before[member] != after[member], name
        # Restore unmutated lane outputs after deliberately failing source builds.
    for lane in LANES:
        run('dotnet', 'build', REPORT/f'Projects/{lane}/{lane}.csproj', *FLAGS)
        run('dotnet', CHECKER, 'compare', dll(lane), FIXTURES/(lane+'.golden.txt'))
    vectors = json.loads((FIXTURES/'serialization-vectors.json').read_text())
    assert len({v['id'] for v in vectors}) == len(vectors)
    for vector in vectors:
        assert vector['expectedHex'] == vector['bomHex'] + vector['byteText'].encode(vector['encoding']).hex()
    LOG.append(f'Passed {len(negatives)} compilation-rejection probes, 3 compiled ABI mutation probes, {len(vectors)} independent vector encoding checks.')
    # Preserve existing foundation executable behavior without rewriting historical reports.
    run('dotnet', 'build', 'docs/reports/M0-contracts/Checks/Checks.csproj', *FLAGS)
    run('dotnet', 'test', '--project', 'docs/reports/M0-contracts/Checks/Checks.csproj', '-c', 'Release', '--no-build', '--no-restore')
    for stage in ['M1', 'M2']:
        run('dotnet', CHECKER, 'compare', ROOT/f'docs/reports/M0-contracts/Fixtures/{stage}/bin/Release/net10.0/Foundation{stage}.dll',
            ROOT/f'tests/Contracts/Fixtures/foundation/{stage}.golden.txt')
    run('dotnet', 'build', 'Xemele.slnx', *FLAGS)
    run('dotnet', 'test', '--solution', 'Xemele.slnx', '-c', 'Release', '--no-build', '--no-restore')
    run('python3', 'tools/planning/tasks.py', 'validate')
    run('git', 'diff', '--check')
    LOG.append('Passed: declarations/usage/protocol models/M0 preservation. No production XML or allocation behavior was executed.')

try:
    main()
finally:
    output = ROOT/'artifacts/M1-contracts/checks.txt'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text('\n'.join(LOG) + '\n')
    print('\n'.join(LOG))
