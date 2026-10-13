"""Reassess the fixed original XSD corpus on explicitly verified M9 runtime repairs.

This is ordinary deterministic corpus validation. It does not import or run any
fuzz target, instrumenter or engine. The released M8 executor/inspection/cleanup
functions are reused unchanged; its historical runtime/HOLD manifest is retained.
"""
import sys
sys.dont_write_bytecode=True
import collections,hashlib,importlib.util,json,os,subprocess,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
released=module('released_xsd',ROOT/'docs/reports/M8-xsd-corpus/run.py')
approved=module('approved_xsd',ROOT/'docs/reports/M8-xsd-corpus/approved_dispositions.py')
out=ROOT/'artifacts/M8-xsd-corpus/m9-current-xsd';out.mkdir(parents=True,exist_ok=False)
host=Path(released.D);corpus=ROOT/'tests/corpora/xsd'
revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
assert not subprocess.check_output(['git','status','--porcelain'],cwd=ROOT,text=True).strip(),'Reviewed source must be committed'
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

# Renew the exact current source binding through integrated verified owners.
# No old source mismatch is ignored, and no original HOLD/API record is edited.
binding=json.loads((ROOT/'docs/reports/M8-xsd-corpus/verified-api-preparation.json').read_bytes())
identities={};changed={}
owners={
    'src/Xemele/Tree/Document.cs':'M9-dom-memory',
    'src/Xemele/Tree/SchemaSourceLocations/Document.SourceLocations.cs':'M9-dom-memory',
    'src/Xemele/XPath/Navigation/XPathNavigator.cs':'M9-xpath-order',
    'src/Xemele/XPath/Evaluation/Evaluator.cs':'M9-xpath-order'}
actual={str(p.relative_to(ROOT)) for p in (ROOT/'src/Xemele').rglob('*') if p.is_file() and p.suffix in ('.cs','.csproj','.props','.targets') and not {'bin','obj'}.intersection(p.relative_to(ROOT).parts)}
assert actual==set(binding['runtime_production_inputs'])
for name,previous in binding['runtime_production_inputs'].items():
    current=sha(ROOT/name);identities[name]=current
    if current!=previous:
        assert name in owners,name
        task=json.loads((ROOT/('docs/tasks/'+owners[name]+'.json')).read_bytes())
        assert task['status']=='verified' and name in task['owns']
        integrated=task['integration_revision']
        subprocess.run(['git','merge-base','--is-ancestor',integrated,'HEAD'],cwd=ROOT,check=True,timeout=10)
        assert hashlib.sha256(subprocess.check_output(['git','show',integrated+':'+name],cwd=ROOT)).hexdigest()==current
        changed[name]=dict(originalSha256=previous,currentSha256=current,verifiedOwner=task['id'],integrationRevision=integrated)
assert set(changed)==set(owners)
released.verified_dependencies();identities.update(released.verified_api_inputs())
dispositions=approved.approved_sources();assert len(dispositions)==155
catalog=json.loads((corpus/'resources.json').read_bytes())
for resource in catalog:
    name='tests/corpora/xsd/upstream/'+resource['path'];assert sha(ROOT/name)==resource['sha256'];identities[name]=resource['sha256']
for directory in [corpus,ROOT/'docs/reports/M8-xsd-corpus',ROOT/'tests/Xemele.Conformance.Tests/Xsd',ROOT/'tests/Xemele.Differential.Tests/Xsd']:
    for path in directory.iterdir():
        if path.is_file():identities[str(path.relative_to(ROOT))]=sha(path)
for name in ['Directory.Build.props','Directory.Packages.props','global.json','docs/tasks/M9-dom-memory.json','docs/tasks/M9-xpath-order.json']:
    identities[name]=sha(ROOT/name)
sourceManifest=dict(revision=revision,authorization='Active human M9 completion objective authorizes necessary ordinary fixed-corpus checks; explicit fuzz deferral remains in force.',
                    historicalBinding='docs/reports/M8-xsd-corpus/verified-api-preparation.json',historicalBindingSha256=sha(ROOT/'docs/reports/M8-xsd-corpus/verified-api-preparation.json'),
                    reviewedRuntimeChanges=changed,identities=identities,fuzzExecuted=False)
