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
    nested.durable(Path(path), {'status': 'passed', 'checks': checks, 'familyReceipt': str(runner.directory / 'family-cleanup.json')})
    if time.monotonic() >= runner.hard:
        Path(path).rename(Path(path).with_name('deadline-invalid-candidate.json'))
        nested.durable(Path(path).with_name('late-success-failure.json'), {'status': 'failed', 'reason': 'hard deadline'})
        raise TimeoutError('Success publication crossed deadline; receipt invalid without root cleanup success')


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
    receipt = Path(manifest['preparationReceipt'])
    assert OUT.resolve() in receipt.resolve().parents
    assert manifest['identities'].get(key(receipt)) == digest(receipt) and json.loads(receipt.read_text())['completed']
    return manifest, source
