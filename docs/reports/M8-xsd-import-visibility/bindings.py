"""Hash/source/command bindings only; imports launch no child and enable no counter."""
import hashlib
import json
import os
import sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
OUT = ROOT / 'artifacts/M8-xsd-import-visibility'
DOTNET = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
FLAGS = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
SOURCE = HERE / 'harness-source.json'
API_ROOT = ROOT / 'docs/api'
REVIEW = HERE / 'harness-review.json'  # written solely by coordinator, never synthesized here


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def key(path):
    path = Path(path).resolve()
    return str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path)


def mismatches(identities):
    wrong = []
    for name, expected in identities.items():
        path = Path(name); path = path if path.is_absolute() else ROOT / path
        try:
            if digest(path) != expected: wrong.append(name)
        except OSError:
            wrong.append(name)
    return wrong


def review(expected_hash):
    assert digest(REVIEW) == expected_hash, 'Coordinator review identity changed'
    record = json.loads(REVIEW.read_text())
    assert record['result'] == 'SOURCE PASS' and record['harnessSourceSha256'] == digest(SOURCE)
    source = json.loads(SOURCE.read_text())
    assert source['runtimeControlCommit'] == '8cb31895a01fccf945dc2b2ff5a58ef39fd14fab'
    assert api_inputs() == source['apiInputs'], 'Full API membership/hash changed'
    assert not mismatches(source['identities']), 'Reviewed source/dependency/command identity changed'
    return source


def environment(source_revision):
    env = os.environ.copy()
    env.update(PYTHONDONTWRITEBYTECODE='1', DOTNET_TieredCompilation='0', DOTNET_gcServer='0',
               DOTNET_GCConcurrent='0', XEMELE_IMPORT_REVISION=source_revision,
               NuGetAudit='false', RuntimeFrameworkVersion='10.0.11', UseSharedCompilation='false')
    return env


def commands(output, probe):
    output = Path(output).resolve();probe=Path(probe).resolve()
    assert OUT.resolve() in output.parents and OUT.resolve() in probe.parents
    return [[str(DOTNET), '--fx-version', '10.0.11', str(probe), mode, str(output / name)]
            for mode, name in [('--control', 'control.json'), ('--measure', 'allocations.json')]]


def build_commands(active):
    project = 'benchmarks/Xemele.Benchmarks/M8-xsd-import-visibility/Import.Probe.csproj'
    return [[str(DOTNET), 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *FLAGS,*active_flags(active)],
            [str(DOTNET), 'build', project, '-c', 'Release', '--no-restore', *FLAGS,*active_flags(active)]]


def binary_identities(probe):
    probe=Path(probe).resolve()
    paths = {DOTNET.resolve(), Path(sys.executable).resolve(), probe,probe.parent/'Xemele.dll'}
    for folder in [probe.parent, DOTNET.parent / 'shared/Microsoft.NETCore.App/10.0.11', DOTNET.parent / 'host/fxr']:
        assert folder.is_dir(), 'Required prepared binary directory missing'
        for path in folder.rglob('*'):
            if path.is_file(): paths.add(path.resolve())
    assert (probe.parent / 'Xemele.dll').resolve() in paths, 'Actual probe product dependency must be pinned'
    assert probe.with_suffix('.deps.json') in paths and probe.with_suffix('.runtimeconfig.json') in paths
    return {key(path): digest(path) for path in sorted(paths)}


def verify_control(path, probe):
    probe=Path(probe).resolve()
    data = json.loads(Path(path).read_text())
    assert data['mode'] == '--control' and data['status'] == 'passed' and data['runtime'] == '.NET 10.0.11'
    assert len(data['rows']) == 24 and data['consumed'] == 1012 and data['retainedResults'] == 28
    assert data['coldInputsBytes'] == data['positiveBytes'] == 0 and len(data['heap']) == 7
    assert all(row['bytes'] == row['firstRunBytes'] == 0 and row['operations'] == 32 and len(row['distribution']) == 32 and not any(row['distribution']) for row in data['rows'])
    assert all(row['before'] == row['after'] == row['delta'] == 0 and row['count'] == 4 for row in data['heap'])
    assert [r['route'] for r in data['rows']] == list(range(24))
    assert data['assemblySha256'] == digest(probe.parent / 'Xemele.dll') and data['probeSha256'] == digest(probe)
    return data


