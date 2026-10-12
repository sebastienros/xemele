"""Task-scoped correctness checks. No allocation counters, timings or full gate."""
import subprocess,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'artifacts/M8-rng-corpus/checks'
NET='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
FLAGS=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def main():
 OUT.mkdir(parents=True,exist_ok=True);failures=[];number=0;built=set()
 def run(*args):
  nonlocal number
  number+=1
  with (OUT/f'{number:02}.log').open('w') as log:r=subprocess.run(args,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
  print(('PASS' if r.returncode==0 else 'FAIL')+': '+' '.join(str(a) for a in args),flush=True)
  if r.returncode:failures.append((args,r.returncode))
  return r.returncode==0
 def build(project,*extra):
  key=(project,extra)
  if key in built:return True
  if not run(NET,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*FLAGS,*extra):return False
  ok=run(NET,'build',project,'-c','Release','--no-restore',*FLAGS,*extra)
  if ok:built.add(key)
  return ok
 def test(project,classes,name,count):
  if not build(project):return
  dll=Path(project).parent/'bin/Release/net10.0'/Path(project).with_suffix('.dll').name
  arguments=[]
  for cls in classes:arguments+=['-class',cls]
  report=OUT/(name+'.xml')
  run(NET,'--fx-version','10.0.11',str(dll),*arguments,'-result-xml',str(report))
  a=ET.parse(report).getroot().find('assembly');counts={k:int(a.attrib[k]) for k in ['total','passed','failed','skipped','errors','not-run']}
  print(name+': '+str(counts),flush=True)
  if counts['passed']!=count or any(counts[k] for k in ['failed','errors','skipped','not-run']) or '10.0.11' not in a.attrib['environment']:failures.append((name,counts))
 test('tests/Xemele.Conformance.Tests/Xemele.Conformance.Tests.csproj',['Xemele.Conformance.Tests.RelaxNg.*'],'conformance',417)
 test('tests/Xemele.Differential.Tests/Xemele.Differential.Tests.csproj',['Xemele.Differential.Tests.RelaxNg.*'],'differential',2)
 test('tests/Xemele.Tests/Xemele.Tests.csproj',['Xemele.Tests.M8RngValidate.*'],'rng-owned',89)
 # Only correctness classes; never select WarmBorrowedReadsAllocateNothing.
 test('tests/Xemele.Tests/Xemele.Tests.csproj',['Xemele.Tests.M8RngCompile.*'],'rng-compile',88)
 test('tests/Xemele.Tests/Xemele.Tests.csproj',['Xemele.Tests.M8RngLexical.*'],'rng-lexical',80)
 if build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj'):run('python3','docs/reports/M5-validation-model/check_api.py')
 if build('docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj'):run('python3','docs/reports/M8-rng-validate/frozen.py')
 consumer='samples/M8-rng-validate/Consumer.csproj'
 for extra in [[],['-p:RngXmlOnly=true']]:
  if build(consumer,*extra):run(NET,'--fx-version','10.0.11','samples/M8-rng-validate/bin/Release/net10.0/Consumer.dll')
 run('python3','docs/reports/M8-rng-corpus/verify.py');run('python3','tools/planning/tasks.py','validate');run('git','diff','--check')
 if failures:raise SystemExit('Checks requiring attention: '+str(failures))
if __name__=='__main__':main()
