#!/usr/bin/env python3
"""Read-only identity capture/check for the prepared, non-generating probe."""
import hashlib
import json
import pathlib
import subprocess
import sys
root=pathlib.Path(__file__).resolve().parents[3]
probe=root/'docs/reports/M4-functions/bin/Release/net10.0'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def capture():
    sources=[]
    for path in (root/'src/Xemele').rglob('*.cs'):
        if 'bin' not in path.parts and 'obj' not in path.parts:sources.append(path)
    sources.extend(root/path for path in ['global.json','Directory.Build.props','Directory.Packages.props','src/Xemele/Xemele.csproj','docs/reports/M4-functions/Functions.Probe.csproj','benchmarks/Xemele.Benchmarks/M4-functions/Probe.cs.fixture','docs/reports/M4-functions/freeze.py','docs/reports/M4-functions/run_measurements.py'])
    for project in ['src/Xemele','docs/reports/M4-functions']:
        generated=root/project/'obj/Release/net10.0'
        sources.extend(generated.glob('*.cs'))
        sources.extend(generated.glob('.*.cs'))
    binaries={str(p):digest(p) for p in sorted(probe.rglob('*')) if p.is_file()}
    runtime=pathlib.Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
    runtime_files=[runtime/'dotnet',runtime/'sdk/10.0.400/dotnet.dll'] + [p for p in (runtime/'shared/Microsoft.NETCore.App/10.0.11').rglob('*') if p.is_file()] + [p for p in (runtime/'host/fxr').rglob('*') if p.is_file()]
    return {'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'source':{str(p):digest(p) for p in sorted(set(sources))},'binary':binaries,'runtime':{str(p):digest(p) for p in runtime_files}}
mode,path=sys.argv[1:];path=pathlib.Path(path);actual=capture()
if mode=='capture':path.write_text(json.dumps(actual,indent=2,sort_keys=True)+'\n')
elif mode=='check':assert json.loads(path.read_text())==actual,'Prepared source/binary/runtime identity changed'
else:raise ValueError('capture|check <manifest>')
print(f'{mode}: source={len(actual["source"])}, binary={len(actual["binary"])}, runtime={len(actual["runtime"])}; manifestSha256={digest(path)}')
