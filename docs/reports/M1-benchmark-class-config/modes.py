"""Exercise launcher modes and CLI discovery without timing workloads."""
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
ENV = dict(os.environ, DOTNET_ROOT='/Users/sebastienros/Library/Application Support/dotnetup/dotnet', DOTNET_CLI_USE_MSBUILD_SERVER='0')
ENV['PATH'] = ENV['DOTNET_ROOT'] + os.pathsep + ENV['PATH']
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']

def run(args, cwd=ROOT):
    print('$ ' + ' '.join(map(str, args)), flush=True)
    subprocess.run(args, cwd=cwd, env=ENV, check=True)

def dll(folder, name):
    return str(ROOT / folder / 'bin/Release/net10.0' / (name + '.dll'))

def invoke(folder, name, args, cwd=ROOT):
    run(['dotnet', dll(folder, name), *args], cwd)

for folder, name, prefix in [
    ('experiments/Tree', 'Tree', []), ('experiments/Input', 'Input', ['benchmark']),
    ('benchmarks/Xemele.Benchmarks', 'Xemele.Benchmarks', []),
    ('docs/reports/M1-common', 'Xemele.CommonBenchmarks', []),
    ('docs/reports/M1-input', 'Xemele.InputBenchmarks', []),
    ('docs/reports/M1-serialize', 'SerializerBenchmarks', []),
]:
    invoke(folder, name, [*prefix, '--list', 'flat'])
invoke('benchmarks/Xemele.Benchmarks', 'Xemele.Benchmarks', ['--help'])
for args in [['--self-check'], ['--memory']]:
    invoke('experiments/Tree', 'Tree', args)
for command in ['help', 'check', 'allocations', 'lowering']:
    invoke('experiments/Input', 'Input', [command])
invoke('docs/reports/M1-common', 'Xemele.CommonBenchmarks', ['--setup-allocation'])
invoke('docs/reports/M1-input', 'Xemele.InputBenchmarks', ['--allocation'])
invoke('docs/reports/M1-serialize', 'SerializerBenchmarks', ['--allocation'])
run(['dotnet', 'build', 'docs/reports/M1-contracts/Projects/Input/Input.csproj', *FLAGS])
# The historical driver writes a relative report. Redirect its working directory
# to a disposable directory so prior evidence stays untouched.
with tempfile.TemporaryDirectory(prefix='xemele-input-contract-') as directory:
    destination = Path(directory) / 'docs/reports/M1-input'
    destination.mkdir(parents=True)
    model = dll('docs/reports/M1-contracts/Projects/Input', 'M1Input')
    invoke('docs/reports/M1-input', 'Xemele.InputBenchmarks', ['--contract', model], Path(directory))
    actual = (destination / 'internal-contract.txt').read_bytes()
    approved = (ROOT / 'docs/reports/M1-input/internal-contract.txt').read_bytes()
    if actual != approved:
        raise Exception('Historical internal contract report changed')
print('PASS: launcher discovery/help/check/allocation/lowering/contract modes; historical contract evidence unchanged.')
