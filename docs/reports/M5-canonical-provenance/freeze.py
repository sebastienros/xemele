#!/usr/bin/env python3
"""Read-only identity capture/check for the prepared, non-generating probe."""
import hashlib
import json
import pathlib
import os
import platform
import subprocess
import sys
root=pathlib.Path(__file__).resolve().parents[3]
probe=root/'docs/reports/M5-canonical-provenance/bin/Release/net10.0'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def capture():
    sources=[]
    for path in (root/'src/Xemele').rglob('*.cs'):
        if 'bin' not in path.parts and 'obj' not in path.parts:sources.append(path)
    sources.extend(root/path for path in ['global.json','Directory.Build.props','Directory.Packages.props','src/Xemele/Xemele.csproj','docs/reports/M5-canonical-provenance/Provenance.Probe.csproj','benchmarks/Xemele.Benchmarks/M5-canonical-provenance/Probe.cs.fixture','docs/reports/M5-canonical-provenance/freeze.py','docs/reports/M5-canonical-provenance/run_measurements.py','docs/reports/M5-canonical-provenance/measurement-plan.json'])
    for project in ['src/Xemele','docs/reports/M5-canonical-provenance']:
        generated=root/project/'obj/Release/net10.0'
        sources.extend(generated.glob('*.cs'))
        sources.extend(generated.glob('.*.cs'))
        sources.extend(p for p in (root/project/'obj').rglob('*') if p.is_file() and 'ref' not in p.parts and 'refint' not in p.parts)
    binaries={str(p):digest(p) for directory in [probe,root/'src/Xemele/bin/Release/net10.0'] for p in sorted(directory.rglob('*')) if p.is_file()}
    runtime=pathlib.Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
    runtime_files=[runtime/'dotnet'] + [p for p in (runtime/'sdk/10.0.400').rglob('*') if p.is_file()] + [p for p in (runtime/'shared/Microsoft.NETCore.App/10.0.11').rglob('*') if p.is_file()] + [p for p in (runtime/'host/fxr').rglob('*') if p.is_file()]
    host={'platform':platform.platform(),'machine':platform.machine(),'cpuCount':os.cpu_count(),'cpu':subprocess.check_output(['sysctl','-n','machdep.cpu.brand_string'],text=True).strip(),'memoryBytes':subprocess.check_output(['sysctl','-n','hw.memsize'],text=True).strip()}
    # Shell bookkeeping and connector/session variables may change between tool
    # invocations. Freeze only effective runtime-relevant inherited settings;
    # exact planned launch overrides are part of the frozen source/plan closure.
    plan=json.loads((root/'docs/reports/M5-canonical-provenance/measurement-plan.json').read_text())
    environment={key:value for key,value in os.environ.items() if key in {'PATH','LANG','LC_ALL','LC_CTYPE','MallocNanoZone','LD_LIBRARY_PATH'} or key.startswith(('DOTNET_','COMPlus_','CORECLR_','DYLD_'))}
    environment.update(plan['environment'])
    environment_sha256=hashlib.sha256(json.dumps(environment,sort_keys=True).encode()).hexdigest()
    return {'host':host,'effectiveRuntimeEnvironmentSha256':environment_sha256,'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'source':{str(p):digest(p) for p in sorted(set(sources))},'binary':binaries,'runtime':{str(p):digest(p) for p in runtime_files}}
mode,path=sys.argv[1:];path=pathlib.Path(path);actual=capture()
if mode=='capture':path.write_text(json.dumps(actual,indent=2,sort_keys=True)+'\n')
elif mode=='check':assert json.loads(path.read_text())==actual,'Prepared source/binary/runtime identity changed'
else:raise ValueError('capture|check <manifest>')
print(f'{mode}: source={len(actual["source"])}, binary={len(actual["binary"])}, runtime={len(actual["runtime"])}; manifestSha256={digest(path)}')
