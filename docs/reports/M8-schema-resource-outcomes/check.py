"""Focused correctness only; no allocation counters, timers, broad discovery or measurements."""
from pathlib import Path
import json
import subprocess
import xml.etree.ElementTree as ET
D='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
for project in ['tests/Xemele.Tests/Xemele.Tests.csproj','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj','docs/reports/M8-model/ApiCheck/ApiCheck.csproj','docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj']:
 subprocess.run([D,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags],check=True)
 subprocess.run([D,'build',project,'-c','Release','--no-restore',*flags],check=True)
out=Path('artifacts/M8-schema-resource-outcomes');out.mkdir(parents=True,exist_ok=True)
failures=[]
def assert_tests(path,count=None):
 r=ET.parse(path).getroot();a=r.find('assembly')
 count=int(a.attrib['passed']) if count is None else count
 assert int(a.attrib['passed'])==count and all(int(a.attrib[k])==0 for k in ['errors','failed','skipped','not-run']),a.attrib
 assert '10.0.11' in a.attrib['environment']
 assert len(list(r.iter('test')))==count and all(t.attrib['result']=='Pass' for t in r.iter('test'))
 assert all('WarmBorrowed' not in t.attrib['name'] for t in r.iter('test'))
args=[D,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
for ns in ['Xemele.Tests.M8SchemaResourceOutcomes','Xemele.Tests.M8RngCompile','Xemele.Tests.M8RngValidate','Xemele.Tests.M8SchCompile','Xemele.Tests.M8SchValidate','Xemele.Tests.M8SchReport','Xemele.Tests.SchemaResources','Xemele.Tests.ResourceKinds','Xemele.Tests.CatalogTests','Xemele.Tests.M8XsdFacade','Xemele.Tests.M8XsdPublicationCheckpoints','Xemele.Tests.M8XsdFixedRestriction','Xemele.Tests.M8SchemaResourceBase','Xemele.Tests.M8XsdValidate','Xemele.Tests.M8ConstraintLexical','Xemele.Tests.M8XsdContent','Xemele.Tests.M8XsdCompile','Xemele.Tests.M8XsdIdentity','Xemele.Tests.M8Facets','Xemele.Tests.M8Model','Xemele.Tests.M8SourceLocations','Xemele.Tests.M8XsdWildcardComposition','Xemele.Tests.M8XsdEffectiveContent']:args+=['-namespace',ns]
args+=['-method-','*WarmBorrowed*','-parallelMode','none','-noLogo','-noColor','-result-xml',str(out/'correctness.xml')]
correctness=subprocess.run(args)
try: assert_tests(out/'correctness.xml',1530)
except AssertionError: failures.append('affected correctness')
if correctness.returncode and 'affected correctness' not in failures: failures.append('affected correctness')
resolver=next(r['args'] for r in json.loads(Path('docs/reports/M8-resource-kind/test-commands.json').read_text()) if r['label']=='resolver')
resolver=resolver[:resolver.index('-result-xml')]+['-result-xml',str(out/'resolver.xml')]
assert not any('WarmBorrowed' in item for item in resolver)
subprocess.run(resolver,check=True);assert_tests(out/'resolver.xml',42)
subprocess.run(['python3','docs/reports/M5-validation-model/check_api.py'],check=True)
subprocess.run([D,'--fx-version','10.0.11','docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll','docs/reports/M8-xsd-validate/evaluation.golden.txt'],check=True)
for baseline in ['docs/api/M8-xsd-facade.txt','docs/reports/M8-xsd-compile/compiler.golden.txt','docs/reports/M8-xsd-identity/identity.golden.txt','docs/reports/M8-xsd-content/content.golden.txt']:
 subprocess.run([D,'--fx-version','10.0.11','docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll',baseline],check=True)
# Use the established all-visible extractor to compare each frozen lane as a subset.
internal=out/'internal.txt'
subprocess.run([D,'--fx-version','10.0.11','docs/reports/M8-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll','emit','src/Xemele/bin/Release/net10.0/Xemele.dll',str(internal)],check=True)
def rows(path): return {r['id']:r for r in map(json.loads,Path(path).read_text().splitlines()[1:])}
actual=rows(internal)
for lane in ['Model','Regex','Datatypes','Facets','RelaxNg','Xsd','Schematron','Reporting']:
 expected=rows('tests/Contracts/Fixtures/M8/'+lane+'.golden.txt')
 assert all(actual.get(key)==value for key,value in expected.items()),lane
 print('PASS frozen lane',lane,len(expected))
# The fixture freezes all legacy resolver rows and the exact additive surface.
fixture=rows('tests/Contracts/Fixtures/M8/ResourceOutcomes/Public.golden.txt')
public=out/'public.txt'
subprocess.run([D,'--fx-version','10.0.11','docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll','emit','src/Xemele/bin/Release/net10.0/Xemele.dll',str(public)],check=True)
public_rows=rows(public)
assert all(public_rows.get(key)==value for key,value in fixture.items())
print('PASS additive exact fixture',len(fixture))
project='samples/M8-schema-resource-outcomes/Consumer.csproj'
for xmlonly in [False,True]:
 xmlflags=[*flags,*(['-p:OutcomesXmlOnly=true'] if xmlonly else [])]
 subprocess.run([D,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*xmlflags],check=True)
 subprocess.run([D,'build',project,'-c','Release','--no-restore',*xmlflags],check=True)
 import os
 env=dict(os.environ,OUTCOMES_XML_ONLY='1' if xmlonly else '0')
 subprocess.run([D,'--fx-version','10.0.11','samples/M8-schema-resource-outcomes/bin/Release/net10.0/Xemele.Benchmarks.dll'],check=True,env=env)
subprocess.run(['python3','tools/planning/tasks.py','validate'],check=True)
subprocess.run(['git','diff','--check'],check=True)
if failures:
 print('FAILED: '+', '.join(failures)+'; safe resolver42, frozen/public API, normal/XML-only consumers and planning completed separately.')
 raise SystemExit(1)
print('Owned/affected correctness, safe resolver42, frozen/public API, normal and XML-only consumers passed.')
