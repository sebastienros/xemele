#!/usr/bin/env python3
"""Full fixed gate source/config/generated/binary/SDK-runtime-host identity."""
import hashlib,json,os,pathlib,subprocess,sys,platform
root=pathlib.Path(__file__).resolve().parents[3]
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def capture():
 tracked=subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0')
 sources=[root/p for p in tracked if p and (root/p).is_file()]
 sources.extend(p for p in root.rglob('*') if p.is_file() and 'obj' in p.relative_to(root).parts and '.git' not in p.parts)
 # Original official inventory is ignored, but belongs to the frozen fixture closure.
 sources.extend(p for p in (root/'tests/corpora/xml/xmlconf').rglob('*') if p.is_file())
 binaries=[p for p in root.rglob('*') if p.is_file() and 'bin' in p.relative_to(root).parts and '.git' not in p.parts]
 runtime=pathlib.Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
 runtime_files=[runtime/'dotnet']+[p for folder in ['sdk/10.0.400','shared/Microsoft.NETCore.App/10.0.11','host/fxr'] for p in (runtime/folder).rglob('*') if p.is_file()]
 plan=json.loads((root/'docs/reports/M6-adapters/measurement-plan.json').read_text())
 effective=dict(os.environ);effective.update(plan['environment'])
 environment_sha256=hashlib.sha256(json.dumps(effective,sort_keys=True,separators=(',',':')).encode()).hexdigest()
 env={k:v for k,v in effective.items() if k in {'PATH','LANG','LC_ALL','LC_CTYPE','MallocNanoZone','LD_LIBRARY_PATH'} or k.startswith(('DOTNET_','COMPlus_','CORECLR_','DYLD_','XEMELE_'))};env.update(plan['environment'])
 host={'platform':platform.platform(),'machine':platform.machine(),'cpuCount':os.cpu_count(),'cpu':subprocess.check_output(['sysctl','-n','machdep.cpu.brand_string'],text=True).strip(),'memoryBytes':subprocess.check_output(['sysctl','-n','hw.memsize'],text=True).strip()}
 return {'host':host,'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'environment':env,'completeEffectiveChildEnvironmentSha256':environment_sha256,**{name:{str(p):digest(p) for p in sorted(set(paths))} for name,paths in [('source',sources),('binary',binaries),('runtime',runtime_files)]}}
mode,path=sys.argv[1:];path=pathlib.Path(path);actual=capture()
if mode=='capture':path.write_text(json.dumps(actual,indent=2,sort_keys=True)+'\n')
elif mode=='check':assert json.loads(path.read_text())==actual,'Prepared closure changed'
else:raise ValueError('capture|check manifest')
print(mode, {k:len(actual[k]) for k in ['source','binary','runtime']},'manifestSha256='+digest(path))
