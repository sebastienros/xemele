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
PROBE = ROOT / 'benchmarks/Xemele.Benchmarks/M8-xsd-import-visibility/bin/probe/net10.0/Xemele.Benchmarks.dll'
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


def commands(output):
    output = Path(output).resolve()
    assert OUT.resolve() in output.parents
    return [[str(DOTNET), '--fx-version', '10.0.11', str(PROBE), mode, str(output / name)]
            for mode, name in [('--control', 'control.json'), ('--measure', 'allocations.json')]]


def build_commands():
    project = 'benchmarks/Xemele.Benchmarks/M8-xsd-import-visibility/Import.Probe.csproj'
    return [[str(DOTNET), 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *FLAGS],
            [str(DOTNET), 'build', project, '-c', 'Release', '--no-restore', *FLAGS]]


def binary_identities():
    paths = {DOTNET.resolve(), Path(sys.executable).resolve(), PROBE.resolve(),
             ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll'}
    for folder in [PROBE.parent, DOTNET.parent / 'shared/Microsoft.NETCore.App/10.0.11', DOTNET.parent / 'host/fxr']:
        assert folder.is_dir(), 'Required prepared binary directory missing'
        for path in folder.rglob('*'):
            if path.is_file(): paths.add(path.resolve())
    assert PROBE.parent / 'Xemele.dll' in paths, 'Actual probe product dependency must be pinned'
    assert PROBE.with_suffix('.deps.json') in paths and PROBE.with_suffix('.runtimeconfig.json') in paths
    return {key(path): digest(path) for path in sorted(paths)}


def verify_control(path):
    data = json.loads(Path(path).read_text())
    assert data['mode'] == '--control' and data['status'] == 'passed' and data['runtime'] == '.NET 10.0.11'
    assert len(data['rows']) == 24 and data['consumed'] == 1012 and data['retainedResults'] == 28
    assert data['coldInputsBytes'] == data['positiveBytes'] == 0 and len(data['heap']) == 7
    assert all(row['bytes'] == row['firstRunBytes'] == 0 and row['operations'] == 32 and len(row['distribution']) == 32 and not any(row['distribution']) for row in data['rows'])
    assert all(row['before'] == row['after'] == row['delta'] == 0 and row['count'] == 4 for row in data['heap'])
    assert [r['route'] for r in data['rows']] == list(range(24))
    assert data['assemblySha256'] == digest(PROBE.parent / 'Xemele.dll') and data['probeSha256'] == digest(PROBE)
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
    manifest = json.loads(path.read_text())
    assert manifest['coordinatorReviewSha256'] == review_sha
    source = review(review_sha)
    assert manifest['harnessSourceSha256'] == digest(SOURCE)
    assert all(manifest['identities'].get(name) == value for name, value in source['identities'].items()), 'Whole reviewed source/dependency identity coverage required'
    assert manifest['commands'] == commands(Path(manifest['directory'])), 'Exact required counter/disabled commands changed'
    assert (manifest['maximumSeconds'], manifest['workSeconds'], manifest['cleanupReserveSeconds']) == (120, 100, 20)
    assert key(PROBE) in manifest['identities'] and key(PROBE.parent / 'Xemele.dll') in manifest['identities'], 'Runner and actual product dependency required'
    assert not mismatches(manifest['identities']), 'Prepared source/dependency/binaries changed'
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
        controller=payload.parent if action=='controls' else payload.parents[1 if action=='prepare' else 2]
        release_path=controller/'controller-release.json'
    release = json.loads(release_path.read_text())
    assert release['completed'] and release['action'] == action and release['coordinatorReviewSha256'] == review_sha
    assert release['harnessSourceSha256'] == digest(SOURCE) and release['payloads'].get(key(payload)) == expected
    assert not release_path.with_name(release_path.name + '.deadline-failure.json').exists()
    family = Path(release['familyReceipt']).resolve()
    assert family == controller / 'family-cleanup.json' and digest(family) == release['familySha256']
    receipt = json.loads(family.read_text())
    assert receipt['completed'] and receipt['ownership']['allOwnedGroupsAbsent']
    assert receipt['sequence'] and all(__import__('family').clean(row) for row in receipt['sequence'])
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
    folders = {ROOT / 'src/Xemele/bin/Release/net10.0'}
    dlls = [Path(arg) for arg in argv if arg.endswith('.dll')]
    api_union = any(arg.endswith('M5-validation-model/check_api.py') for arg in argv)
    if not dlls and not api_union:
        return {}, []
    for dll in dlls:
        absolute = dll if dll.is_absolute() else ROOT / dll
        assert absolute.is_file()
        folders.add(absolute.parent.resolve())
    if api_union: folders.add(ROOT / 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0')
    folders.update([DOTNET.parent / 'shared/Microsoft.NETCore.App/10.0.11', DOTNET.parent / 'host/fxr'])
    identities = {key(DOTNET): digest(DOTNET), key(sys.executable): digest(sys.executable)}
    for folder in folders:
        assert folder.is_dir(), 'Actual binary dependency directory missing'
        for path in folder.rglob('*'):
            if path.is_file(): identities[key(path)] = digest(path)
    return identities, sorted(key(folder) for folder in folders)


def archive_execution(directory, index, argv, before, folders):
    import shutil
    from family import nested
    directory = Path(directory)
    archive = directory / 'binary-archive'; archive.mkdir(exist_ok=True)
    saved = {}
    for name, expected in before.items():
        source = Path(name); source = source if source.is_absolute() else ROOT / source
        destination = archive / expected
        if not destination.exists():
            # Exclusive content-addressed bytes preserve earlier consumer binaries
            # across later deliberate normal/XML-only builds at the same output path.
            with source.open('rb') as input_stream, destination.open('xb') as output:
                shutil.copyfileobj(input_stream, output)
                output.flush(); os.fsync(output.fileno())
        assert digest(source) == digest(destination) == expected
        saved[key(destination)] = expected
    nested.durable(directory / f'binary-inputs-{index:03d}.json',
        {'argv': list(argv), 'actualInputs': before, 'folders': folders, 'archiveIdentities': saved})


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


def verify_correctness_evidence(evidence):
    assert evidence['apiInputs'] == api_inputs(), 'API inputs changed/deleted/added'
    latest = {}
    folders = set()
    for row in evidence['binaryRuns']:
        assert digest(row['receiptPath']) == row['receiptSha256']
        assert digest(row['lifecyclePath'])==row['lifecycleSha256']
        actual=json.loads(Path(row['lifecyclePath']).read_text())
        assert actual['argv']==row['argv'] and __import__('family').clean(actual)
        assert not mismatches(row['archiveIdentities']), 'Tested binary archive changed'
        assert set(row['actualInputs'].values()) == set(row['archiveIdentities'].values())
        current_folders=[Path(folder if Path(folder).is_absolute() else ROOT/folder) for folder in row['folders']]
        latest={name:value for name,value in latest.items() if not any(Path(name if Path(name).is_absolute() else ROOT/name).is_relative_to(folder) for folder in current_folders)}
        latest.update(row['actualInputs']); folders.update(row['folders'])
    assert not mismatches(latest), 'Actual test/runtime/dependency binaries changed'
    # Membership is bound as well as bytes: no newly injected or deleted dependency.
    expected_members = {name for name in latest if any(Path(name if Path(name).is_absolute() else ROOT/name).is_relative_to(Path(folder if Path(folder).is_absolute() else ROOT/folder)) for folder in folders)}
    actual_members = {key(p) for folder in folders for p in Path(folder if Path(folder).is_absolute() else ROOT/folder).rglob('*') if p.is_file()}
    assert actual_members == expected_members, 'Actual binary dependency membership changed'
    directory = Path(evidence['resultDirectory'])
    actual_results = {key(p): digest(p) for p in directory.iterdir() if p.is_file() and p.name != 'checks-completed.json'}
    assert actual_results == evidence['resultIdentities'], 'Fresh correctness result membership/hash changed'


def verify_checks_success(path, expected, review_sha):
    verify_controller(path, expected, 'check', review_sha)
    data = json.loads(Path(path).read_text())
    assert data['completed'] and data['status'] == 'passed' and all(v == 'passed' for v in data['checks'].values())
    assert data['harnessSourceSha256'] == digest(SOURCE)
    assert digest(data['familyReceipt']) == data['familySha256'] and json.loads(Path(data['familyReceipt']).read_text())['completed']
    verify_correctness_evidence(data['evidence'])
    return data


def publish_controller(runner, action, payloads, review_sha):
    from family import publish_finite
    assert runner.closed and runner.receipt and runner.receipt['completed'] and not runner.stopped
    assert all(__import__('family').clean(row) for row in runner.sequence)
    assert not mismatches(payloads), 'Child candidate changed before enclosing acceptance'
    family = runner.directory / 'family-cleanup.json'
    assert json.loads(family.read_text())['completed']
    publish_finite(runner.directory / 'controller-release.json',
        {'completed':True,'action':action,'coordinatorReviewSha256':review_sha,
         'harnessSourceSha256':digest(SOURCE),'payloads':payloads,
         'familyReceipt':str(family),'familySha256':digest(family)},runner.hard)
