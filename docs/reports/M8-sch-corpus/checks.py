#!/usr/bin/env python3
"""Scoped corpus, preservation, APIs and real consumers; no measurements."""
import os,subprocess,xml.etree.ElementTree as E
from pathlib import Path
root=Path(__file__).resolve().parents[3];os.chdir(root)
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(*args):subprocess.run(args,check=True)
def build(project):
 run(net,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags)
 run(net,'build',project,'-c','Release','--no-restore',*flags)
def tests(project,namespace,count,name):
 build('tests/'+project+'/'+project+'.csproj')
 path=Path('artifacts/M8-sch-corpus')/(name+'.xml');path.parent.mkdir(parents=True,exist_ok=True)
 run(net,'--fx-version','10.0.11','tests/'+project+'/bin/Release/net10.0/'+project+'.dll','-namespace',namespace,'-result-xml',str(path))
 a=E.parse(path).getroot().find('assembly');assert int(a.attrib['passed'])==count and all(int(a.attrib[k])==0 for k in ['failed','errors','skipped','not-run']),a.attrib
 print('ASSERTED',name,count,flush=True)
run('python3','docs/reports/M8-sch-corpus/verify.py')
tests('Xemele.Conformance.Tests','Xemele.Conformance.Tests.Schematron',90,'conformance')
tests('Xemele.Differential.Tests','Xemele.Differential.Tests.Schematron',96,'differential')
# Verified dependency script covers42 reporting+504 affected checks, frozen3/9/154,
# public1403 and regular/genuine XML-only consumers. Counts overlap predecessors.
run('python3','docs/reports/M8-sch-report/checks.py')
run('python3','tools/planning/tasks.py','validate');run('git','diff','--check')
