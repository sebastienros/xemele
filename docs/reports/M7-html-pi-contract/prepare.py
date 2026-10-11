#!/usr/bin/env python3
"""Emit the three additive, nonpackaged declaration baselines."""
import argparse,os,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[3];env=os.environ.copy();env['NoWarn']='CS0436'
def run(args):
 r=subprocess.run(args,cwd=root,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 assert r.returncode==0,r.stdout
 print(r.stdout.strip())
fixtures=root/'tests/Contracts/Fixtures/M7'
checker=root/'docs/reports/M7-contracts/ApiCheck'
for folder in [checker]+[fixtures/lane for lane in ['Engine','Tokenizer','Selector']]:
 project=next(folder.glob('*.csproj'))
 run([a.dotnet,'restore',str(project),'--source',str(fixtures),'-p:NuGetAudit=false','--verbosity','quiet'])
 run([a.dotnet,'build',str(project),'-c','Release','--no-restore','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','--disable-build-servers','--verbosity','quiet'])
 if folder!=checker:
  lane=folder.name
  run([a.dotnet,'--fx-version','10.0.11',str(checker/'bin/Release/net10.0/ApiCheck.dll'),'emit',str(folder/'bin/Release/net10.0'/(lane+'.dll')),str(fixtures/(lane+'.golden.txt'))])
