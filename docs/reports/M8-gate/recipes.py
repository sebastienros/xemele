"""Exact finite phase commands. Import performs no work or executable launch."""
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
OUT = ROOT/'artifacts/M8-gate'
NET = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
FLAGS = ['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
PROJECTS = [
 'tests/Xemele.Tests/Xemele.Tests.csproj', 'tests/Xemele.Conformance.Tests/Xemele.Conformance.Tests.csproj',
 'tests/Xemele.Differential.Tests/Xemele.Differential.Tests.csproj', 'tools/ReferenceRunners/Bcl/BclReference.csproj',
 'samples/M2-conformance-profile/Probe.csproj', 'samples/M2-conformance-grammar/Probe.csproj',
 'samples/XmlToolkit/XmlToolkit.csproj', 'src/Xemele.Tool/Xemele.Tool.csproj',
 'docs/reports/M4-corpus/Fuzz.Driver.csproj', 'docs/reports/M7-corpus/Fuzz.Driver.csproj',
 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', 'docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj',
 'docs/reports/M8-xsd-resolved-availability/harness/Missing.Api.csproj',
 'samples/M8-xsd-resolved-availability/Consumer.csproj', 'samples/M8-xsd-resolved-availability/XmlOnly.csproj',
 'docs/reports/M8-gate/CounterControl.csproj', 'samples/M7-cli/M7.CliSample.csproj', 'samples/M7-serialize/M7-serialize.csproj']
BINARY_FOLDERS = [str(Path(p).parent/'bin/Release/net10.0') for p in PROJECTS] + ['src/Xemele/bin/Release/net10.0']
PACKAGE_WORK = OUT/'external'
PHASES = {'prepare': {'work':12500,'reserve':180}, 'check': {'work':3000,'reserve':180}, 'identity': {'work':180,'reserve':40}, 'counter': {'work':1800,'reserve':180}}

def dll(path,*args): return [NET,'--fx-version','10.0.11',str(path),*map(str,args)]
def py(file,*args): return [sys.executable,'-B',file,*map(str,args)]
def commands(phase):
 if phase == 'prepare':
  commands = [(py('docs/reports/M8-gate/guard_controls.py','--synthetic'),90),(py('docs/reports/M1-corpus-import/provision.py'),120), (py('docs/reports/M1-corpus-import/import_suite.py','--check'),120)]
  for project in PROJECTS:
   commands += [([NET,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*FLAGS],240),
                ([NET,'build',project,'-c','Release','--no-restore','--no-incremental',*FLAGS],240)]
  commands += [(py('docs/reports/M8-gate/native_selection.py'),60),(py('docs/reports/M8-gate/package_inputs.py','layout'),60),
               ([NET,'pack','src/Xemele/Xemele.csproj','-c','Release','--no-build','--no-restore','-p:PackageVersion=0.8.0-integration','-o',str(PACKAGE_WORK/'feed'),*FLAGS],120),
               (py('docs/reports/M8-gate/package_inputs.py','package'),30)]
  project = str(PACKAGE_WORK/'consumer/Consumer.csproj')
  common = ['--source',str(PACKAGE_WORK/'feed'),'--packages',str(PACKAGE_WORK/'packages'),*FLAGS]
  commands += [([NET,'restore',project,*common],240),([NET,'build',project,'-c','Release','--no-restore','--no-incremental',*FLAGS],240)]
  for lane,prop in [('trim','PublishTrimmed'),('aot','PublishAot')]:
   commands += [([NET,'restore',project,'-r','osx-arm64',f'-p:{prop}=true',*common],300),
                ([NET,'publish',project,'-c','Release','--no-restore','--disable-build-servers','-m:1','-t:Rebuild,Publish',
                  '-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false',
                  '-r','osx-arm64','--self-contained','true',f'-p:{prop}=true','-o',str(PACKAGE_WORK/lane)],600)]
  return commands + [(py('docs/reports/M8-gate/native_selection.py'),60),(py('docs/reports/M8-gate/package_inputs.py','freeze'),60)]
 if phase == 'check':
  return [(py('docs/reports/M8-gate/identity_controls.py','${CERT}'),1000),(py('docs/reports/M8-gate/reuse.py'),90),(py('docs/reports/M8-gate/guard_controls.py','--archive'),90),
          (py('docs/reports/M8-gate/semantic.py','api'),180),
          (dll('samples/M8-xsd-resolved-availability/bin/Release/net10.0/Consumer.dll'),60),
          (dll('samples/M8-xsd-resolved-availability/bin/Release/net10.0/XmlOnly.dll'),60),
          (py('docs/reports/M8-gate/semantic.py','grammar'),240),
          (dll('docs/reports/M4-corpus/bin/Release/net10.0/Fuzz.Driver.dll'),60),
          (dll('docs/reports/M7-corpus/bin/Release/net10.0/Fuzz.Driver.dll'),90),
          (dll('samples/XmlToolkit/bin/Release/net10.0/XmlToolkit.dll',ROOT/'tests/corpora'),60),
          (dll('samples/M7-cli/bin/Release/net10.0/M7.CliSample.dll'),60),
          (dll('samples/M7-serialize/bin/Release/net10.0/M7-serialize.dll'),60),
          (dll('docs/reports/M8-gate/bin/Release/net10.0/CounterControl.dll','--disabled'),30),
          (py('docs/reports/M8-gate/semantic.py','external','managed'),60),
          (py('docs/reports/M8-gate/semantic.py','external','trim'),60),
          (py('docs/reports/M8-gate/semantic.py','external','aot'),60),
          (py('tools/planning/tasks.py','validate'),30), (['/usr/bin/git','diff','--check'],30)]
 if phase == 'identity':return []
 if phase == 'counter':
  commands = [(dll('docs/reports/M8-gate/bin/Release/net10.0/CounterControl.dll','--positive'),30)]
  for group in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']:
   commands += [(dll(f'tests/{group}/bin/Release/net10.0/{group}.dll','-parallelMode','none','-culture','invariant','-noColor',
                    '-result-xml', '${RAW}/'+group+'.xml'),500)]
  return commands
 raise ValueError('Unknown released phase')
