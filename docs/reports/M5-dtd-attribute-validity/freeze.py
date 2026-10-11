#!/usr/bin/env python3
"""Read-only identity capture/check for the prepared, non-generating probe."""
import os
import hashlib
import json
import pathlib
import subprocess
import sys
root=pathlib.Path(__file__).resolve().parents[3]
probe=root/'benchmarks/Xemele.Benchmarks/M5-dtd-attribute-validity/bin/Release/net10.0'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def capture():
    sources=[]
    for path in (root/'src/Xemele').rglob('*.cs'):
        if 'bin' not in path.parts and 'obj' not in path.parts:sources.append(path)
    sources.extend(root/path for path in ['global.json','Directory.Build.props','Directory.Packages.props','src/Xemele/Xemele.csproj','benchmarks/Xemele.Benchmarks/M5-dtd-attribute-validity/AttributeValidity.Probe.csproj','benchmarks/Xemele.Benchmarks/M5-dtd-attribute-validity/Probe.cs.fixture','docs/reports/M5-dtd-attribute-validity/freeze.py','docs/reports/M5-dtd-attribute-validity/run_measurements.py','docs/reports/M5-dtd-attribute-validity/measurement-plan.json'])
    for project in ['src/Xemele','benchmarks/Xemele.Benchmarks/M5-dtd-attribute-validity']:
        generated=root/project/'obj/Release/net10.0'
        sources.extend(generated.glob('*.cs'))
        sources.extend(generated.glob('.*.cs'))
        sources.extend(p for p in generated.rglob('*') if p.is_file())
        sources.extend((root/project/'obj').glob('*.json'))
        sources.extend((root/project/'obj').glob('*.props'))
        sources.extend((root/project/'obj').glob('*.targets'))
    binary_files=[p for p in probe.rglob('*') if p.is_file()] + [p for p in (root/'src/Xemele/bin/Release/net10.0').rglob('*') if p.is_file()]
    binaries={str(p):digest(p) for p in sorted(set(binary_files))}
    runtime=pathlib.Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
    runtime_files=[runtime/'dotnet'] + [p for p in (runtime/'sdk/10.0.400').rglob('*') if p.is_file()] + [p for p in (runtime/'shared/Microsoft.NETCore.App/10.0.11').rglob('*') if p.is_file()] + [p for p in (runtime/'host/fxr').rglob('*') if p.is_file()]
    plan=json.loads((root/'docs/reports/M5-dtd-attribute-validity/measurement-plan.json').read_text())
    environment={key:value for key,value in os.environ.items() if key in {'PATH','LANG','LC_ALL','LC_CTYPE','MallocNanoZone','LD_LIBRARY_PATH'} or key.startswith(('DOTNET_','COMPlus_','CORECLR_','DYLD_'))}
    environment.update(plan['environment'])
    return {'environment':environment,'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'source':{str(p):digest(p) for p in sorted(set(sources))},'binary':binaries,'runtime':{str(p):digest(p) for p in runtime_files}}
mode,path=sys.argv[1:];path=pathlib.Path(path);actual=capture()
if mode=='capture':path.write_text(json.dumps(actual,indent=2,sort_keys=True)+'\n')
elif mode=='check':assert json.loads(path.read_text())==actual,'Prepared source/binary/runtime identity changed'
else:raise ValueError('capture|check <manifest>')
print(f'{mode}: source={len(actual["source"])}, binary={len(actual["binary"])}, runtime={len(actual["runtime"])}; manifestSha256={digest(path)}')
