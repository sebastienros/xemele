"""Released preparation-only actual Xcode selection guards; never compiles or runs a product."""
import sys
sys.dont_write_bytecode=True
import os,subprocess,time,json
from pathlib import Path
from identities import native_identity
expected=native_identity()
end=min(float(os.environ['XEMELE_GATE_WORK_DEADLINE']),time.monotonic()+25)
def read(args):
 p=subprocess.run(['/usr/bin/xcrun',*args],capture_output=True,text=True,timeout=max(.001,end-time.monotonic()))
 assert p.returncode==0,p.stderr
 return str(Path(p.stdout.strip()).resolve())
assert read(['--find','clang'])==expected['clang']
assert read(['--find','ld'])==expected['linker']
assert read(['--sdk','macosx','--show-sdk-path'])==expected['sdk']
assert native_identity()==expected
print('PASS native selection '+json.dumps({key:expected[key] for key in ['developer','clang','linker','sdk']}))
