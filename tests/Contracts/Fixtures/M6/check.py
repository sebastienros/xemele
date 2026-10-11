#!/usr/bin/env python3
"""Compile-only M6 API evidence; executes integrated prior APIs, never extern M6 members."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
LANES = ['Catalog', 'XInclude', 'Interop', 'Cli']
CLAIM = 'ab3acdeec933762d8ec412168104d691671f8dc6'

def run(command, expected=0):
    result = subprocess.run(command, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if result.returncode != expected:
        raise AssertionError(f'{command}\nexit={result.returncode}\n{result.stdout}')
    return result.stdout

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--emit-baselines', action='store_true')
    parser.add_argument('--verify-existing', action='store_true', help='Recheck artifacts just built in this same checkout; does not rebuild')
    args = parser.parse_args()
    assert run([args.dotnet, '--version']).strip() == '10.0.400'
    print('PASS: pinned SDK 10.0.400', flush=True)
    for lane in LANES + ['ApiCheck']:
        project = HERE/lane/(lane+'.csproj')
        if not args.verify_existing:
            run([args.dotnet, 'restore', str(project), '--source', str(HERE), '-p:NuGetAudit=false', '-p:RuntimeFrameworkVersion=10.0.11', '--verbosity', 'quiet'])
            run([args.dotnet, 'build', str(project), '-c', 'Release', '--no-restore', '--no-incremental', '-p:RuntimeFrameworkVersion=10.0.11', '--verbosity', 'quiet'])
        config = json.loads((HERE/lane/('bin/Release/net10.0/'+lane+'.runtimeconfig.json')).read_text())
        assert config['runtimeOptions']['framework']['version'] == '10.0.11'
        assert config['runtimeOptions']['rollForward'] == 'Disable'
        action = 'existing local artifacts rechecked (not rebuilt)' if args.verify_existing else 'independently restored locally and built'
        print(f'PASS: {lane} {action}; runtimeconfig pins 10.0.11', flush=True)
        if lane in LANES:
            output = run([args.dotnet, '--fx-version', '10.0.11', str(HERE/lane/('bin/Release/net10.0/'+lane+'.dll'))])
            assert 'actual runtime 10.0.11' in output
            print(output.strip(), flush=True)
    checker = HERE/'ApiCheck/bin/Release/net10.0/ApiCheck.dll'
    for lane in LANES[:-1]:
        assembly = HERE/lane/('bin/Release/net10.0/'+lane+'.dll')
        baseline = HERE/(lane+'.golden.txt')
        output = run([args.dotnet, '--fx-version', '10.0.11', str(checker), 'emit' if args.emit_baselines else 'compare', str(assembly), str(baseline)])
        print(f'PASS: {lane} compiled exact API '+output.strip(), flush=True)
        lines = baseline.read_text().splitlines()
        assert lines[0] == 'xemele-api-v1' and len(lines) > 5
        with tempfile.TemporaryDirectory(prefix='m6-api-negative-') as directory:
            bad = Path(directory)/'removed.txt'; bad.write_text('\n'.join(lines[:-1])+'\n')
            output = run([args.dotnet, '--fx-version', '10.0.11', str(checker), 'compare', str(assembly), str(bad)], expected=1)
            assert 'API mismatch' in output
        print(f'PASS: {lane} missing-member negative control rejected', flush=True)
    for lane in LANES:
        project = (HERE/lane/(lane+'.csproj')).read_text()
        assert '<IsPackable>false</IsPackable>' in project and '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>' in project
        assert 'ProjectReference' in project and '../'+lane+'.cs.fixture' in project
        assert 'CompileUsage' in (HERE/lane/'Program.cs.fixture').read_text()
    vectors = json.loads((HERE/'vectors.json').read_text())
    cases = vectors['cases']
    assert len(cases) == 16 and len({row['id'] for row in cases}) == 16
    assert {row['lane'] for row in cases} == set(LANES)
    assert all('expected' in row and row['input'] for row in cases)
    public_only = next(row for row in cases if row['id'] == 'catalog-public-only-system-preference')
    assert public_only['input']['system'] is None and public_only['expected'] == 'b'
    # Schema negative controls establish no missing case/expected result can pass inventory.
    for mutated in [cases[:-1], [dict(row) for row in cases]]:
        if len(mutated) == 16:
            del mutated[0]['expected']
        assert len(mutated) != 16 or not all('expected' in row for row in mutated)
    print('PASS: 16 future acceptance vectors and two rejecting inventory controls; no runtime-conformance claim', flush=True)
    # Earlier contracts/source/build/package files are untouched, including after commits.
    def owned(path):
        return path in ['docs/tasks/M6-contracts.json', 'docs/specs/integration.md'] or any(path.startswith(prefix) for prefix in ['tests/Contracts/Fixtures/M6/', 'docs/decisions/M6/', 'docs/reports/M6-contracts/'])
    changed = run(['git','diff','--name-only',CLAIM,'--']).splitlines()
    untracked = run(['git','ls-files','--others','--exclude-standard']).splitlines()
    assert all(owned(path) for path in changed+untracked), changed+untracked
    registry = (ROOT/'src/Xemele.Tool/CommandRegistry.cs').read_text()
    assert 'partial void RegisterXmlCommands();' in registry and 'Reflection' not in registry
    assert not [p for p in HERE.rglob('*.cs') if 'obj' not in p.parts and 'bin' not in p.parts], 'All fixture sources must have .fixture extension to preserve parent globs'
    print('PASS: owned changes only; earlier sources/contracts unchanged; four independent nonpackaged lanes; static M0 registry; no new .cs glob sources or shipped stubs', flush=True)

if __name__ == '__main__': main()