released.durable(out/'reviewed-current-inputs.json',sourceManifest)
env=dict(os.environ,DOTNET_ROOT=str(host.parent),DOTNET_ROLL_FORWARD='Disable',PYTHONDONTWRITEBYTECODE='1',
         DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0')
released.supervision.ROOT=ROOT;released.supervision.ARTIFACTS=out
report=dict(result='failed',revision=revision,upstreamRevision=released.PIN,commands=[],reviewedRuntimeChanges=changed,
            sourceManifestSha256=sha(out/'reviewed-current-inputs.json'),fuzzExecuted=False,originalPassCredit=0,gateCredit=0)
hard=time.monotonic()+3600;work=hard-10
def run(argv,cap):
    index=len(report['commands']);end=min(work,time.monotonic()+cap);argv=list(map(str,argv))
    receipt=released.nested.supervise_family_command(released.supervision,argv,index,env,end,min(hard,end+10))
    log=out/f'command-{index}.log';text=log.read_text(errors='replace')
    report['commands'].append(dict(argv=argv,receipt=receipt,outputSha256=sha(log),outputTail=text[-2000:]))
    print(text[-1000:],flush=True)
    assert released.supervision.workload_succeeded(receipt) and receipt['nestedOwnership']['allOwnedGroupsAbsent']
try:
    # Released preparation controls, with only a necessary fresh offline restore.
    project='tests/Xemele.Conformance.Tests/Xsd/Corpus.Runner.csproj'
    run([host,'restore',project,'--source','/private/tmp','--disable-build-servers','-p:RuntimeFrameworkVersion=10.0.11','-p:NuGetAudit=false'],180)
    for command in released.preparation_commands():run(command,300)
    for folder in [released.DLL.parent,ROOT/'src/Xemele/bin/Release/net10.0']:
        for path in folder.rglob('*'):
            if path.is_file():identities[str(path.relative_to(ROOT))]=sha(path)
    inventory=json.loads((corpus/'inventory.json').read_bytes())
    assert inventory['revision']==released.PIN and len(inventory['testSets'])==93
    cases=[];runs=out/'rows';runs.mkdir()
    for engine in ['product','bcl']:
        for testset in inventory['testSets']:
            journal=runs/(engine+'-'+testset.replace('/','_')+'.jsonl')
            expected=released.expected_rows(inventory,testset)
            cases.append(dict(engine=engine,testSet=testset,journal=journal,expected=expected,
                argv=[str(host),'--fx-version',released.RUNTIME,str(released.DLL),str(corpus/'upstream'),str(corpus/'inventory.json'),testset,engine,str(journal)]))
    assert sum(len(c['expected']) for c in cases)==83472
    execution=released.execute_cases(cases,runs,env,work,hard);report['execution']=execution
    assert execution['status']==0 and execution['allLaunchedChildrenClean']
    rows=[r for case in cases for r in released.inspect_rows(case)]
    original=[r for r in rows if r['engine']=='product' and r['classification']=='applicable' and not r['id'].endswith('/unicode16-supplemental') and r['passed'] is not True]
    # Exact outcome tuples must match every human disposition; never promote them.
    assert {r['id'] for r in original}==set(dispositions)
    assert all(approved.gate_disposition(r) for r in original)
    report['originalProductCounts']=dict(passed=sum(r['passed'] is True for r in rows if r['engine']=='product' and r['classification']=='applicable' and not r['id'].endswith('/unicode16-supplemental')),
        failed=sum(r['passed'] is False for r in original),indeterminate=sum(r['passed'] is None for r in original))
    assert report['originalProductCounts']==dict(passed=39329,failed=144,indeterminate=11)
    annotation=[r for r in rows if r['engine']=='product' and r['id']=='msMeta/Annotations_w3c.xml#g78/s:annotZ002']
    assert len(annotation)==1 and annotation[0]['passed'] is True
    report.update(result='passed',exactDispositions=155,annotationPassed=True,rowsInspected=len(rows),
                  fixtureJournalSha256={str(c['journal'].relative_to(ROOT)):sha(c['journal']) for c in cases})
finally:
    report['identityChanges']=[name for name,value in identities.items() if not (ROOT/name).is_file() or sha(ROOT/name)!=value]
    report['totalDeadlineExceeded']=time.monotonic()>hard
    if report['identityChanges'] or report['totalDeadlineExceeded']:report['result']='failed'
    (HERE/'current-xsd.json').write_text(json.dumps(report,indent=2)+'\n')