def publish_success(path, runner, checks):
    import time
    from family import nested
    assert checks and all(value == 'passed' for value in checks.values()), 'Failed checks cannot publish success'
    assert runner.closed and runner.receipt and runner.receipt['completed'], 'Family cleanup must precede success'
    assert not runner.stopped and all(__import__('family').clean(row) for row in runner.sequence)
    assert time.monotonic() < runner.hard - 1, 'No late success publication'
    evidence = correctness_evidence(runner.directory)
    verify_correctness_evidence(evidence)
    from family import publish_finite
    publish_finite(Path(path), {'status': 'passed', 'completed': True, 'checks': checks,
        'harnessSourceSha256': digest(SOURCE), 'evidence': evidence,
        'familyReceipt': str(runner.directory / 'family-cleanup.json'),
        'familySha256': digest(runner.directory / 'family-cleanup.json')}, runner.hard)


def verify_prepared(path, expected, review_sha):
    path = Path(path).resolve()
    assert OUT.resolve() in path.parents and path.name == 'prepared.json'
    assert digest(path) == expected, 'Reviewed preparation manifest changed'
    assert not (path.parent/'consumed.json').exists(),'Prepared binaries already claimed; no reuse'
    manifest = json.loads(path.read_text())
    assert manifest['coordinatorReviewSha256'] == review_sha
    source = review(review_sha)
    assert manifest['harnessSourceSha256'] == digest(SOURCE)
    assert all(manifest['identities'].get(name) == value for name, value in source['identities'].items()), 'Whole reviewed source/dependency identity coverage required'
    probe=Path(manifest['probePath']).resolve()
    assert OUT.resolve() in probe.parents
    assert manifest['commands'] == commands(Path(manifest['directory']),probe), 'Exact required counter/disabled commands changed'
    assert (manifest['maximumSeconds'], manifest['workSeconds'], manifest['cleanupReserveSeconds']) == (120, 100, 20)
    assert key(probe) in manifest['identities'] and key(probe.parent / 'Xemele.dll') in manifest['identities'], 'Runner and actual product dependency required'
    assert all(manifest['identities'].get(name)==value for name,value in manifest['binaryInputs'].items()),'Whole actual binary input coverage required'
    verify_active_prepared(manifest)
    verify_controller(path, expected, 'prepare', review_sha)
    receipt = Path(manifest['preparationReceipt'])
    assert OUT.resolve() in receipt.resolve().parents
    assert manifest['identities'].get(key(receipt)) == digest(receipt) and json.loads(receipt.read_text())['completed']
    return manifest, source


def api_inputs():
    return {key(path): digest(path) for path in sorted(API_ROOT.glob('*.txt')) if path.is_file()}


def verify_controller(payload, expected, action, review_sha):
    payload = Path(payload).resolve()
    assert OUT.resolve() in payload.parents and digest(payload) == expected
    if action=='measure':
        release_path=Path(json.loads(payload.read_text())['controllerReceipt']).resolve()
        assert OUT.resolve() in release_path.parents and release_path.name=='controller-release.json'
        controller=release_path.parent
    else:
        controller=payload.parent if action in ['controls','discard'] else payload.parents[1 if action=='prepare' else 2]
        release_path=controller/'controller-release.json'
    assert not (controller/'preparation-abort.json').exists(),'Aborted preparation cannot be accepted'
    release = json.loads(release_path.read_text())
    assert release['completed'] and release['action'] == action and release['coordinatorReviewSha256'] == review_sha
    assert release['harnessSourceSha256'] == digest(SOURCE) and release['payloads'].get(key(payload)) == expected
    assert not release_path.with_name(release_path.name + '.deadline-failure.json').exists()
    family = Path(release['familyReceipt']).resolve()
    assert family == controller / 'family-cleanup.json' and digest(family) == release['familySha256']
    receipt = json.loads(family.read_text())
    assert receipt['completed'] and receipt['ownership']['allOwnedGroupsAbsent']
    assert receipt['sequence'] and all(__import__('family').clean(row) for row in receipt['sequence'])
    read_terminal(release)
    return release


