#!/usr/bin/env python3
"""Read-only identity capture/check for the prepared, non-generating probe."""
import os
import hashlib
import json
import pathlib
import subprocess
import sys
root=pathlib.Path(__file__).resolve().parents[3]
probe=root/'benchmarks/Xemele.Benchmarks/M7-html-model/bin/Release/net10.0'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def capture():
    sources=[]
    for path in (root/'src/Xemele').rglob('*.cs'):
        if 'bin' not in path.parts and 'obj' not in path.parts:sources.append(path)
    sources.extend(root/path for path in ['global.json','.editorconfig','Directory.Build.props','Directory.Packages.props','src/Xemele/Xemele.csproj','benchmarks/Xemele.Benchmarks/M7-html-model/Model.Probe.csproj','benchmarks/Xemele.Benchmarks/M7-html-model/Probe.cs.fixture','docs/reports/M7-html-model/freeze.py','docs/reports/M7-html-model/run_measurements.py','docs/reports/M7-html-model/measurement-plan.json','docs/reports/M7-html-model/prepare_identity.py','benchmarks/Xemele.Benchmarks/M7-html-model/ModelBenchmarks.cs'])
    sources.extend(root/path for path in ['docs/tasks/M7-html-model.json','docs/specs/html.md','docs/specs/common.md','docs/performance-coding.md','tests/Contracts/Fixtures/M7/Model.cs.fixture','tests/Contracts/Fixtures/M7/Engine.cs.fixture','tests/Contracts/Fixtures/M7/Tokenizer.cs.fixture'])
    sources.extend(p for p in [root/'NuGet.config',root/'NuGet.Config'] if p.is_file())
    for project in ['src/Xemele','benchmarks/Xemele.Benchmarks/M7-html-model']:
        generated=root/project/'obj/Release/net10.0'
        sources.extend(generated.glob('*.cs'))
        sources.extend(generated.glob('.*.cs'))
        sources.extend(p for p in generated.rglob('*') if p.is_file())
        sources.extend((root/project/'obj').glob('*.json'))
        sources.extend((root/project/'obj').glob('*.props'))
        sources.extend((root/project/'obj').glob('*.targets'))
    for directory in ['tests/Xemele.Tests/M7-html-model','tests/fixtures/M7-html-model','samples/M7-html-model','docs/reports/M7-html-model/ApiCheck','tests/corpora/xml']:
        sources.extend(p for p in (root/directory).rglob('*') if p.is_file() and 'bin' not in p.parts)
    sources.append(root/'docs/api/M7-html-model.txt')
    for project in ['src/Xemele','benchmarks/Xemele.Benchmarks/M7-html-model']:
        sources.extend(p for p in (root/project/'obj').rglob('*') if p.is_file())
    binary_files=[p for p in probe.rglob('*') if p.is_file()] + [p for p in (root/'src/Xemele/bin/Release/net10.0').rglob('*') if p.is_file()]
    binaries={str(p):digest(p) for p in sorted(set(binary_files))}
    runtime=pathlib.Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
    runtime_files=[runtime/'dotnet'] + [p for p in (runtime/'sdk/10.0.400').rglob('*') if p.is_file()] + [p for p in (runtime/'shared/Microsoft.NETCore.App/10.0.11').rglob('*') if p.is_file()] + [p for p in (runtime/'host/fxr').rglob('*') if p.is_file()]
    plan=json.loads((root/'docs/reports/M7-html-model/measurement-plan.json').read_text())
    environment={key:value for key,value in os.environ.items() if key in {'PATH','LANG','LC_ALL','LC_CTYPE','MallocNanoZone','LD_LIBRARY_PATH','SHLVL'} or key.startswith(('DOTNET_','COMPlus_','CORECLR_','DYLD_'))}
    environment.update(plan['environment'])
    effective_environment=dict(os.environ)
    effective_environment.update(plan['environment'])
    # Commit only an opaque digest of the complete child environment; secret values never print.
    effective_environment_sha256=hashlib.sha256(json.dumps(effective_environment,sort_keys=True,separators=(',',':')).encode()).hexdigest()
    return {'effective_environment_key_sha256':{key:hashlib.sha256(value.encode()).hexdigest() for key,value in sorted(effective_environment.items())},'effective_environment_sha256':effective_environment_sha256,'environment':environment,'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'host':{'uname':subprocess.check_output(['uname','-a'],text=True).strip(),'hardware':subprocess.check_output(['sysctl','hw.model','hw.ncpu','hw.memsize'],text=True).strip()},'source':{str(p):digest(p) for p in sorted(set(sources))},'binary':binaries,'runtime':{str(p):digest(p) for p in runtime_files}}
mode,path=sys.argv[1:];path=pathlib.Path(path);actual=capture()
if mode=='capture':path.write_text(json.dumps(actual,indent=2,sort_keys=True)+'\n')
elif mode=='check':assert json.loads(path.read_text())==actual,'Prepared source/binary/runtime identity changed'
else:raise ValueError('capture|check <manifest>')
print(f'{mode}: source={len(actual["source"])}, binary={len(actual["binary"])}, runtime={len(actual["runtime"])}; manifestSha256={digest(path)}')
