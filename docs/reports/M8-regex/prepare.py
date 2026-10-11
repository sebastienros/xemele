from pathlib import Path
import subprocess,json,hashlib
D='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
p='benchmarks/Xemele.Benchmarks/M8-regex/Regex.Measure.csproj'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
subprocess.run([D,'restore',p,'--source','/Users/sebastienros/.nuget/packages',*flags],check=True)
subprocess.run([D,'build',p,'-c','Release','--no-restore',*flags],check=True)
out=Path('artifacts/M8-regex');out.mkdir(parents=True,exist_ok=True)
bin=Path('benchmarks/Xemele.Benchmarks/M8-regex/bin/Release/net10.0')
subprocess.run([D,'--fx-version','10.0.11',str(bin/'Xemele.Benchmarks.dll'),'--control',str(out/'control.json')],check=True)
subprocess.run([D,'--fx-version','10.0.11',str(bin/'Xemele.Benchmarks.dll'),'--discover',str(out/'bdn')],check=True)
paths=[bin/'Xemele.Benchmarks.dll',bin/'Xemele.dll',bin/'Xemele.Benchmarks.runtimeconfig.json',Path(D),Path(D).parent/'shared/Microsoft.NETCore.App/10.0.11/System.Private.CoreLib.dll',Path(D).parent/'shared/Microsoft.NETCore.App/10.0.11/libcoreclr.dylib',Path(D).parent/'shared/Microsoft.NETCore.App/10.0.11/libclrgc.dylib']
paths+=list(bin.glob('*.dll'))+list(Path('benchmarks/Xemele.Benchmarks/M8-regex').glob('*.csproj'))+[Path('Directory.Build.props'),Path('Directory.Packages.props'),Path('global.json')]
paths+=list(Path('src/Xemele/Validation/Regex').glob('*.cs'))+list(Path('benchmarks/Xemele.Benchmarks/M8-regex').glob('*.fixture'))+list(Path('benchmarks/Xemele.Benchmarks/M8-regex').glob('*.cs'))+list(Path('tests/fixtures/M8-regex').glob('*'))
control=json.loads((out/'control.json').read_text());assert control['mode']=='--control'
manifest={'sourceRevision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'files':{str(x.resolve()):hashlib.sha256(x.read_bytes()).hexdigest() for x in paths},'control':{k:control[k] for k in ['runtime','architecture','os','serverGc','inputSha256','invalidScalar']},'commands':[[D,'--fx-version','10.0.11',str(bin/'Xemele.Benchmarks.dll'),'--measure','artifacts/M8-regex/allocation1.json'],[D,'--fx-version','10.0.11',str(bin/'Xemele.Benchmarks.dll'),'--measure','artifacts/M8-regex/allocation2.json'],[D,'--fx-version','10.0.11',str(bin/'Xemele.Benchmarks.dll'),'--bdn','artifacts/M8-regex/bdn']],'bdnDiscovery':'8 ShortRun NoEmit borrowed-match cases; 1 launch/3 warmup/3 measurement; no generation/builds','maximumSeconds':600}
Path('docs/reports/M8-regex/preparation.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('Prepared identity manifest and counters-disabled control; no measurements run.')