def verify_positive_controls(path, expected, review_sha):
    path = Path(path).resolve()
    assert path.name == 'positive-controls-release.json'
    verify_controller(path, expected, 'controls', review_sha)
    result = json.loads(path.read_text())
    assert result['completed'] and not result['counterEnabled'] and result['sourceSha256'] == digest(SOURCE)
    assert result['coordinatorReviewSha256'] == review_sha
    for name in ['semantic', 'family']:
        receipt = Path(result[name + 'Receipt']).resolve()
        assert OUT.resolve() in receipt.parents and digest(receipt) == result[name + 'Sha256']
        assert json.loads(receipt.read_text())['completed']
    return result


def execution_inputs(argv):
    folders = set()
    dlls = [Path(arg) for arg in argv if arg.endswith('.dll')]
    if not dlls:
        return {}, []
    for dll in dlls:
        absolute = dll if dll.is_absolute() else ROOT / dll
        assert absolute.is_file()
        folders.add(absolute.parent.resolve())
    folders.update([DOTNET.parent / 'shared/Microsoft.NETCore.App/10.0.11', DOTNET.parent / 'host/fxr'])
    identities = {key(DOTNET): digest(DOTNET), key(sys.executable): digest(sys.executable)}
    for folder in folders:
        assert folder.is_dir(), 'Actual binary dependency directory missing'
        for path in folder.rglob('*'):
            if path.is_file(): identities[key(path)] = digest(path)
    return identities, sorted(key(folder) for folder in folders)


def assert_prior_execution(directory):
    for path in sorted(Path(directory).glob('binary-inputs-*.json')):
        row=json.loads(path.read_text())
        assert not mismatches(row['actualInputs']), 'Previously executed binary changed; rebuild overlap forbidden'
        members={key(p) for folder in row['folders'] for p in resolve(folder).rglob('*') if p.is_file()}
        expected={name for name in row['actualInputs'] if any(resolve(name).is_relative_to(resolve(folder)) for folder in row['folders'])}
        assert members==expected,'Previously executed dependency membership changed'


def record_execution(directory, index, argv, before, after, folders):
    from family import nested
    assert before==after and not mismatches(before),'Actual execution inputs changed'
    assert_prior_execution(directory)
    nested.durable(Path(directory) / f'binary-inputs-{index:03d}.json',
        {'argv':list(argv),'actualInputs':before,'folders':folders,
         'beforeInputsSha256':identity_sha(before),'afterInputsSha256':identity_sha(after)})


def correctness_evidence(directory):
    directory = Path(directory)
    records = sorted(directory.glob('binary-inputs-*.json'))
    assert records, 'Fresh actual test/runtime/dependency inputs required'
    runs = []
    for path in records:
        row = json.loads(path.read_text())
        index=int(path.stem.rsplit('-',1)[1])
        lifecycle=directory/f'lifecycle-{index:03d}.json'
        actual=json.loads(lifecycle.read_text())
        assert actual['argv']==row['argv'] and __import__('family').clean(actual), 'Fresh execution receipt required'
        runs.append(dict(row,sequenceIndex=index,receiptPath=str(path),receiptSha256=digest(path),
                         lifecyclePath=str(lifecycle),lifecycleSha256=digest(lifecycle)))
    results = directory / 'results'
    inputs = {key(p): digest(p) for p in results.iterdir() if p.is_file() and p.name != 'checks-completed.json'}
    assert all(key(results / name) in inputs for name in ['correctness.xml', 'resolver.xml', 'datatypes.xml', 'control.json', 'internal.txt', 'public.txt'])
    return {'apiInputs': api_inputs(), 'binaryRuns': runs, 'resultDirectory': str(results), 'resultIdentities': inputs}


def verify_correctness_evidence(evidence, terminal=None):
    assert evidence['apiInputs'] == api_inputs(), 'API inputs changed/deleted/added'
    latest = {}
    folders = set()
    for row in evidence['binaryRuns']:
        assert digest(row['receiptPath']) == row['receiptSha256']
        assert digest(row['lifecyclePath'])==row['lifecycleSha256']
        actual=json.loads(Path(row['lifecyclePath']).read_text())
        assert actual['argv']==row['argv'] and __import__('family').clean(actual)
        assert row['beforeInputsSha256']==row['afterInputsSha256']==identity_sha(row['actualInputs'])
        assert all(name not in latest or latest[name]==value for name,value in row['actualInputs'].items()),'Overlapping rebuild invalidates earlier execution'
        latest.update(row['actualInputs']); folders.update(row['folders'])
    verify_live_or_removed(latest,terminal)
    # Membership is bound as well as bytes: no newly injected or deleted dependency.
    expected_members = {name for name in latest if any(Path(name if Path(name).is_absolute() else ROOT/name).is_relative_to(Path(folder if Path(folder).is_absolute() else ROOT/folder)) for folder in folders)}
    actual_members = {key(p) for folder in folders for p in Path(folder if Path(folder).is_absolute() else ROOT/folder).rglob('*') if p.is_file()}
    removed=set() if terminal is None else set(terminal['removedIdentities'])
    assert actual_members == expected_members-removed, 'Actual binary dependency membership changed'
    directory = Path(evidence['resultDirectory'])
    actual_results = {key(p): digest(p) for p in directory.iterdir() if p.is_file() and p.name != 'checks-completed.json'}
    removed={} if terminal is None else terminal['removedIdentities']
    assert actual_results == {name:value for name,value in evidence['resultIdentities'].items() if name not in removed}, 'Fresh correctness result membership/hash changed'
    verify_live_or_removed(evidence['resultIdentities'],terminal)


