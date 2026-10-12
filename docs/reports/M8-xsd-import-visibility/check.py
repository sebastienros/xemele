"""Focused correctness only; no allocation counters, timers, broad discovery or measurements."""
import sys
sys.dont_write_bytecode = True
from pathlib import Path
import json
import subprocess
import xml.etree.ElementTree as ET
import hashlib
import time
import os
import signal
if sys.argv[1:] != ['--released']:
 raise SystemExit('Source-only hold: coordinator source review/release required before invoking --released.')
started=time.monotonic()
from family import Runner
import bindings
review_sha=os.environ['XEMELE_IMPORT_REVIEW_SHA256']
runner=Runner(os.environ['XEMELE_IMPORT_RUN_DIRECTORY']+'/checks',800,started=started)
runner.__enter__()
interruptions=__import__('nested_ownership').interruptions()
interruptions.__enter__()
import atexit
closed=False
def finish():
 global closed
 if not closed:
  closed=True
  # Any incomplete interpreter exit remains failure, including signal/error exit.
  runner.close('IncompleteCheckSequence')
atexit.register(finish)
bindings.review(review_sha)
commands=[]
def bounded(*args, **kwargs):
 check=kwargs.pop('check',False)
 environment=kwargs.pop('env',None)
 assert not kwargs, 'Unreviewed launch options'
 bindings.review(review_sha)
 binary_before, binary_folders=bindings.execution_inputs(args[0])
 if binary_before:bindings.archive_execution(runner.directory,len(runner.sequence),args[0],binary_before,binary_folders)
 r=runner.run(args[0],180,environment=environment,check=True)
 if binary_before:
  binary_after, after_folders=bindings.execution_inputs(args[0])
  assert binary_after==binary_before and after_folders==binary_folders,'Actual execution binaries changed during check'
 commands.append({'command':args[0],'exit':r.returncode})
 bindings.review(review_sha)
 return r
Path('artifacts/M8-xsd-import-visibility').mkdir(parents=True,exist_ok=True)
D='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
for project in ['tests/Xemele.Tests/Xemele.Tests.csproj','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj','docs/reports/M8-model/ApiCheck/ApiCheck.csproj','docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj']:
 bounded([D,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags],check=True)
 bounded([D,'build',project,'-c','Release','--no-restore',*flags],check=True)
out=runner.directory/'results';out.mkdir(parents=True,exist_ok=False)
def assert_tests(path, count=None, correctness=False):
 tests=list(ET.parse(path).getroot().iter('test'))
 assert tests and all(t.attrib['result']=='Pass' for t in tests)
 assert len({t.attrib['id'] for t in tests})==len(tests)
 if path.name=='correctness.xml':
  own=[t for t in tests if t.attrib['type'].startswith('Xemele.Tests.M8XsdImportVisibility.')]
  assert len(own)==91, ('owned rows',len(own))
  (out/'focused-summary.json').write_text(json.dumps({'owned_rows':len(own),'affected_rows':len(tests),'original_expectations':'all three original 1.0 INVALID expectations preserved','identities_sha256':hashlib.sha256('\n'.join(sorted(t.attrib['id'] for t in tests)).encode()).hexdigest()},indent=2)+'\n')
 if count is not None: assert len(tests)==count
 print('PASS test rows',len(tests))
 return len(tests)
