"""Only run after durable coordinator GRANT; performs no raw inspection."""
from pathlib import Path
import subprocess,os,signal,time,json,hashlib,sys
manifest=json.loads(Path('docs/reports/M8-regex/preparation.json').read_text())
for p,digest in manifest['files'].items():assert hashlib.sha256(Path(p).read_bytes()).hexdigest()==digest,p
D='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
exe='benchmarks/Xemele.Benchmarks/M8-regex/bin/Release/net10.0/Xemele.Benchmarks.dll'
env=dict(os.environ,XEMELE_REGEX_REVISION=manifest['sourceRevision'],DOTNET_ROLL_FORWARD='Disable',DOTNET_ROOT=str(Path(D).parent),RestoreSources='/Users/sebastienros/.nuget/packages',RuntimeFrameworkVersion='10.0.11',UseSharedCompilation='false',NuGetAudit='false')
env['PATH']=str(Path(D).parent)+os.pathsep+env['PATH']
commands=[[D,'--fx-version','10.0.11',exe,'--measure','artifacts/M8-regex/allocation1.json'],[D,'--fx-version','10.0.11',exe,'--measure','artifacts/M8-regex/allocation2.json'],[D,'--fx-version','10.0.11',exe,'--bdn','artifacts/M8-regex/bdn']]
startAt = 1 if sys.argv[1:] == ['--remaining'] else 0
if sys.argv[1:] not in ([], ['--remaining']):raise ValueError('Only the explicitly granted full or remaining sequence is permitted')
if startAt:
 preserved=Path('artifacts/M8-regex/allocation1.json')
 receipt=json.loads(Path('docs/reports/M8-regex/partial-measurement.json').read_text())
 assert hashlib.sha256(preserved.read_bytes()).hexdigest()==receipt['preservedProbeSha256'],'Preserved first probe changed'
# Permission preflight occurs before counters start; the process must be authorized
# for read-only process inventory by its exact execution escalation.
subprocess.run(['ps','-axo','pid=,pgid=,stat='],stdout=subprocess.DEVNULL,check=True)
deadline=time.monotonic()+590
for i,args in enumerate(commands[startAt:],start=startAt):
 with open(f'artifacts/M8-regex/measurement-{i}.log','wb') as log:
  process=subprocess.Popen(args,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
  try:
   result=process.wait(timeout=max(0.01,deadline-time.monotonic()))
   if result:raise RuntimeError(f'Approved command {i} failed: {result}')
  except BaseException:
   try:os.killpg(process.pid,signal.SIGTERM)
   except ProcessLookupError:pass
   try:process.wait(timeout=5)
   except subprocess.TimeoutExpired:
    os.killpg(process.pid,signal.SIGKILL);process.wait()
   raise
  finally:
   # Reap any surviving benchmark/compiler descendants in the isolated process group.
   try:os.killpg(process.pid,signal.SIGTERM)
   except ProcessLookupError:pass
   drainDeadline=time.monotonic()+5
   while True:
    members=[row for row in subprocess.check_output(['ps','-axo','pid=,pgid=,stat='],text=True).splitlines() if len(row.split())>=3 and row.split()[1]==str(process.pid) and not row.split()[2].startswith('Z')]
    if not members:break
    if time.monotonic()>=drainDeadline:
     try:os.killpg(process.pid,signal.SIGKILL)
     except ProcessLookupError:pass
     raise RuntimeError('Descendant drain exceeded; group killed, coordinator must verify cleanup before ACK')
    time.sleep(0.1)
for p,digest in manifest['files'].items():assert hashlib.sha256(Path(p).read_bytes()).hexdigest()==digest,('Final prepared identity changed',p)
# All approved child commands are supervised in their own groups. No raw inspection here.
print('Approved sequence processes exited/reaped; logs closed. RELEASE required before raw inspection.')
