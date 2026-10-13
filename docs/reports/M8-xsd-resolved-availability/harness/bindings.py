"""Reviewed source/tool/project/input/receipt bindings. No work occurs on import."""
import hashlib
import json
import os
import shutil
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
REPORT = HERE.parent
OUT = ROOT / 'artifacts/M8-xsd-resolved-availability'
SOURCE = REPORT / 'harness-source.json'
REVIEW = REPORT / 'harness-review.json'  # Coordinator-owned; never synthesized.
DOTNET = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
FLAGS = ['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
NUGET = Path('/Users/sebastienros/.nuget/packages')
GIT = Path('/usr/bin/git')
PS = Path('/bin/ps')


def digest(path):
    with Path(path).open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()


def key(path):
    p = Path(path).resolve()
    return str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p)


def resolve(name):
    p = Path(name)
    return p if p.is_absolute() else ROOT / p


def identity_sha(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()


def verify_role(role):
    expected=json.loads(SOURCE.read_text())['executableRoles'][role]
    fixed={'git':GIT,'ps':PS}[role].resolve()
    assert resolve(expected['path']).resolve()==fixed and digest(fixed)==expected['sha256'],'Frozen executable role changed'
    return fixed


def verify_executable(argv,environment):
    for role,fixed in [('git',GIT),('ps',PS)]:
        if Path(argv[0]).name==fixed.name:
            expected=verify_role(role);selected=shutil.which(argv[0],path=environment.get('PATH'))
            assert selected and Path(selected).resolve()==expected,'PATH selected a different frozen executable'


def identities(paths):
    return {key(p): digest(p) for p in sorted(set(Path(p).resolve() for p in paths))}


def directory_inputs(folders):
    files = []
    for folder in folders:
        p = Path(folder).resolve(); assert p.is_dir() and not p.is_symlink()
        for file in p.rglob('*'):
            assert not file.is_symlink(), 'Symlinked active/toolchain input refused'
            if file.is_file(): files.append(file)
    return identities(files)


def mismatches(expected):
    bad = []
    for name, sha in expected.items():
        try:
            if digest(resolve(name)) != sha: bad.append(name)
        except OSError: bad.append(name)
    return bad


def api_inputs():
    return identities(ROOT.joinpath('docs/api').glob('*.txt'))


def source_members():
    return sorted(key(p) for folder in ['src/Xemele','tests/Xemele.Tests','tests/Xemele.TestUtilities','benchmarks/Xemele.Benchmarks',
        'docs/reports/M0-contracts/ApiCheck','docs/reports/M8-contracts/ApiCheck','samples/M8-xsd-resolved-availability',str(HERE)]
        for p in ROOT.joinpath(folder).rglob('*') if p.is_file() and not any(part in ['bin','obj'] for part in p.parts)
        and p.suffix in ['.cs','.csproj','.props','.targets','.fixture','.json'])


def review(expected, action=None):
    assert digest(REVIEW) == expected, 'Coordinator SOURCE review SHA changed'
    row = json.loads(REVIEW.read_text()); source = json.loads(SOURCE.read_text())
    assert row['result'] == 'SOURCE PASS' and row['harnessSourceSha256'] == digest(SOURCE)
    selected=action or os.environ.get('XEMELE_MISSING_ACTION')
    assert selected is None or selected in row['allowedActions'],'Coordinator did not release this execution phase'
    assert row['runtimeSourceManifestSha256'] == source['runtimeSourceManifestSha256']
    assert row['runtimeSourceCommit'] == source['runtimeSourceCommit']
    assert not mismatches(source['identities']), 'Reviewed source or toolchain changed'
    assert digest(REPORT/'source-manifest.json')==source['runtimeSourceManifestSha256']
    assert api_inputs() == source['apiInputs'], 'Whole public-fragment membership changed'
    assert source_members() == source['sourceMembers'], 'Whole compile-input membership changed'
    assert sorted(key(p) for p in HERE.rglob('*') if p.is_file())==source['harnessMembers'],'Harness membership changed'
    assert {name:resolve(name).is_file() for name in source['configurationPresence']}==source['configurationPresence'],'Build configuration presence changed'
    assert directory_inputs([resolve(p) for p in source['toolchainDirectories']]) == source['toolchainDirectoryInputs']
    assert directory_inputs([resolve(p) for p in source['packageDirectories']]) == source['packageInputs']
    assert identity_sha(source['commandRecipes']) == source['commandRecipesSha256']
    assert row['pythonExecutableSha256'] == digest(sys.executable)
    assert time.monotonic() < float(os.environ.get('XEMELE_MISSING_WORK_DEADLINE','inf'))
    return source


def environment(revision):
    env = os.environ.copy()
    env.update(PYTHONDONTWRITEBYTECODE='1',DOTNET_TieredCompilation='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0',
               XEMELE_MISSING_SOURCE_REVISION=revision,XEMELE_RESOLVED_PHASE=json.loads(SOURCE.read_text())['phase'],NuGetAudit='false',RuntimeFrameworkVersion='10.0.11',UseSharedCompilation='false',
               NUGET_PACKAGES=str(NUGET),DOTNET_ROOT=str(DOTNET.parent),DOTNET_MULTILEVEL_LOOKUP='0',DOTNET_ROLL_FORWARD='Disable',
               MSBuildSDKsPath=str(DOTNET.parent/'sdk/10.0.400/Sdks'))
    return env


def owned(path):
    p = Path(path).resolve()
    assert OUT.resolve() in p.parents and not p.is_symlink(), 'Exact owned artifact path required'
    return p


def active_flags(active):
    p = owned(active)
    return ['-p:UseArtifactsOutput=true','-p:ArtifactsPath='+str(p),'-p:ArtifactsPivots=release',
            '-p:OutputPath='+str(p/'bin')+'/', '-p:AppendTargetFrameworkToOutputPath=false','-p:AppendRuntimeIdentifierToOutputPath=false']


def build_commands(project, active):
    return [[str(DOTNET),'restore',project,'--source',str(NUGET),*FLAGS,*active_flags(active)],
            [str(DOTNET),'build',project,'-c','Release','--no-restore',*FLAGS,*active_flags(active)]]


def restore_inputs(argv):
    """Bind generated assets and fail closed on a package outside source review."""
    if argv[0]!=str(DOTNET) or argv[1:2] not in [['restore'],['build']]:return {}
    active=next(owned(a.split('=',1)[1]) for a in argv if a.startswith('-p:ArtifactsPath='))
    source=json.loads(SOURCE.read_text());paths=[]
    for path in (active/'obj').rglob('*'):
        if not path.is_file():continue
        if path.name=='project.assets.json':
            assets=json.loads(path.read_text());paths.append(path)
            assert all(Path(folder).resolve()==NUGET.resolve() for folder in assets['packageFolders']),'Unexpected package folder'
            packages=[coordinate.lower() for coordinate,row in assets['libraries'].items() if row['type']=='package']
            assert set(packages)<=set(source['packageCoordinates']),'Unreviewed resolved package version'
        elif path.name.endswith(('.nuget.g.props','.nuget.g.targets')):paths.append(path)
    if argv[1]=='build':assert any(p.name=='project.assets.json' for p in paths),'Build requires bound restore assets'
    return identities(paths)


def restore_scope(argv):
    if argv[0]!=str(DOTNET) or argv[1:2] not in [['restore'],['build']]:return None
    active=next(owned(a.split('=',1)[1]) for a in argv if a.startswith('-p:ArtifactsPath='))
    return dict(project=key(resolve(argv[2])),activeRoot=key(active))


def preceding_restore(directory,argv,current):
    if argv[0]!=str(DOTNET) or argv[1:2]!=['build']:return None
    assert '--no-restore' in argv,'Only the reviewed no-restore build recipe is allowed'
    scope=restore_scope(argv);candidate=None
    from family import clean
    for path in sorted(Path(directory).glob('lifecycle-*.json')):
        row=json.loads(path.read_text())
        if row['argv'][0:2]==[str(DOTNET),'restore'] and row.get('restoreScope')==scope:candidate=(path,row)
    assert candidate is not None,'No preceding same-project/same-active-root restore'
    path,row=candidate
    assert clean(row),'Preceding restore did not complete successfully'
    assert current==row['afterRestoreInputs'],'Restore outputs changed before no-restore build'
    return dict(path=key(path),sha256=digest(path),scope=scope,restoreInputsSha256=identity_sha(current))


def execution_inputs(argv):
    dlls = [resolve(a) for a in argv if str(a).endswith('.dll')]
    if not dlls: return {}, []
    folders = sorted({p.parent.resolve() for p in dlls} | {DOTNET.parent/'shared/Microsoft.NETCore.App/10.0.11',DOTNET.parent/'host/fxr'})
    assert all(p.is_file() for p in dlls)
    values = directory_inputs(folders); values.update(identities([DOTNET,sys.executable]))
    return values, [key(p) for p in folders]


def assert_prior_execution(directory):
    for p in Path(directory).glob('binary-inputs-*.json'):
        row = json.loads(p.read_text())
        now = directory_inputs([resolve(n) for n in row['folders']]); now.update(identities([DOTNET,sys.executable]))
        assert now == row['actualInputs'], 'Previously executed binary bytes or membership changed'


def record_execution(directory,index,argv,before,after,folders):
    from family import nested
    assert before == after and not mismatches(before), 'Execution inputs changed'
    assert_prior_execution(directory)
    nested.durable(Path(directory)/f'binary-inputs-{index:03d}.json',dict(argv=list(argv),actualInputs=before,folders=folders,
        beforeInputsSha256=identity_sha(before),afterInputsSha256=identity_sha(after)))


def probe_commands(output,probe):
    output = owned(output); probe = owned(probe)
    return [[str(DOTNET),'--fx-version','10.0.11',str(probe),mode,str(output/name)]
            for mode,name in [('--disabled','disabled.json'),('--measure','allocations.json')]]


def binary_identities(probe):
    p = owned(probe); assert p.is_file() and (p.parent/'Xemele.dll').is_file()
    values,folders = execution_inputs([str(DOTNET),'--fx-version','10.0.11',str(p)])
    assert key(p.with_suffix('.deps.json')) in values and key(p.with_suffix('.runtimeconfig.json')) in values
    return values, folders


# Legacy 11-route synthetic schema retained only by inherited controls; never an actual phase verifier.
def verify_probe(path,probe,measure=False,revision=None):
    row = json.loads(owned(path).read_text())
    assert row['mode'] == ('--measure' if measure else '--disabled') and row['countersEnabled'] is measure
    assert row['status']=='passed' and row['runtime']=='.NET 10.0.11' and row['serverGc'] is False
    assert row['productSha256']==digest(Path(probe).parent/'Xemele.dll') and row['probeSha256']==digest(probe)
    if revision is not None: assert row['sourceRevision']==revision
    assert row['retainedResults']==352 and row['retainedSchemas']==8 and len(row['rows'])==11
    assert row['consumed']==fixture_consumption() and row['inspections']==13632
    assert row['heapDelta']==row['heapAfter']-row['heapBefore']
    route_names=['compile-complete','compile-explicit','compile-inferred','compile-list','compile-deferred-program','validate-unrelated','validate-required','validate-xsi-program','availability-resolved','availability-missing-final','availability-owner-absent']
    for i,item in enumerate(row['rows']):
        assert item['name']==route_names[i]
        assert item['route']==i and item['operations']==32 and len(item['distribution'])==32
        if measure:
            assert item['threadBytes']==sum(item['distribution'])
            assert item['threadBytes']==0 if i>=8 else item['threadBytes']>0
            assert all(value==0 if i>=8 else value>0 for value in item['distribution'])
            assert item['firstThreadBytes']>=0 and item['firstGlobalBytes']>=0 and item['globalBytes']>=item['threadBytes']
        else:
            assert all(item[n]==0 for n in ['firstThreadBytes','firstGlobalBytes','threadBytes','globalBytes'])
            assert not any(item['distribution'])
    if measure:
        assert row['positiveThreadBytes']>=4096 and row['positiveGlobalBytes']>=4096
        assert row['coldThreadBytes']>=0 and row['coldGlobalBytes']>=row['coldThreadBytes']
    else: assert all(row[n]==0 for n in ['coldThreadBytes','coldGlobalBytes','positiveThreadBytes','positiveGlobalBytes','heapBefore','heapAfter','heapDelta'])
    return row


def fixture_consumption():
    # Independent fixture oracle: compile/result routes each increment1; missing
    # availability consumes reference63; 1 first +8 warm +32 retained each route,
    # plus8 retained compile calls. No counters are invoked here.
    return 41 * (10 + 63) + 8


def write_api_union(path):
    rows = []; seen = set()
    for p in sorted(ROOT.joinpath('docs/api').glob('*.txt')):
        lines=p.read_text().splitlines(); assert lines[0]=='xemele-api-v1'
        for line in lines[1:]:
            row=json.loads(line); assert set(row)=={'id','member','declaration'}
            assert line==json.dumps(row,sort_keys=True,ensure_ascii=False,separators=(',',':'))
            assert row['member'] not in seen;seen.add(row['member']);rows.append(dict(row,id=row['member']))
    assert len(rows)==1430, 'Public1430 union changed'
    Path(path).write_text('xemele-api-v1\n'+''.join(json.dumps(r,sort_keys=True,ensure_ascii=False,separators=(',',':'))+'\n' for r in sorted(rows,key=lambda r:r['member'])))


def rows(path):
    lines=Path(path).read_text().splitlines();assert lines[0]=='xemele-api-v1'
    values={}
    for line in lines[1:]:
        value=json.loads(line);assert value['id'] not in values; values[value['id']]=value
    return values


def exact_projection(actual,expected):
    names=[n[2:] for n in expected if n.startswith('T:')]
    selected={name:row for name,row in actual.items() if name in expected or any(name[2:].startswith(n+'.') for n in names)}
    assert selected==expected,'Frozen named-type member/signature/nullability projection changed'


def positive_controls(path,expected,review_sha):
    release=verify_release(path,expected,'controls',review_sha)
    payload=json.loads(Path(path).read_text());assert payload['completed'] and payload['testsRun']==CONTROL_COUNT and not payload['counterEnabled']
    return release


CONTROL_COUNT = 15


def verify_release(path,expected,action,review_sha):
    path=owned(path);assert digest(path)==expected
    payload=json.loads(path.read_text());controller=owned(payload['controller'])
    release_path=controller/'controller-release.json';release=json.loads(release_path.read_text())
    assert release['completed'] and release['action']==action and release['reviewSha256']==review_sha
    assert release['sourceSha256']==digest(SOURCE) and release['payloads'].get(key(path))==expected
    assert not (controller/'controller-release.json.deadline-failure.json').exists()
    for kind in ['family','terminal']:
        p=owned(release[kind+'Receipt']);assert digest(p)==release[kind+'Sha256'] and json.loads(p.read_text())['completed']
    terminal=json.loads(Path(release['terminalReceipt']).read_text())
    assert all(not resolve(name).exists() for name in terminal['removedIdentities'])
    assert all(not resolve(name).exists() for name in terminal['removedDirectories'])
    family=json.loads(Path(release['familyReceipt']).read_text())
    assert family['ownership']['allOwnedGroupsAbsent'] and family['sequence']
    assert family['finishedMonotonic']<family['hardDeadline'] and family['hardDeadline']<=family['startedMonotonic']+family['maximumSeconds']
    assert all(r['finishedMonotonic']<r['hardDeadline'] and r['hardDeadline']<=r['startedMonotonic']+r['childHardSeconds'] for r in family['sequence'])
    assert all(r['reason']=='completed' and r['exit']==0 and r['directChildReaped'] and r['ownedGroupAbsent'] and r['logClosed'] and r['nestedOwnership']['allOwnedGroupsAbsent'] for r in family['sequence'])
    assert terminal['familyOutcome'] and terminal['outerError'] is None
    from terminal import verify_membership
    extra=[Path(release['terminalReceipt']),release_path]
    if action=='prepare' and (path.parent/'consumed.json').exists():
        marker=path.parent/'consumed.json';claim=json.loads(marker.read_text())
        assert claim==dict(preparationSha256=expected,controller=os.environ['XEMELE_MISSING_RUN_DIRECTORY'],state='claimed; never reusable')
        extra.append(marker)
    verify_membership(terminal,extra)
    return release


def verify_prepared(path,expected,review_sha,allow_claimed=False):
    path=owned(path);assert path.name=='prepared.json' and digest(path)==expected
    row=json.loads(path.read_text());source=review(review_sha)
    assert row['sourceSha256']==digest(SOURCE) and row['runtimeSourceCommit']==source['runtimeSourceCommit']
    assert row['reviewSha256']==review_sha
    assert allow_claimed or not (path.parent/'consumed.json').exists(),'Preparation already claimed'
    verify_release(path,expected,'prepare',review_sha)
    positive_controls(row['positiveReceipt'],row['positiveSha256'],review_sha)
    verify_release(row['checksReceipt'],row['checksSha256'],'check',review_sha)
    assert digest(row['preparationFamilyReceipt'])==row['preparationFamilySha256']
    assert json.loads(Path(row['preparationFamilyReceipt']).read_text())['completed']
    assert (row['maximumSeconds'],row['workSeconds'],row['cleanupReserveSeconds'])==(120,90,30)
    assert row['disabledReceiptSha256']==digest(row['disabledReceipt'])
    disabled=json.loads(Path(row['disabledReceipt']).read_text());assert disabled['completed'] and not disabled['countersEnabled']
    assert row['commands']==probe_commands(row['measurementDirectory'],row['probePath'])
    verify_active_prepared(row)
    return row,source


def verify_active_prepared(row):
    now,folders=binary_identities(row['probePath'])
    assert now==row['binaryInputs'] and folders==row['binaryFolders']
    assert directory_inputs([owned(row['activeOutputRoot'])])==row['activeOutputIdentities'],'Active prepared inventory changed'
    assert not mismatches(row['identities'])


def claim_prepared(path,expected,review_sha):
    from family import nested
    row,source=verify_prepared(path,expected,review_sha)
    nested.durable(Path(path).parent/'consumed.json',dict(preparationSha256=expected,controller=os.environ['XEMELE_MISSING_RUN_DIRECTORY'],state='claimed; never reusable'))
    return row,source


def verify_resolved_probe(path,probe,measure=False,revision=None):
    row=json.loads(owned(path).read_text());source=json.loads(SOURCE.read_text())
    assert row['mode']==('--measure' if measure else '--disabled') and row['countersEnabled'] is measure
    assert row['phase']==source['phase'] and row['status']=='passed' and row['runtime']=='.NET 10.0.11' and row['serverGc'] is False
    assert row['productSha256']==digest(Path(probe).parent/'Xemele.dll') and row['probeSha256']==digest(probe)
    if revision is not None:assert row['sourceRevision']==revision
    assert row['originalSha256']=='fff58be90b04ace4117786f19d8a450ed81b0e074f9c67d9316a3275a028b6cd'
    assert row['originalOutcome']==('SCHEMA.Limit' if source['phase']=='baseline' else 'VALID')
    assert row['retainedResults']==192 and row['retainedSchemas']==8 and row['consumed']==254 and len(row['rows'])==6
    assert row['heapDelta']==row['heapAfter']-row['heapBefore']
    for i,item in enumerate(row['rows']):
        assert item['route']==i and item['name']==['query-8','query-32','query-64','compile-8','compile-32','compile-64'][i]
        assert item['size']==[8,32,64][i%3] and item['operations']==32 and len(item['distribution'])==32
        if i<3:
            assert item['chargedWork']==0 and item['chargedComponents']==0 if source['phase']=='proposed' else item['chargedWork']>0 and item['chargedComponents']>0
        if measure:
            assert item['threadBytes']==sum(item['distribution']) and item['globalBytes']>=item['threadBytes']
            assert all(v==0 for v in item['distribution']) if i<3 and source['phase']=='proposed' else all(v>0 for v in item['distribution'])
        else:
            assert all(item[n]==0 for n in ['firstThreadBytes','firstGlobalBytes','threadBytes','globalBytes']) and not any(item['distribution'])
    if measure:assert row['positiveThreadBytes']>=4096 and row['positiveGlobalBytes']>=4096
    else:assert all(row[n]==0 for n in ['coldThreadBytes','coldGlobalBytes','positiveThreadBytes','positiveGlobalBytes','heapBefore','heapAfter','heapDelta'])
    return row


def allocation_summary(path,row):
    import statistics
    data=verify_resolved_probe(path,row['probePath'],True,row['runtimeSourceCommit'])
    return dict(completed=True,rawSha256=digest(path),runtime=data['runtime'],architecture=data['architecture'],os=data['os'],serverGc=data['serverGc'],
        runtimeSourceCommit=row['runtimeSourceCommit'],phase=data['phase'],coldThreadBytes=data['coldThreadBytes'],coldGlobalBytes=data['coldGlobalBytes'],
        positiveThreadBytes=data['positiveThreadBytes'],positiveGlobalBytes=data['positiveGlobalBytes'],consumed=data['consumed'],
        retainedResults=192,retainedSchemas=8,heapBefore=data['heapBefore'],heapAfter=data['heapAfter'],heapDelta=data['heapDelta'],originalOutcome=data['originalOutcome'],originalSha256=data['originalSha256'],
        rows=[dict(route=r['route'],name=r['name'],size=r['size'],operations=32,firstThreadBytes=r['firstThreadBytes'],firstGlobalBytes=r['firstGlobalBytes'],
                   threadBytes=r['threadBytes'],globalBytes=r['globalBytes'],minimumBytes=min(r['distribution']),medianBytes=statistics.median(r['distribution']),
                   maximumBytes=max(r['distribution']),meanBytes=statistics.mean(r['distribution']),chargedWork=r['chargedWork'],chargedComponents=r['chargedComponents']) for r in data['rows']],
        limitation='Allocation-only; fixture initialization/first operation/JIT warmup8/warm32/retained schemas8; no timing/host-isolation/static-initialization claim; original compiled outside measured intervals')


def recovery_review(review_sha):
    assert digest(REVIEW)==review_sha
    row=json.loads(REVIEW.read_text());source=json.loads(SOURCE.read_text())
    assert row['result']=='SOURCE PASS' and row['harnessSourceSha256']==digest(SOURCE)
    assert 'recovery' in row['allowedActions'] or os.environ.get('XEMELE_MISSING_ACTION')=='controls'
    harness={name:sha for name,sha in source['identities'].items() if resolve(name).is_relative_to(HERE)}
    assert harness and not mismatches(harness) and digest(sys.executable)==row['pythonExecutableSha256']
    return source
