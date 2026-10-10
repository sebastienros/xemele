#!/usr/bin/env python3
"""Portable focused API/contract checks; never regenerate approved fragments."""
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[3]
CHECKER = ROOT / 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
GOLDENS = ROOT / 'tests/Contracts/Fixtures/foundation'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
LOG = []
sys.path.insert(0, str(ROOT / 'tools/ApiBaseline'))
from compare import compose


def run(*args, expected=0):
    args = [str(arg) for arg in args]
    LOG.append('$ ' + ' '.join(args))
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, timeout=120)
    LOG.extend([result.stdout, f'exit={result.returncode}; expected={expected}'])
    print('\n'.join(LOG[-3:]), flush=True)
    if result.returncode != expected:
        raise AssertionError(f'Unexpected exit for {args}')
    return result.stdout


def rows(path):
    return [json.loads(line) for line in path.read_text(encoding='utf-8').splitlines()[1:]]


def fragment(root, name, entries):
    path = root / 'docs/api' / (name + '.txt')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text('xemele-api-v1\n' + ''.join(json.dumps(row, ensure_ascii=False, sort_keys=True,
                    separators=(',', ':')) + '\n' for row in sorted(entries, key=lambda row: row['id'])), encoding='utf-8')
    return path


def assembly(stage):
    name = stage if stage == 'ApiProbe' else 'Foundation' + stage
    return ROOT / f'docs/reports/M0-contracts/Fixtures/{stage}/bin/Release/net10.0/{name}.dll'


def union(root, dll, directory, name, expected=0, bootstrap=False):
    report = directory / (name + '.json')
    run(sys.executable, '-B', ROOT / 'tools/ApiBaseline/compare.py', '--root', root,
        '--assembly', dll, '--checker', CHECKER, '--report', report,
        *(['--bootstrap-empty-api'] if bootstrap else []), expected=expected)
    if report.exists():
        return json.loads(report.read_text(encoding='utf-8'))
    return None


def fixture_checks():
    m1, m2 = rows(GOLDENS / 'M1.golden.txt'), rows(GOLDENS / 'M2.golden.txt')
    later = {row['member']: row['declaration'] for row in m2}
    assert all(later.get(row['member']) == row['declaration'] for row in m1)
    assert not any('Resolver' in row['declaration'] or 'DtdProcessing' in row['declaration'] or
                   '.Recover ' in row['declaration'] for row in m1)
    LOG.append(f'Additive stages: {len(m1)} M1 members preserved within {len(m2)} M2 members.')
    with tempfile.TemporaryDirectory(prefix='xemele-api-wiring-') as temp:
        directory = Path(temp)
        for stage, entries in [('M1', m1), ('M2', m2), ('ApiProbe', rows(GOLDENS / 'ApiProbe.golden.txt'))]:
            root = directory / stage
            # Split actual goldens and assign distinct valid inventory IDs. File order
            # and ID order deliberately differ from compiled member order.
            remapped = [dict(row, id=f'entry-{len(entries)-index:04}') for index, row in enumerate(entries)]
            files = [fragment(root, 'a', remapped[::2]), fragment(root, 'z', remapped[1::2])]
            snapshots = [path.read_bytes() for path in files]
            report = union(root, assembly(stage), directory, stage)
            assert report['matched'] and len(report['provenance']) == len(entries)
            assert all(row['id'] != row['member'] for row in report['provenance'])
            assert {row['source'] for row in report['provenance']} == {'docs/api/a.txt', 'docs/api/z.txt'}
            canonical, _ = compose(root)
            assert [row['member'] for row in report['provenance']] == sorted(row['member'] for row in entries)
            assert [json.loads(line)['id'] for line in canonical.splitlines()[1:]] == sorted(row['member'] for row in entries)
            assert snapshots == [path.read_bytes() for path in files]
        root = directory / 'negative'
        original = fragment(root, 'baseline', m1)
        for name, entries in [('missing', m1[:-1]),
                              ('added', m1 + [dict(id='T:NotShipped', member='T:NotShipped', declaration='public class NotShipped')]),
                              ('changed', [dict(row, declaration=row['declaration'] + '; changed') if index == 0 else row
                                           for index, row in enumerate(m1)])]:
            fragment(root, 'baseline', entries)
            result = union(root, assembly('M1'), directory, name, expected=1)
            assert result and not result['matched'] and 'API mismatch' in result['checkerOutput']
        original.unlink()
        project = root / 'src/Xemele/Xemele.csproj'
        project.parent.mkdir(parents=True)
        project.write_text('<Project />')
        # A source-empty inventory alone cannot exempt a nonempty actual assembly.
        result = union(root, assembly('M1'), directory, 'false-bootstrap', expected=1, bootstrap=True)
        assert result and not result['matched']
        union(root, assembly('M1'), directory, 'implicit-bootstrap', expected=1)
        project.unlink()
        union(root, assembly('M1'), directory, 'missing-project', expected=1, bootstrap=True)
        project.write_text('<Project />')
        (project.parent / 'Runtime.cs').write_text('internal class Runtime {}')
        union(root, assembly('M1'), directory, 'source-bootstrap', expected=1, bootstrap=True)
        LOG.append('Compiled union checks: 3 fragmented positive assemblies; missing/added/changed mismatch exits; explicit bootstrap fails for nonempty compiled API, absent exemption/project and runtime sources; provenance/ordering/no rewrites passed.')
        # Reuse the approved real signature mutation fixture, without editing it.
        probe_project = ROOT / 'docs/reports/M0-contracts/Fixtures/ApiProbe/ApiProbe.csproj'
        try:
            run('dotnet', 'build', probe_project, *FLAGS, '-p:DefineConstants=MUTATED')
            run('dotnet', CHECKER, 'compare', assembly('ApiProbe'), GOLDENS / 'ApiProbe.golden.txt', expected=1)
            emitted = directory / 'mutated.txt'
            run('dotnet', CHECKER, 'emit', assembly('ApiProbe'), emitted)
            before = {row['member']: row['declaration'] for row in rows(GOLDENS / 'ApiProbe.golden.txt')}
            after = {row['member']: row['declaration'] for row in rows(emitted)}
            sample = 'T:NormalizationProbe.Sample`1'
            prop = 'P:NormalizationProbe.Sample`1.Text'
            call = 'M:NormalizationProbe.Sample`1.Call(System.Int32)'
            constant = 'F:NormalizationProbe.Sample`1.Constant'
            assert 'where T : class' in before[sample] and 'where T : struct' in after[sample]
            assert 'interface NormalizationProbe.IMarker' in before[sample] and 'interface NormalizationProbe.IMarker' not in after[sample]
            assert 'sealed=False' in before[sample] and 'sealed=True' in after[sample]
            assert 'System.String?' in before[prop] and 'System.String?' not in after[prop]
            assert 'set=Protected' in before[prop] and 'set=Public' in after[prop]
            assert 'value = 1' in before[call] and 'value = 2' in after[call]
            assert 'virtual=True' in before[call] and 'virtual=False' in after[call]
            assert before[constant].endswith(' = 1') and after[constant].endswith(' = 2')
            assert 'M:NormalizationProbe.Sample`1.Added' not in before and 'M:NormalizationProbe.Sample`1.Added' in after
            # Exercise changed/added compiled surface through fragmented union too.
            union(directory / 'ApiProbe', assembly('ApiProbe'), directory, 'mutated-union', expected=1)
            LOG.append('9 real signature probes passed: constraints, interface, sealing, nullability, setter visibility, default, virtual, constant, added method.')
        finally:
            run('dotnet', 'build', probe_project, *FLAGS)
        run('dotnet', CHECKER, 'compare', assembly('ApiProbe'), GOLDENS / 'ApiProbe.golden.txt')


