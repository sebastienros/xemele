#!/usr/bin/env python3
"""Bounded ordinary preparation; no allocation, timing or inherited full suite."""
import os, pathlib, signal, subprocess, json, time
root=pathlib.Path(__file__).resolve().parents[3]
dotnet='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
cache='/Users/sebastienros/.nuget/packages'
projects=['tests/Xemele.Tests/Xemele.Tests.csproj','samples/M6-xinclude/XIncludeSample.csproj','benchmarks/Xemele.Benchmarks/M6-xinclude/XInclude.Probe.csproj','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj']
phases=[]
for project in projects:
    phases.append((f'restore:{project}',[dotnet,'restore',project,'--source',cache,'-p:RuntimeFrameworkVersion=10.0.11','-p:NuGetAudit=false','--disable-build-servers']))
    phases.append((f'build:{project}',[dotnet,'build',project,'-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11']))
phases.extend([
    ('focused',[dotnet,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-namespace','Xemele.Tests.M6XInclude','-noColor','-parallelMode','none']),
    ('sample',[dotnet,'--fx-version','10.0.11','samples/M6-xinclude/bin/Release/net10.0/XIncludeSample.dll']),
    ('discovery',[dotnet,'--fx-version','10.0.11','benchmarks/Xemele.Benchmarks/M6-xinclude/bin/Release/net10.0/Xemele.Benchmarks.dll','--discover',str(root/'artifacts/M6-xinclude/bdn')]),
    ('api',['python3','-B','docs/reports/M6-xinclude/check_api.py']),
    ('planning',['python3','-B','tools/planning/tasks.py','validate'])])
output=root/'artifacts/M6-xinclude';output.mkdir(parents=True,exist_ok=True)
results=[];process=None;groups=[];deadline=time.monotonic()+600
status={'exit':0,'noChildren':True,'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'results':results}
try:
    for index,(phase,command) in enumerate(phases):
        entry={'phase':phase,'exit':None};results.append(entry)
        with (output/f'prepare-{index}.log').open('w') as log:
            process=subprocess.Popen(command,cwd=root,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
            groups.append(process.pid);entry['exit']=process.wait(timeout=min(90,deadline-time.monotonic()))
        if entry['exit']!=0:raise subprocess.CalledProcessError(entry['exit'],command)
        process=None;print(phase+' passed',flush=True)
except BaseException as error:
    status['exit']=getattr(error,'returncode',1) or 1;status['error']=type(error).__name__
finally:
    for group in groups:
        try:os.killpg(group,signal.SIGKILL)
        except ProcessLookupError:pass
    if process is not None:process.wait(timeout=5)
    for group in groups:
        inventory=subprocess.run(['ps','-o','pid=,pgid=','-g',str(group)],capture_output=True,text=True,timeout=5)
        if inventory.returncode not in (0,1) or any(int(row.split()[1])==group for row in inventory.stdout.splitlines()):status['noChildren']=False
    if not status['noChildren']:status['exit']=status['exit'] or 1
    (output/'prepare-status.json').write_text(json.dumps(status,indent=2)+'\n')
    print(json.dumps(status,sort_keys=True),flush=True)
raise SystemExit(status['exit'])