args=[D,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
for ns in ['Xemele.Tests.M8XsdImportVisibility','Xemele.Tests.M8XsdOptionalComposition','Xemele.Tests.M8XsdParticleNormalization','Xemele.Tests.M8XsdTypeRelations','Xemele.Tests.M8XsdInstanceHintOrdering','Xemele.Tests.M8XsdContentFailureAssessment','Xemele.Tests.M8XsdCountedMatcher','Xemele.Tests.M8XsdInstanceAssessment','Xemele.Tests.M8XsdAttributeFixed','Xemele.Tests.M8SchemaResourceOutcomes','Xemele.Tests.M8XsdRecursiveComponents','Xemele.Tests.M8XsdComponentMapping','Xemele.Tests.M8XsdSchemaSyntax','Xemele.Tests.M8XsdFacade','Xemele.Tests.M8XsdPublicationCheckpoints','Xemele.Tests.M8XsdFixedRestriction','Xemele.Tests.M8SchemaResourceBase','Xemele.Tests.M8XsdValidate','Xemele.Tests.M8ConstraintLexical','Xemele.Tests.M8XsdContent','Xemele.Tests.M8XsdCompile','Xemele.Tests.M8XsdIdentity','Xemele.Tests.M8Facets','Xemele.Tests.M8Model','Xemele.Tests.M8SourceLocations','Xemele.Tests.M8XsdWildcardComposition','Xemele.Tests.M8XsdEffectiveContent']:args+=['-namespace',ns]
args+=['-method-','*WarmBorrowed*','-parallelMode','none','-noLogo','-noColor','-result-xml',str(out/'correctness.xml')]
correctness=bounded(args,check=True)
assert_tests(out/'correctness.xml',correctness=False)
resolver=next(r['args'] for r in json.loads(Path('docs/reports/M8-resource-kind/test-commands.json').read_text()) if r['label']=='resolver')
resolver=resolver[:resolver.index('-result-xml')]+['-result-xml',str(out/'resolver.xml')]
assert not any('WarmBorrowed' in item for item in resolver)
bounded(resolver,check=True);assert_tests(out/'resolver.xml',42)
bounded(['python3','docs/reports/M5-validation-model/check_api.py'],check=True)
bounded([D,'--fx-version','10.0.11','docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll','docs/reports/M8-xsd-validate/evaluation.golden.txt'],check=True)
for baseline in ['tests/Contracts/Fixtures/M8/Model.golden.txt','docs/api/M8-model.txt','docs/api/M8-xsd-facade.txt','docs/reports/M8-xsd-compile/compiler.golden.txt','docs/reports/M8-xsd-identity/identity.golden.txt','docs/reports/M8-xsd-content/content.golden.txt']:
 bounded([D,'--fx-version','10.0.11','docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll',baseline],check=True)
# Use the established all-visible extractor to compare each frozen lane as a subset.
internal=out/'internal.txt'
bounded([D,'--fx-version','10.0.11','docs/reports/M8-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll','emit','src/Xemele/bin/Release/net10.0/Xemele.dll',str(internal)],check=True)
def rows(path): return {r['id']:r for r in map(json.loads,Path(path).read_text().splitlines()[1:])}
actual=rows(internal)
for lane in ['Model','Regex','Datatypes','Facets','RelaxNg','Xsd','Schematron','Reporting']:
 expected=rows('tests/Contracts/Fixtures/M8/'+lane+'.golden.txt')
 assert all(actual.get(key)==value for key,value in expected.items()),lane
 print('PASS frozen lane',lane,len(expected))
# The fixture freezes all legacy resolver rows and the exact additive surface.
fixture=rows('tests/Contracts/Fixtures/M8/ResourceOutcomes/Public.golden.txt')
public=out/'public.txt'
bounded([D,'--fx-version','10.0.11','docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll','emit','src/Xemele/bin/Release/net10.0/Xemele.dll',str(public)],check=True)
public_rows=rows(public)
assert all(public_rows.get(key)==value for key,value in fixture.items())
print('PASS additive exact fixture',len(fixture))
project='samples/M8-xsd-facade/Facade.Consumer.csproj'
for xmlonly in [False,True]:
 xmlflags=[*flags,*(['-p:FacadeXmlOnly=true'] if xmlonly else [])]
 bounded([D,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*xmlflags],check=True)
 bounded([D,'build',project,'-c','Release','--no-restore',*xmlflags],check=True)
 import os
 env=dict(os.environ,FACADE_XML_ONLY='1' if xmlonly else '0')
 bounded([D,'--fx-version','10.0.11','samples/M8-xsd-facade/bin/Release/net10.0/Xemele.Benchmarks.dll'],check=True,env=env)
project='samples/M8-xsd-type-relations/Relations.Consumer.csproj'
for xmlonly in [False,True]:
 xmlflags=[*flags,*(['-p:RelationsXmlOnly=true'] if xmlonly else [])]
 bounded([D,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*xmlflags],check=True)
 bounded([D,'build',project,'-c','Release','--no-restore',*xmlflags],check=True)
 import os
 env=dict(os.environ,RELATIONS_XML_ONLY='1' if xmlonly else '0')
 bounded([D,'--fx-version','10.0.11','samples/M8-xsd-type-relations/bin/Release/net10.0/Xemele.Benchmarks.dll'],check=True,env=env)
datatypes=bounded([D,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-namespace','Xemele.Tests.M8Datatypes','-method-','*WarmBorrowed*','-parallelMode','none','-noLogo','-noColor','-result-xml',str(out/'datatypes.xml')],check=True)
assert_tests(out/'datatypes.xml',77)
# Probe control executes identical routes while allocation and heap counters are disabled.
project='benchmarks/Xemele.Benchmarks/M8-xsd-import-visibility/Import.Probe.csproj'
bounded([D,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags],check=True)
bounded([D,'build',project,'-c','Release','--no-restore',*flags],check=True)
bounded([D,'--fx-version','10.0.11','benchmarks/Xemele.Benchmarks/M8-xsd-import-visibility/bin/probe/net10.0/Xemele.Benchmarks.dll','--control',str(out/'control.json')],check=True)
control=json.loads((out/'control.json').read_text())
assert control['mode']=='--control' and control['status']=='passed' and len(control['rows'])==24 and control['consumed']==1012
assert control['coldInputsBytes']==0 and control['positiveBytes']==0 and all(r['bytes']==0 and r['firstRunBytes']==0 and not any(r['distribution']) for r in control['rows'])
assert all(h['before']==h['after']==h['delta']==0 for h in control['heap'])
bounded(['python3','tools/planning/tasks.py','validate'],check=True)
bounded(['git','diff','--check'],check=True)
bindings.review(review_sha)
receipt=runner.close(None)
closed=True
assert receipt['completed']
assert time.monotonic() < runner.hard-1
bindings.publish_success(out/'checks-completed.json',runner,
 {'correctness':'passed','resolver42':'passed','datatypes77':'passed','frozenAndAdditiveApis':'passed','normalAndXmlOnlyConsumers':'passed','disabledProbeControl':'passed','planning':'passed','diff':'passed'})
interruptions.__exit__(None,None,None)
print('Owned/affected correctness, safe resolver42, frozen/public API, normal and XML-only consumers passed.')
