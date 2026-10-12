#!/usr/bin/env python3
"""Scoped correctness, frozen/public API and actual consumers; no measurements."""
import subprocess,xml.etree.ElementTree as E
from pathlib import Path
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(*args):subprocess.run(args,check=True)
def build(project,*extra):
 run(net,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags,*extra)
 run(net,'build',project,'-c','Release','--no-restore',*flags,*extra)
def tests(path,expected=None):
 a=E.parse(path).getroot().find('assembly');assert int(a.attrib['passed'])>0 and all(int(a.attrib[k])==0 for k in ['failed','errors','skipped','not-run']),a.attrib
 if expected is not None:assert int(a.attrib['passed'])==expected,a.attrib
 print('ASSERTED',path,a.attrib['passed'],flush=True)
out=Path('artifacts/M8-sch-report');out.mkdir(parents=True,exist_ok=True)
build('tests/Xemele.Tests/Xemele.Tests.csproj')
prefix=[net,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
run(*prefix,'-namespace','Xemele.Tests.M8SchReport','-result-xml',str(out/'focused.xml'));tests(out/'focused.xml',42)
a=[]
for ns in ['Xemele.Tests.M8SchValidate','Xemele.Tests.M8SchCompile','Xemele.Tests.M8Model','Xemele.Tests.M8SourceLocations','Xemele.Tests.M8XPathAccounting','Xemele.Tests.M8SchemaResourceBase','Xemele.Tests.M4Evaluate','Xemele.Tests.M5ValidationModel','Xemele.Tests.DtdValidation','Xemele.Tests.Serialization']:
 a+=['-namespace',ns]
run(*prefix,*a,'-result-xml',str(out/'prerequisites.xml'));tests(out/'prerequisites.xml',504)
build('docs/reports/M8-sch-report/ApiCheck/ApiCheck.csproj')
api=[net,'--fx-version','10.0.11','docs/reports/M8-sch-report/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll']
for baseline in ['docs/api/M8-sch-report.txt','docs/reports/M8-sch-validate/frozen.txt','docs/reports/M8-sch-compile/frozen.txt']:
 run(*api,baseline)
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj');run('python3','docs/reports/M5-validation-model/check_api.py')
for xmlonly in ['false','true']:
 build('samples/M8-sch-report/Consumer.csproj','-p:SchXmlOnly='+xmlonly)
 run(net,'--fx-version','10.0.11','samples/M8-sch-report/bin/Release/net10.0/Xemele.Benchmarks.dll')
run('python3','tools/planning/tasks.py','validate');run('git','diff','--check')