def verify_checks_success(path, expected, review_sha):
    verify_controller(path, expected, 'check', review_sha)
    data = json.loads(Path(path).read_text())
    assert data['completed'] and data['status'] == 'passed' and all(v == 'passed' for v in data['checks'].values())
    assert data['harnessSourceSha256'] == digest(SOURCE)
    assert digest(data['familyReceipt']) == data['familySha256'] and json.loads(Path(data['familyReceipt']).read_text())['completed']
    controller=verify_controller(path,expected,'check',review_sha)
    cleanup=read_terminal(controller)
    verify_correctness_evidence(data['evidence'],cleanup)
    return data


def publish_controller(runner, action, payloads, review_sha):
    # Preparation acceptance leaves ten seconds inside the total hard bound
    # for revocation/purge if publication fails or crosses its earlier bound.
    try:
        from family import publish_finite
        assert runner.closed and runner.receipt and runner.receipt['completed'] and not runner.stopped
        assert all(__import__('family').clean(row) for row in runner.sequence)
        assert not mismatches(payloads), 'Child candidate changed before enclosing acceptance'
        family = runner.directory / 'family-cleanup.json'
        assert json.loads(family.read_text())['completed']
        terminal=runner.directory/'terminal-cleanup.json'
        assert json.loads(terminal.read_text())['completed']
        publish_finite(runner.directory / 'controller-release.json',
            {'completed':True,'action':action,'coordinatorReviewSha256':review_sha,
             'harnessSourceSha256':digest(SOURCE),'payloads':payloads,
             'familyReceipt':str(family),'familySha256':digest(family),
             'terminalReceipt':str(terminal),'terminalSha256':digest(terminal)},runner.hard-10 if action=='prepare' else runner.hard)
    except BaseException as error:
        if action=='prepare':abort_preparation(runner,type(error).__name__,str(error))
        raise


def resolve(name):
    path=Path(name)
    return path if path.is_absolute() else ROOT/path


def identity_sha(identities):
    return hashlib.sha256(json.dumps(identities,sort_keys=True,separators=(',',':')).encode()).hexdigest()


def active_flags(active):
    active=Path(active).resolve()
    assert OUT.resolve() in active.parents
    return ['-p:UseArtifactsOutput=true','-p:ArtifactsPath='+str(active),
            '-p:ArtifactsPivots=release','-p:OutputPath='+str(active/'bin')+'/',
            '-p:AppendTargetFrameworkToOutputPath=false','-p:AppendRuntimeIdentifierToOutputPath=false']


def verify_live_or_removed(identities, terminal):
    removed={} if terminal is None else terminal['removedIdentities']
    for name,expected in identities.items():
        if name in removed:
            assert removed[name]==expected and not resolve(name).exists(),'Terminal identity/deletion changed'
        else:
            assert digest(resolve(name))==expected,'Actual binary/result identity changed'


