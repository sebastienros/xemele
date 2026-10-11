#!/usr/bin/env python3
"""Persist inspected gate outputs; does not run or infer unexecuted checks."""
import hashlib,json,pathlib,shutil,subprocess
root=pathlib.Path(__file__).resolve().parents[3];raw=root/'artifacts/M7-gate';owned=root/'docs/reports/M7-gate'
def load(name):return json.loads((raw/name).read_text())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def digest(value):return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()
manifest=load('prepared.json');cumulative=load('cumulative.json');api=load('api-full.json');children=load('allocation-children.json');grammar=load('grammar.json');baseline=json.loads((root/'docs/reports/M6-gate/summary.json').read_text())
assert api['matched'] and api['checkerExit']==0 and api['inventory']['apiDeclarations']==1253 and api['inventory']['fixtures']==3388 and not api['inventory']['bootstrapEmptyApi'] and len(api['inventory']['apiFragments'])==34
assert sum(g['passed'] for g in cumulative['groups'])==8717 and sum(g['earlierCount'] for g in cumulative['groups'])==6985
assert grammar['result']=='passed' and grammar['counts']==baseline['grammar']['counts']
for key in ['caseModelHashes','originalOutputWitnesses','referenceResolutions']:assert digest(grammar[key])==baseline['grammar'][key+'Sha256']
p=grammar['provenance']
for key,domain in [('sourceInputs','source'),('compiledOutputClosure','binary')]:
 for path,value in p[key].items():assert manifest[domain][str(root/path)]==value
for path,value in p['runtimeClosure'].items():assert manifest['runtime'][path]==value
assert p['dotnetExecutableSha256']==manifest['runtime']['/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet']
# Confirm no product/test/helper source changed since the leased run.
source_count=0
for path,value in manifest['source'].items():
 relative=pathlib.Path(path).relative_to(root)
 if relative.parts[0] in ['src','tests','tools','samples','benchmarks'] and 'obj' not in relative.parts:
  assert sha(pathlib.Path(path))==value,path;source_count+=1
package=load('package/package-identity.json');assert package['source_package_dll_equal'] and package['sha256']==api['assemblySha256']
expected=(root/'tests/corpora/xinclude/expected.txt').read_bytes()+ b'\n'+ (owned/'consumer-expected.txt').read_bytes()
lanes=[]
for name in ['package-managed','trim','aot']:
 stdout=(raw/'package'/(name+'.stdout')).read_bytes();stderr=(raw/'package'/(name+'.stderr')).read_text().strip()
 assert stdout==expected and stderr=='Framework=.NET 10.0.11; Architecture=Arm64'
 lanes.append(dict(lane=name,framework='.NET 10.0.11',architecture='Arm64',stdoutSha256=hashlib.sha256(stdout).hexdigest(),independentExpectedOutputMatches=True))
assert (raw/'package/external-sdk.log').read_text().strip()=='10.0.400'
assert 'Mach-O 64-bit executable arm64' in (raw/'package/aot-file.txt').read_text()
retained=raw/'packages';retained.mkdir(exist_ok=True);original=pathlib.Path(package['package']);shutil.copyfile(original,retained/original.name)
cli={}
for name,count in [('html-cli-managed',111),('xml-cli-managed',57)]:
 data=json.loads((raw/(name+'.log')).read_text());assert data['passed']==len(data['cases'])==count;cli[name]=dict(passed=count,identitiesSha256=digest(data['cases']))
assert '28 controls; .NET 10.0.11; Arm64' in (raw/'html-cli-sample.log').read_text()
assert load('api-controls.json')['passed']==7
summary=dict(testedRuntimeSourcesRevision='b914fd0edd9b79f9788c5b8c1fdf9bbf75be7089',leasedTestRevision=manifest['revision'],consumerHarnessRevision=subprocess.check_output(['git','rev-parse','9994fd3'],cwd=root,text=True).strip(),cumulative=cumulative,leasedLifecycle=dict(grantRevision=subprocess.check_output(['git','rev-parse','e419112'],cwd=root,text=True).strip(),releaseRevision=subprocess.check_output(['git','rev-parse','6faf7c9'],cwd=root,text=True).strip(),wrapperExit=0,initialIdentity=0,suiteExits=[0,0,0],finalIdentity=0,noChild=True,maximumSeconds=1100,exclusiveRuns=1,manifestSha256=sha(raw/'prepared.json'),closureCounts={k:len(manifest[k]) for k in ['source','binary','runtime']},effectiveEnvironmentSha256=manifest['environmentSha256'],runtimeEnvironment=manifest['runtimeEnvironment'],host=manifest['host'],controls=dict(positive=0,unknownEnvironmentNegative=1,finalPositive=0,noChild=True,runs=[])),allocationChildren=children,grammar=dict(result='passed',counts=grammar['counts'],**{key+'Sha256':digest(grammar[key]) for key in ['caseModelHashes','originalOutputWitnesses','referenceResolutions','provenance']},exactEarlierCountsModelsWitnessesResolutions=True,currentProvenanceMatchesLeasedClosure=True,provenanceHashChangedForM7SourcesOutputs=True,childExitAndCleanup=grammar['childExitAndCleanup'],sourceOutputClosureUnchanged=grammar['sourceOutputClosureUnchanged']),xpathFuzz=load('fuzz.json'),htmlFuzz=load('html-fuzz.json'),api=dict(matched=True,checkerExit=0,checkerOutput=api['checkerOutput'],assemblySha256=api['assemblySha256'],members=1253,fragments=34,fixtures=3388,bootstrapEmptyApi=False,negativeControls=7,toolPublicMembers=0),cli=cli|dict(xmlExamples=7,htmlUsageOwnershipSample=28,nativeEvidence='Verified unchanged M7-cli runtime source: managed/native111HTML+57XML at a3dc9a68; nativeSHA556a3abc9cbe8f1cbde44cb0d0509cc122746a487922d3ed3dfa5f19343d8fc6; docs/reports/M7-cli/integration.md. Native CLI not republished during gate.'),package=package|dict(packageSha256=sha(retained/original.name),retained=str(retained/original.name),published=False,sdk='10.0.400',lanes=lanes,nativeBinarySha256=sha(pathlib.Path(package['work'])/'aot/Consumer')),sourcePreservation=dict(files=source_count,allActualProductTestFixtureHelperSourcesUnchangedSinceLease=True),followups=dict(m3Allocation='Historical uncontrolled7789/7788pass/1fail expected0 actual7744B preserved in M7-corpus report. Identical source assertion passes serial invariant established tiering/PGO/R2R-disabled exclusive gate; no excluded tests or product fix; precise default-context cause not proven.'),failures=[dict(scope='external consumer initial build',result='failed CS0103 LegacyHtmlParser name does not exist; owned harness corrected to actual HtmlParser, rerun managed/trim/native all passed; no product changes'),dict(scope='allocation report inspection',result='Two inspection attempts failed on inherited report shape differences (optional NativeGcLibrary and encoding Labels); corrected owned inspector reads actual schemas, all12 pairs independently inspected; no probe rerun or data change')],limits=['Actual .NET10.0.11/SDK10.0.400/macOSArm64; no other platform claimed','No new throughput/cold/retention campaign; earlier verified lane scopes retained','Local unpublished package, original caches and M6 checkpoint retained; no M8 validators or successor created'])
(owned/'summary.json').write_text(json.dumps(summary,indent=2)+'\n');print('Inspected8717tests/6985earlieridentities/12allocationpairs/1253API/3388fixtures/111HTML57XML/package3lanes; packageSHA',summary['package']['packageSha256'])
