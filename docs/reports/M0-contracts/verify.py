#!/usr/bin/env python3
"""Correctness only. Never runs new timings or silently rewrites approved baselines."""
import json
import os
import platform
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
# Respect setup-dotnet, DOTNET_ROOT and the caller's PATH on every host.
ENV = os.environ.copy()
LOG = []

def run(*args, expected=0):
    LOG.append('$ ' + ' '.join(args))
    try:
        result = subprocess.run(args, cwd=ROOT, env=ENV, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=60)
    except subprocess.TimeoutExpired as error:
        output = error.stdout or ""
        LOG.append(output.decode(errors="replace") if isinstance(output, bytes) else output)
        LOG.append("FAILED: command exceeded 60-second timeout; no passing result recorded.")
        raise
    LOG.append(result.stdout)
    LOG.append(f'exit={result.returncode}; expected={expected}')
    if result.returncode != expected:
        raise AssertionError(LOG[-3:])
    return result.stdout

FIXTURE = 'tests/Contracts/Fixtures/foundation'
CHECKER = 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']

def dll(stage):
    name = stage if stage == 'ApiProbe' else 'Foundation' + stage
    return f'docs/reports/M0-contracts/Fixtures/{stage}/bin/Release/net10.0/{name}.dll'

def rows(path):
    lines = Path(path).read_text().splitlines()
    assert lines[0] == 'xemele-api-v1'
    result = {}
    for line in lines[1:]:
        obj = json.loads(line)
        assert set(obj) == {'declaration', 'id', 'member'}
        assert line == json.dumps(obj, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
        assert obj['id'] == obj['member'] and obj['member'] not in result
        result[obj['member']] = obj['declaration']
    assert list(result) == sorted(result)
    return result

def main():
    os.chdir(ROOT)
    LOG.append(f'Host: {platform.platform()}; architecture={platform.machine()}')
    LOG.append('Tested implementation revision: ' + run('git', 'rev-parse', 'HEAD').strip())
    expected_sdk = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    actual_sdk = run('dotnet', '--version').strip()
    assert actual_sdk == expected_sdk, f'SDK mismatch: expected {expected_sdk}, got {actual_sdk}'
    run('dotnet', '--info')
    run('dotnet', 'build', 'docs/reports/M0-contracts/Checks/Checks.csproj', *FLAGS)
    run('dotnet', 'build', 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', *FLAGS)
    run('dotnet', 'test', '--project', 'docs/reports/M0-contracts/Checks/Checks.csproj', '-c', 'Release', '--no-build', '--no-restore')
    for stage in ['M1', 'M2']:
        run('dotnet', CHECKER, 'compare', dll(stage), f'{FIXTURE}/{stage}.golden.txt')
    m1, m2 = rows(f'{FIXTURE}/M1.golden.txt'), rows(f'{FIXTURE}/M2.golden.txt')
    assert all(m2.get(key) == value for key, value in m1.items()), 'M2 changed an M1 declaration'
    assert not any('Resolver' in value or 'DtdProcessing' in value or '.Recover ' in value for value in m1.values())
    LOG.append(f'Additive stage check: {len(m1)} M1 members preserved within {len(m2)} M2 members')
    project = 'docs/reports/M0-contracts/Fixtures/ApiProbe/ApiProbe.csproj'
    run('dotnet', 'build', project, *FLAGS)
    run('dotnet', CHECKER, 'compare', dll('ApiProbe'), f'{FIXTURE}/ApiProbe.golden.txt')
    with tempfile.TemporaryDirectory(prefix='xemele-contract-negative-') as temp:
        original = Path(f'{FIXTURE}/M1.golden.txt').read_text().splitlines()
        for name, edited in [('missing', original[:-1]), ('added', original + [original[-1]]),
                             ('changed', [line.replace('access=Public', 'access=Private') for line in original])]:
            path = str(Path(temp) / (name + '.txt'))
            Path(path).write_text('\n'.join(edited) + '\n')
            run('dotnet', CHECKER, 'compare', dll('M1'), path, expected=1)
        try:
            run('dotnet', 'build', project, *FLAGS, '-p:DefineConstants=MUTATED')
            run('dotnet', CHECKER, 'compare', dll('ApiProbe'), f'{FIXTURE}/ApiProbe.golden.txt', expected=1)
            mutated = str(Path(temp) / 'mutated.txt')
            run('dotnet', CHECKER, 'emit', dll('ApiProbe'), mutated)
            before, after = rows(f'{FIXTURE}/ApiProbe.golden.txt'), rows(mutated)
            for member in ['T:NormalizationProbe.Sample`1', 'F:NormalizationProbe.Sample`1.Constant',
                           'P:NormalizationProbe.Sample`1.Text', 'M:NormalizationProbe.Sample`1.Call(System.Int32)']:
                assert before[member] != after[member], member
            assert 'M:NormalizationProbe.Sample`1.Added' in after and 'M:NormalizationProbe.Sample`1.Added' not in before
            # Independently ensure each semantic fact is actually captured by normalization.
            assert 'where T : class' in before['T:NormalizationProbe.Sample`1']
            assert 'where T : struct' in after['T:NormalizationProbe.Sample`1']
            assert 'interface NormalizationProbe.IMarker' in before['T:NormalizationProbe.Sample`1']
            assert 'interface NormalizationProbe.IMarker' not in after['T:NormalizationProbe.Sample`1']
            assert 'sealed=True' in after['T:NormalizationProbe.Sample`1']
            assert 'System.String?' in before['P:NormalizationProbe.Sample`1.Text']
            assert 'set=Protected' in before['P:NormalizationProbe.Sample`1.Text']
            assert 'set=Public' in after['P:NormalizationProbe.Sample`1.Text']
            assert 'value = 1' in before['M:NormalizationProbe.Sample`1.Call(System.Int32)']
            assert 'virtual=True' in before['M:NormalizationProbe.Sample`1.Call(System.Int32)']
            assert 'virtual=False' in after['M:NormalizationProbe.Sample`1.Call(System.Int32)']
            assert before['F:NormalizationProbe.Sample`1.Constant'].endswith(' = 1')
            assert after['F:NormalizationProbe.Sample`1.Constant'].endswith(' = 2')
            LOG.append('Compiled mutation probes passed: constraints/interfaces/sealing/nullable/accessor visibility/optional default/virtual/constant/added method')
        finally:
            run('dotnet', 'build', project, *FLAGS)
        run('dotnet', CHECKER, 'compare', dll('ApiProbe'), f'{FIXTURE}/ApiProbe.golden.txt')
    run('dotnet', 'build', 'Xemele.slnx', *FLAGS)
    run('dotnet', 'test', '--solution', 'Xemele.slnx', '-c', 'Release', '--no-build', '--no-restore')
    run('python3', '-B', '-m', 'unittest', 'discover', '-s', 'tests/Xemele.TestUtilities/Build', '-p', 'test_*.py', '-v')
    run('python3', 'tools/ApiBaseline/discover.py', '--bootstrap-empty-api')
    run('dotnet', 'build', 'experiments/Tree/Tree.csproj', *FLAGS)
    run('dotnet', 'build', 'experiments/Input/Input.csproj', *FLAGS)
    run('dotnet', 'run', '--project', 'experiments/Tree/Tree.csproj', '-c', 'Release', '--no-build', '--', '--self-check')
    run('dotnet', 'run', '--project', 'experiments/Input/Input.csproj', '-c', 'Release', '--no-build', '--', 'check')
    run('python3', 'tools/planning/tasks.py', 'validate')
    assert not list((ROOT / 'src/Xemele').glob('*.cs')), 'Runtime must remain empty'
    LOG.append('All correctness checks passed; no new timing measured.')

try:
    main()
finally:
    output = ROOT / 'artifacts/M0-contracts/checks.txt'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text('\n'.join(LOG) + '\n')
    print('\n'.join(LOG))
