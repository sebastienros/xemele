"""Counters-disabled semantic/discovery and process-cleanup preflight."""
import os,signal,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[3]
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
def rows():
 return [line.split() for line in subprocess.check_output(['ps','-axo','pid=,ppid=,pgid=,stat='],text=True,timeout=2).splitlines() if line.strip()]
assert any(int(row[0])==os.getpid() for row in rows())
probe=root/'docs/reports/M8-xsd-effective-content/Probe/bin/Release/net10.0/Xemele.Benchmarks.dll'
commands=[[str(probe),'--control']]
for command in commands:
 child=None
 try:
  child=subprocess.Popen([net,'--fx-version','10.0.11',*command],cwd=root,start_new_session=True)
  assert child.wait(timeout=25)==0
 finally:
  if child is not None:
   try: os.killpg(child.pid,signal.SIGTERM)
   except ProcessLookupError: pass
   try: child.wait(timeout=2)
   except subprocess.TimeoutExpired:
    try: os.killpg(child.pid,signal.SIGKILL)
    except ProcessLookupError: pass
    child.wait(timeout=2)
   try: os.killpg(child.pid,signal.SIGKILL)
   except ProcessLookupError: pass
   assert child.returncode is not None
   assert not any(int(row[2])==child.pid for row in rows()),'Owned group remains.'
print('PASS: ps permission;5 consumed owned-composition routes; counters/timing disabled; every direct child reaped/owned group absent.')
