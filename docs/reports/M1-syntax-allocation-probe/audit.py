"""Audit every visible delta against the assigned task ownership."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[3]
report = Path(__file__).resolve().parent
claim = '98236f56b63a689f98756ee115f8f4458b756804'
task_path = 'docs/tasks/M1-syntax-allocation-probe.json'
task = json.loads((root / task_path).read_text())

def git(*args):
    return subprocess.check_output(['git', *args], cwd=root, text=True)

def names(*args):
    return [p for p in git(*args).split('\n') if p]

def owned(path):
    return path == task_path or any(path.startswith(p) if p.endswith('/') else path == p for p in task['owns'])

for args in [('diff', '--check', claim, 'HEAD'), ('diff', '--check'), ('diff', '--cached', '--check')]:
    git(*args)
sets = {
    'committed': names('diff', '--name-only', claim, 'HEAD'),
    'staged': names('diff', '--cached', '--name-only'),
    'working': names('diff', '--name-only'),
    'untracked': names('ls-files', '--others', '--exclude-standard'),
}
for scope, paths in sets.items():
    assert all(owned(path) for path in paths), (scope, paths)
assert git('branch', '--show-current').strip() == task['branch']
assert Path(git('rev-parse', '--show-toplevel').strip()) == root
for ancestor in [claim, task['base_revision'], 'd95f12e36e76ce9a0a6722e13f7b5f775d015090']:
    git('merge-base', '--is-ancestor', ancestor, 'HEAD')
path = 'tests/Xemele.Tests/M1-syntax/StreamingTests.cs'
original = git('show', claim + ':' + path)
current = (root / path).read_text().replace('using System.Runtime.CompilerServices;\n', '', 1)
start = '    [Fact]\n    public void WarmRepeatedTagsAndStreamingUnicodeDispatchAllocateZero()'
end = '    [Fact]\n    public void LocationOverflowIsStructuredAndSourceScalarQuotaPrecedesNormalization()'
a = original.index(start); b = original.index(end)
x = current.index(start); y = current.index(end)
assert original[:a] == current[:x] and original[b:] == current[y:], 'Other test changes'
for path in names('ls-files', 'src', 'tests/Xemele.Tests'):
    assert git('rev-parse', 'f0c42a85783479efd2c76566a230a1c2763a0579:' + path) == git('rev-parse', 'e8b9f7ba15e0a76101adba877b2569bba09f8c0f:' + path), path
historic = {}
for source, copy in [('baseline-full.txt.gz','historical-baseline-full.txt.gz'),('final-full.txt.gz','historical-dom-final-full.txt.gz'),('integration-full.txt.gz','historical-dom-integration-full.txt.gz')]:
    upstream = (root / 'docs/reports/M1-dom-allocation-probe' / source).read_bytes()
    copied = (report / copy).read_bytes()
    assert upstream == copied
    historic[copy] = hashlib.sha256(copied).hexdigest()
settings = {key: os.environ.get(key) for key in ['DOTNET_TieredCompilation', 'COMPlus_TieredCompilation', 'DOTNET_TieredPGO', 'COMPlus_TieredPGO', 'DOTNET_ReadyToRun', 'COMPlus_ReadyToRun']}
assert all(value is None for value in settings.values()), settings
(report / 'source.diff').write_text(git('diff', claim, 'HEAD', '--', 'tests/Xemele.Tests/M1-syntax/StreamingTests.cs'))
(report / 'audit.json').write_text(json.dumps({
    'revision': git('rev-parse', 'HEAD').strip(), 'claim': claim, 'paths': sets,
    'owned': True, 'whitespaceChecks': 'passed', 'otherTestBodiesUnchanged': True,
    'productionAndTestBlobsIdenticalAcrossBuildAndRunRevisions': True,
    'historicalCopiesSha256': historic, 'runtimeSettingsOverrides': settings,
    'status': git('status', '--porcelain=v1'),
}, indent=2) + '\n')
print('Owned committed/staged/working/untracked deltas; whitespace, branch/base/claim/prerequisite, unchanged other tests and source blobs, historical copies and default settings: passed.')