def main():
    LOG.append(f'Host: {platform.platform()}; architecture={platform.machine()}')
    LOG.append('Tested source revision: ' + run('git', 'rev-parse', 'HEAD').strip())
    frozen = sorted(GOLDENS.glob('*')) + sorted((ROOT / 'docs/api').glob('*.txt'))
    frozen_hashes = {path: hashlib.sha256(path.read_bytes()).hexdigest() for path in frozen if path.is_file()}
    expected = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    assert run('dotnet', '--version').strip() == expected
    run('dotnet', '--info')
    run('dotnet', 'build', ROOT / 'src/Xemele/Xemele.csproj', *FLAGS)
    run('dotnet', 'build', ROOT / 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', *FLAGS)
    run('dotnet', 'build', ROOT / 'docs/reports/M0-contracts/Checks/Checks.csproj', *FLAGS)
    run('dotnet', 'build', ROOT / 'docs/reports/M0-contracts/Fixtures/ApiProbe/ApiProbe.csproj', *FLAGS)
    run('dotnet', 'test', '--project', ROOT / 'docs/reports/M0-contracts/Checks/Checks.csproj', '-c', 'Release', '--no-build', '--no-restore')
    run(sys.executable, '-B', '-m', 'unittest', 'discover', '-s', 'tests/Xemele.TestUtilities/Build', '-p', 'test_*.py', '-v')
    for stage in ['M1', 'M2', 'ApiProbe']:
        run('dotnet', CHECKER, 'compare', assembly(stage), GOLDENS / (stage + '.golden.txt'))
    fixture_checks()
    run(sys.executable, '-B', ROOT / 'tools/ApiBaseline/compare.py', '--bootstrap-empty-api',
        '--report', ROOT / 'artifacts/M0-api-wiring/runtime-api.json')
    run(sys.executable, '-B', ROOT / 'tools/planning/tasks.py', 'validate')
    assert all(hashlib.sha256(path.read_bytes()).hexdigest() == digest for path, digest in frozen_hashes.items())
    LOG.append('Approved fixture/golden and existing task fragment bytes unchanged.')
    LOG.append('Focused API/foundation checks passed. No runtime parser, unrestricted ABI, hosted platform or performance claim.')


if __name__ == '__main__':
    try:
        main()
    finally:
        report = ROOT / 'artifacts/M0-api-wiring/checks.txt'
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text('\n'.join(LOG) + '\n', encoding='utf-8')
