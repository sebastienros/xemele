"""Actual counter-disabled child cleanup preflight using the lease supervisor."""
import importlib.util,os,sys,time
from pathlib import Path
sys.argv=['measure.py','--commands']
# Read definitions only: the dispatch below --commands must not execute.
path=Path(__file__).with_name('measure.py')
source=path.read_text().split("if sys.argv[1:] == ['--commands']:")[0]
scope={'__file__':str(path.resolve())};exec(compile(source,str(path),'exec'),scope)
scope['out']=Path('artifacts/M8-xsd-identity/cleanup-preflight');scope['out'].mkdir(parents=True,exist_ok=True)
start=time.monotonic()
record=scope['supervise']([sys.executable,'-c','print("counter-disabled cleanup child")'],0,os.environ.copy(),start+10,start+20)
assert record['exit']==0 and record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'],record
print(record)
