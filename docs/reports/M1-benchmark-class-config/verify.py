"""Build the six harnesses and compare real BDN metadata; never run timing jobs."""
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
REPORT = Path(__file__).resolve().parent
HARNESS = [
    ('tree', 'experiments/Tree/Tree.csproj', 'Tree'),
    ('input', 'experiments/Input/Input.csproj', 'Input'),
    ('main', 'benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj', 'Xemele.Benchmarks'),
    ('common', 'docs/reports/M1-common/Xemele.CommonBenchmarks.csproj', 'Xemele.CommonBenchmarks'),
    ('input-runtime', 'docs/reports/M1-input/Xemele.InputBenchmarks.csproj', 'Xemele.InputBenchmarks'),
    ('serializer', 'docs/reports/M1-serialize/SerializerBenchmarks.csproj', 'SerializerBenchmarks'),
]
CLASSES = [
    'experiments/Tree/Benchmarks.cs', 'experiments/Input/Benchmarks.cs',
    'benchmarks/Xemele.Benchmarks/M1-common/CommonBenchmarks.cs',
    'benchmarks/Xemele.Benchmarks/M1-input/InputBenchmarks.cs',
    'benchmarks/Xemele.Benchmarks/M1-serialize/SerializerBenchmarks.cs',
]
ENV = dict(os.environ, DOTNET_ROOT='/Users/sebastienros/Library/Application Support/dotnetup/dotnet', DOTNET_CLI_USE_MSBUILD_SERVER='0')
ENV['PATH'] = ENV['DOTNET_ROOT'] + os.pathsep + ENV['PATH']
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']

def run(args):
    print('$ ' + ' '.join(map(str, args)), flush=True)
    subprocess.run(args, cwd=ROOT, env=ENV, check=True)

mode = sys.argv[1] if len(sys.argv) > 1 else 'current'
if mode not in ('baseline', 'current'):
    raise SystemExit('baseline [full revision] | current')
run(['dotnet', 'build', str(REPORT / 'ConfigCheck.csproj'), *FLAGS])
checker = REPORT / 'bin/Release/net10.0/ConfigCheck.dll'
saved = {}
try:
    if mode == 'baseline':
        revision = sys.argv[2]
        # Only these owned class files are substituted, and always restored.
        for relative in CLASSES:
            path = ROOT / relative
            saved[path] = path.read_bytes()
            path.write_bytes(subprocess.check_output(['git', 'show', revision + ':' + relative], cwd=ROOT))
    for label, project, name in HARNESS:
        run(['dotnet', 'build', project, *FLAGS])
        assembly = ROOT / Path(project).parent / 'bin/Release/net10.0' / (name + '.dll')
        output = REPORT / (label + '-' + mode + '.json')
        command = ['dotnet', str(checker), str(assembly), mode, str(output)]
        if mode == 'current':
            command.append(str(REPORT / (label + '-baseline.json')))
        run(command)
finally:
    for path, content in saved.items():
        path.write_bytes(content)
print('PASS: all six harnesses; compiled configuration and workload inventory match.' if mode == 'current' else 'Captured six original compiled workload/configuration inventories.')
