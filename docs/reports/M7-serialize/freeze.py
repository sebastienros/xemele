#!/usr/bin/env python3
"""Read-only complete prepared identity; reusable predecessor wrapper semantics."""
import os,hashlib,json,pathlib,subprocess,sys
root=pathlib.Path(__file__).resolve().parents[3]
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def capture():
 sources=[]
 for path in (root/'src/Xemele').rglob('*.cs'):
  if 'bin' not in path.parts and 'obj' not in path.parts:sources.append(path)
 for directory in ['docs/reports/M7-serialize','benchmarks/Xemele.Benchmarks/M7-serialize','tests/fixtures/M7-serialize','tests/Xemele.Tests/M7-serialize','samples/M7-serialize','tests/corpora/xml','tests/fixtures/M7-legacy','tests/Xemele.Tests/M7-legacy','tests/fixtures/M7-builder','tests/Xemele.Tests/M7-builder','tests/Xemele.Tests/M7-tokenizer','tests/Xemele.Tests/M7-html-model','tests/Xemele.Tests/M7-html-pi-model','tests/Xemele.Tests/M7-html-parsed-text-model','tests/Xemele.Tests/M1-serialize']:
  sources.extend(p for p in (root/directory).rglob('*') if p.is_file() and 'bin' not in p.parts and '__pycache__' not in p.parts)
 sources.extend(root/p for p in ['global.json','.editorconfig','Directory.Build.props','Directory.Packages.props','src/Xemele/Xemele.csproj','tests/Xemele.Tests/Xemele.Tests.csproj','docs/tasks/M7-serialize.json','docs/specs/html.md','docs/specs/common.md','docs/performance-coding.md','docs/decisions/M7/html-processing-instructions.md','docs/decisions/M7/legacy-root-recovery.md','tests/Contracts/Fixtures/M7/Engine.cs.fixture','tests/Contracts/Fixtures/M7/Engine.golden.txt','docs/api/M7-serialize.txt','artifacts/M7-serialize/inputs.json'])
 sources.append(pathlib.Path('/private/tmp/xemele-html-parsing.html'))
 sources.extend(p for p in [root/'NuGet.config',root/'NuGet.Config'] if p.is_file())
 projects=['src/Xemele','tests/Xemele.Tests','tests/Xemele.TestUtilities','samples/M7-serialize','benchmarks/Xemele.Benchmarks/M7-serialize','docs/reports/M7-serialize/ApiCheck']
 for project in projects:sources.extend(p for p in (root/project/'obj').rglob('*') if p.is_file())
 binary_files=[p for project in projects for p in (root/project/'bin/Release/net10.0').rglob('*') if p.is_file()]
 runtime=pathlib.Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
 runtime_files=[runtime/'dotnet']+[p for part in ['sdk/10.0.400','shared/Microsoft.NETCore.App/10.0.11','host/fxr'] for p in (runtime/part).rglob('*') if p.is_file()]
 plan=json.loads((root/'docs/reports/M7-serialize/measurement-plan.json').read_text());effective=dict(os.environ);effective.update(plan['environment'])
 relevant={key:value for key,value in effective.items() if key in {'PATH','LANG','LC_ALL','LC_CTYPE','MallocNanoZone','LD_LIBRARY_PATH','SHLVL'} or key.startswith(('DOTNET_','COMPlus_','CORECLR_','DYLD_'))}
 return {'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'host':{'uname':subprocess.check_output(['uname','-a'],text=True).strip(),'hardware':subprocess.check_output(['sysctl','hw.model','hw.ncpu','hw.memsize'],text=True).strip()},'environment':relevant,'effective_environment_sha256':hashlib.sha256(json.dumps(effective,sort_keys=True,separators=(',',':')).encode()).hexdigest(),'effective_environment_key_sha256':{key:hashlib.sha256(value.encode()).hexdigest() for key,value in sorted(effective.items())},'source':{str(p):digest(p) for p in sorted(set(sources))},'binary':{str(p):digest(p) for p in sorted(set(binary_files))},'runtime':{str(p):digest(p) for p in sorted(set(runtime_files))}}
mode,path=sys.argv[1:];path=pathlib.Path(path);actual=capture()
if mode=='capture':path.write_text(json.dumps(actual,indent=2,sort_keys=True)+'\n')
elif mode=='check':assert json.loads(path.read_text())==actual,'Prepared source/binary/runtime/environment identity changed'
else:raise ValueError('capture|check <manifest>')
print(f'{mode}: source={len(actual["source"])}, binary={len(actual["binary"])}, runtime={len(actual["runtime"])}; manifestSha256={digest(path)}')
