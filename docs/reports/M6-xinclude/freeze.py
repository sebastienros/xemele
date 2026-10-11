#!/usr/bin/env python3
"""Complete prepared closure; read-only identity capture/check, never generates."""
import os, hashlib, json, pathlib, subprocess, sys
root=pathlib.Path(__file__).resolve().parents[3]
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def files(path):return [p for p in path.rglob('*') if p.is_file()]
def capture():
    inputs=[]
    for folder in ['src/Xemele','samples/M6-xinclude','benchmarks/Xemele.Benchmarks/M6-xinclude','tests/Xemele.Tests/M6-xinclude','tests/fixtures/M6-xinclude','docs/reports/M6-xinclude']:
        inputs.extend(p for p in files(root/folder) if 'bin' not in p.parts and 'obj' not in p.parts)
    inputs.extend(root/name for name in ['global.json','Directory.Build.props','Directory.Packages.props','NuGet.Config'] if (root/name).exists())
    for folder in ['src/Xemele','samples/M6-xinclude','benchmarks/Xemele.Benchmarks/M6-xinclude']:
        inputs.extend(files(root/folder/'obj'))
    binaries=[]
    for folder in ['src/Xemele','samples/M6-xinclude','benchmarks/Xemele.Benchmarks/M6-xinclude']:binaries.extend(files(root/folder/'bin/Release/net10.0'))
    runtime=pathlib.Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
    runtime_files=[runtime/'dotnet']+files(runtime/'sdk/10.0.400')+files(runtime/'shared/Microsoft.NETCore.App/10.0.11')+files(runtime/'host/fxr')
    plan=json.loads((root/'docs/reports/M6-xinclude/measurement-plan.json').read_text())
    environment={key:value for key,value in os.environ.items() if key in {'PATH','LANG','LC_ALL','LC_CTYPE','MallocNanoZone','LD_LIBRARY_PATH'} or key.startswith(('DOTNET_','COMPlus_','CORECLR_','DYLD_'))}
    environment.update(plan['environment'])
    complete_environment=dict(os.environ);complete_environment.update(plan['environment'])
    environment_hash=hashlib.sha256(json.dumps(complete_environment,sort_keys=True,separators=(',',':')).encode()).hexdigest()
    return {'effectiveEnvironmentSha256':environment_hash,'environment':environment,'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'source':{str(p):digest(p) for p in sorted(set(inputs))},'binary':{str(p):digest(p) for p in sorted(set(binaries))},'runtime':{str(p):digest(p) for p in runtime_files}}
mode,path=sys.argv[1:];path=pathlib.Path(path);actual=capture()
if mode=='capture':path.write_text(json.dumps(actual,indent=2,sort_keys=True)+'\n')
elif mode=='check':assert json.loads(path.read_text())==actual,'Prepared closure changed'
else:raise ValueError('capture|check <manifest>')
print(f'{mode}: source={len(actual["source"])}, binary={len(actual["binary"])}, runtime={len(actual["runtime"])}; manifestSha256={digest(path)}')
