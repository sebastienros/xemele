#!/usr/bin/env python3
"""Run only under a coordinator's exclusive grant; never builds or restores."""
import os
import pathlib
import subprocess
import sys
root=pathlib.Path(__file__).resolve().parents[3]
manifest=pathlib.Path(sys.argv[1]).resolve()
output=pathlib.Path(sys.argv[2]).resolve()
output.mkdir(parents=True,exist_ok=True)
dotnet='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
probe=str(root/'docs/reports/M4-functions/bin/Release/net10.0/Xemele.Benchmarks.dll')
env=dict(os.environ,XEMELE_FUNCTIONS_REVISION=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip())
def check():subprocess.run(['python3',str(root/'docs/reports/M4-functions/freeze.py'),'check',str(manifest)],cwd=root,check=True)
check()
try:
    for mode,destination,seconds in [('--control',output/'control.json',90),('--discover',output/'discovery',30),('--allocation',output/'allocation.json',120),('--benchmark',output/'bdn',600)]:
        command=[dotnet,probe,mode,str(destination)]
        with (output/(mode[2:]+'.log')).open('w') as log:
            subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=seconds,check=True)
        print(f'{mode} passed (bound {seconds}s)',flush=True)
finally:check()
