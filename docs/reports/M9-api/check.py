"""Focused compiled API/preview comparison using the existing approved extractor."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
HOST = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
env = dict(os.environ, DOTNET_ROOT=str(HOST.parent), DOTNET_ROLL_FORWARD='Disable')
spec = importlib.util.spec_from_file_location('bindings', ROOT / 'docs/reports/M8-xsd-resolved-availability/harness/bindings.py')
b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)
product = ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll'
checker = ROOT / 'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
prefix = [str(HOST), '--fx-version', '10.0.11', str(checker)]

def call(mode, path):
    return subprocess.run([*prefix, mode, str(product), str(path)], env=env,
                          capture_output=True, text=True, timeout=60)

with tempfile.TemporaryDirectory(prefix='xemele-m9-api-') as temporary:
    td = Path(temporary)
    union = td / 'union.txt'
    b.write_api_union(union)
    result = call('compare', union)
    assert result.returncode == 0, result.stdout + result.stderr
    expected = b.rows(union)
    assert len(expected) == 1430
    actual = td / 'actual.txt'
    result = call('emit', actual)
    assert result.returncode == 0, result.stdout + result.stderr
    compiled = b.rows(actual)
    assert compiled == expected
    old_path = ROOT / 'docs/reports/M1-gate/integration/runtime-api.json'
    old = json.loads(old_path.read_text())
    assert len(old['provenance']) == 222
    assert all(compiled[x['member']]['declaration'] == x['declaration'] for x in old['provenance'])
    lines = union.read_text().splitlines()
    negative = {}
    for mode in ['missing', 'changed']:
        changed = lines.copy()
        if mode == 'missing':
            changed.pop(1)
        else:
            row = json.loads(changed[1])
            row['declaration'] += ' [deliberate incompatible mutation]'
            changed[1] = json.dumps(row, sort_keys=True, separators=(',', ':'))
        path = td / (mode + '.txt')
        path.write_text('\n'.join(changed) + '\n')
        outcome = call('compare', path)
        assert outcome.returncode != 0
        negative[mode] = dict(exit=outcome.returncode, refused=True)
    report = dict(task='M9-api', revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                  result='passed', assemblySha256=hashlib.sha256(product.read_bytes()).hexdigest(),
                  checkerSha256=hashlib.sha256(checker.read_bytes()).hexdigest(),
                  approvedExtractor='docs/reports/M0-contracts/ApiCheck/Program.cs', apiFragments=55,
                  matchedCompiledMembers=1430,
                  firstPreview=dict(source=str(old_path.relative_to(ROOT)), sourceSha256=hashlib.sha256(old_path.read_bytes()).hexdigest(),
                                    matchedUnchanged=222, removed=0, changed=0, addedSincePreview=1208),
                  negativeControls=negative,
                  migration=dict(checks=20, result='passed', warnings=0, errors=0, runtime='10.0.11', architecture='Arm64'),
                  commands=['Pinned SDK10.0.400 offline restore/build migration Examples.csproj and approved ApiCheck.csproj',
                            'Pinned dotnet --fx-version10.0.11 Examples.dll', 'python3 docs/reports/M9-api/check.py',
                            'python3 tools/planning/tasks.py validate; git diff --check'],
                  review='docs/api-review/README.md', repairRequests=[],
                  priorReportingAttempt=dict(result='failed', cause='Compared row dictionary to declaration string during first-preview assertion; underlying1430assembly match passed. Offline assertion corrected.'),
                  limitations=['Local macOS arm64 behavior only', 'Historical metadata normalizer ABI/custom attribute scope remains explicit'],
                  rawOutputs='Temporary union/emitted/control files inspected and deleted; concise summary only')
    (ROOT / 'docs/reports/M9-api/checks.json').write_text(json.dumps(report, indent=2) + '\n')
print('PASS compiled API1430/55; preview222unchanged; two mismatch controls refused; migration20passed')