def write_api_union(path):
    rows=[];members=set()
    for source in sorted(API_ROOT.glob('*.txt')):
        lines=source.read_text().splitlines();assert lines[0]=='xemele-api-v1'
        for line in lines[1:]:
            row=json.loads(line)
            assert set(row)=={'id','member','declaration'} and line==json.dumps(row,ensure_ascii=False,sort_keys=True,separators=(',',':'))
            assert row['member'] not in members;members.add(row['member']);rows.append(dict(row,id=row['member']))
    rows.sort(key=lambda row:row['member'])
    Path(path).write_text('xemele-api-v1\n'+''.join(json.dumps(row,ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n' for row in rows))


def verify_active_prepared(manifest):
    assert not mismatches(manifest['identities']), 'Prepared source/dependency/binaries changed'
    assert binary_identities(manifest['probePath'])==manifest['binaryInputs'],'Prepared binary membership/hash changed'


def claim_prepared(path, expected, review_sha):
    manifest,source=verify_prepared(path,expected,review_sha)
    from family import nested
    marker=Path(path).parent/'consumed.json'
    nested.durable(marker,{'preparationSha256':expected,'controller':os.environ['XEMELE_IMPORT_RUN_DIRECTORY'],'state':'claimed; never reusable'})
    return manifest,source


def read_terminal(controller):
    path=Path(controller['terminalReceipt']).resolve()
    assert OUT.resolve() in path.parents and path.name=='terminal-cleanup.json'
    assert digest(path)==controller['terminalSha256']
    receipt=json.loads(path.read_text())
    assert receipt['completed'] and receipt['state']=='terminal-summary; deleted bytes cannot be rechecked or reused'
    assert all(not resolve(name).exists() for name in receipt['removedIdentities'])
    if receipt.get('failureDiagnostics') is not None:
        diagnostic=receipt['failureDiagnostics'];path=Path(diagnostic['path']).resolve()
        assert OUT.resolve() in path.parents and digest(path)==diagnostic['sha256']
    return receipt


def terminal_cleanup(runner, action, prepared=None, preserve_preparation=True, receipt_name="terminal-cleanup.json", outer_error=None, outer_detail=None):
    """Only owned transient paths; no source, corpus, cache or installed runtime copies."""
    import shutil
    from family import nested
    assert runner.closed and runner.receipt and runner.receipt['ownership']['allOwnedGroupsAbsent']
    assert all(row['directChildReaped'] and row['ownedGroupAbsent'] and row['logClosed']
               and row['nestedOwnership']['allOwnedGroupsAbsent'] for row in runner.sequence)
    directory=runner.directory.resolve()
    assert OUT.resolve() in directory.parents
    assert receipt_name in ['terminal-cleanup.json','preparation-abort.json']
    from diagnostics import preserve_failure
    extra=[] if prepared is None else [Path(prepared['directory']),Path(prepared['preparationReceipt']).parent]
    try:
        failure_diagnostics=preserve_failure(runner,outer_error,outer_detail,abort=receipt_name=='preparation-abort.json',extra_directories=extra)
    except BaseException as diagnostic_error:
        # Diagnostic parsing failure must not strand raw/generated outputs,
        # nor permit a passing controller after evidence capture failed.
        runner.stopped=True
        failed=directory/('diagnostic-capture-abort.json' if receipt_name=='preparation-abort.json' else 'diagnostic-capture-failed.json')
        nested.durable(failed,{'outcomeCompleted':False,'error':type(diagnostic_error).__name__,
                              'detail':str(diagnostic_error)[:768],'scope':'Diagnostic capture failed; actual cause remains unknown'})
        failure_diagnostics={'path':str(failed),'sha256':digest(failed)}
    retain_preparation=action=='prepare' and preserve_preparation and outer_error is None and runner.receipt['completed'] and not getattr(runner,'stopped',False)
    delete_roots=[];delete_files=[]
    if action=='check':
        delete_roots.append(directory/'checks/active')
        results=directory/'checks/results'
        if results.exists():delete_files.extend(p for p in results.iterdir() if p.name not in ['checks-completed.json','focused-summary.json'])
    if action=='controls':delete_roots.append(directory/'controls')
    if action=='prepare':
        delete_files.append(directory/'preparation/control.json')
        if not retain_preparation:
            delete_roots.append(directory/'preparation/active')
            delete_files.extend([directory/'preparation/prepared.json',directory/'controller-release.json'])
    if prepared is not None:
        preparation=Path(prepared['preparationReceipt']).resolve().parent
        marker=preparation/'consumed.json'
        assert json.loads(marker.read_text())['controller']==str(directory),'Cannot remove another active preparation'
        active=Path(prepared['activeOutputRoot']).resolve()
        assert active==preparation/'active' and OUT.resolve() in active.parents
        delete_roots.append(active)
        measurement=Path(prepared['directory']).resolve()
        assert OUT.resolve() in measurement.parents
        delete_files.extend([measurement/'control.json',measurement/'allocations.json'])
        if measurement.exists():delete_files.extend(measurement.rglob('*.log'))
        delete_files.extend(preparation.rglob('*.log'))
    delete_files.extend(directory.rglob('*.log'))
    removed={}
    for root in delete_roots:
        assert OUT.resolve() in root.resolve().parents
        if root.exists():
            assert not root.is_symlink() and all(not p.is_symlink() for p in root.rglob('*'))
            for path in root.rglob('*'):
                if path.is_file():removed[key(path)]=digest(path)
    for path in delete_files:
        assert OUT.resolve() in path.resolve().parents and not path.is_symlink()
        if path.is_file():removed[key(path)]=digest(path)
    # Hash all transient bytes before removing anything. Summaries never imply
    # that the deleted bytes can be checked again on a later attempt.
    # Revoke acceptance paths before deleting the active payload.
    if action=='prepare' and not retain_preparation:
        for path in [directory/'controller-release.json',directory/'preparation/prepared.json']:
            path.unlink(missing_ok=True)
    for root in delete_roots:
        if root.exists():shutil.rmtree(root)
    for path in delete_files:path.unlink(missing_ok=True)
    assert all(not resolve(name).exists() for name in removed)
    receipt={'completed':True,'state':'terminal-summary; deleted bytes cannot be rechecked or reused',
             'action':action,'removedIdentities':removed,'removedFiles':len(removed),
             'familyOutcome':runner.receipt['completed'],'failureDiagnostics':failure_diagnostics,'outerError':outer_error,'activePreparedOutputs':retain_preparation}
    prior=directory/'terminal-cleanup.json'
    if receipt_name=='preparation-abort.json' and prior.is_file():receipt['supersedesTerminalSha256']=digest(prior)
    nested.durable(directory/receipt_name,receipt)
    return receipt


def allocation_summary(path, manifest):
    """Read counters only after enclosing family cleanup; retain concise aggregates."""
    import statistics
    data=json.loads(Path(path).read_text())
    assert data['mode']=='--measure' and data['status']=='passed' and data['runtime']=='.NET 10.0.11'
    assert len(data['rows'])==24 and data['consumed']==1012 and data['retainedResults']==28 and len(data['heap'])==7
    probe=Path(manifest['probePath'])
    assert data['assemblySha256']==digest(probe.parent/'Xemele.dll') and data['probeSha256']==digest(probe)
    assert data['sourceRevision']==manifest['sourceRevision'] and data['serverGc'] is False
    assert data['positiveBytes']>=1024 and data['coldInputsBytes']>=0
    assert [row['route'] for row in data['rows']]==list(range(24))
    rows=[]
    for row in data['rows']:
        values=row['distribution']
        assert row['operations']==len(values)==32 and all(value>0 for value in values) and row['bytes']>=sum(values)
        rows.append({'route':row['route'],'name':row['name'],'operations':32,'firstRunBytes':row['firstRunBytes'],
                     'totalBytes':row['bytes'],'minimumBytes':min(values),'medianBytes':statistics.median(values),
                     'maximumBytes':max(values),'meanBytes':statistics.mean(values)})
    assert all(row['count']==4 and row['delta']==row['after']-row['before'] for row in data['heap'])
    return {'completed':True,'rawSha256':digest(path),'runtime':data['runtime'],'coldInputsBytes':data['coldInputsBytes'],
            'positiveBytes':data['positiveBytes'],'sourceRevision':data['sourceRevision'],'architecture':data['architecture'],
            'serverGc':data['serverGc'],'consumed':1012,'retainedResults':28,'rows':rows,'retainedHeap':data['heap']}


def verify_allocation_success(path, expected, review_sha):
    controller=verify_controller(path,expected,'measure',review_sha)
    release=json.loads(Path(path).read_text())
    assert release['completed'] and release['afterIdentities'] and not release['rawAllocationsInspected']
    summary=Path(path).parent/'allocation-summary.json'
    assert controller['payloads'].get(key(summary))==digest(summary)
    data=json.loads(summary.read_text());assert data['completed']
    return data  # terminal evidence only; never permits prepared-byte reuse


def abort_preparation(runner, error, detail=None):
    return terminal_cleanup(runner,'prepare',preserve_preparation=False,
                            receipt_name='preparation-abort.json',outer_error=error,outer_detail=detail)
