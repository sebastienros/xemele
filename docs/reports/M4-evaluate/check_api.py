#!/usr/bin/env python3
"""Compare actual compiled facade and all earlier public fragments."""
import argparse
import json
import pathlib
import subprocess
import tempfile
p = argparse.ArgumentParser()
p.add_argument('--dotnet', required=True)
p.add_argument('--assembly', default='src/Xemele/bin/Release/net10.0/Xemele.dll')
p.add_argument('--emit', action='store_true')
a = p.parse_args()
root = pathlib.Path(__file__).resolve().parents[3]
def rows(path):
    lines = pathlib.Path(path).read_text().splitlines()
    assert lines[0] == 'xemele-api-v1'
    return {row['id']: row for row in map(json.loads, lines[1:])}
with tempfile.TemporaryDirectory(prefix='m4-evaluate-api-') as temporary:
    output = pathlib.Path(temporary) / 'actual.txt'
    subprocess.run([a.dotnet, '--fx-version', '10.0.11', str(root / 'docs/reports/M4-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'), 'emit', str(root / a.assembly), str(output)], check=True)
    actual = rows(output)
    frozen = rows(root / 'tests/Contracts/Fixtures/M4/Public.golden.txt')
    expected = frozen
    model = {k: v for k, v in actual.items() if 'Xemele.XPath.' in k}
    assert model == expected, 'compiled model differs from frozen proposal subset'
    legacy = {}
    for path in (root / 'docs/api').glob('*.txt'):
        if not path.name.startswith('M4-'):
            for k, v in rows(path).items():
                assert k not in legacy
                legacy[k] = v
    assert len(legacy) == 720 and all(actual.get(k) == v for k, v in legacy.items()), 'earlier public API drift'
    assert len(actual) == len(legacy) + len(model), 'unexpected public additions'
    baseline = root / 'docs/api/M4-evaluate.txt'
    facade = {k:v for k,v in model.items() if k.startswith(('T:Xemele.XPath.XPathExpression', 'M:Xemele.XPath.XPathExpression'))}
    assert len(facade) == 4 and len(actual) == 897
    assert rows(root / 'docs/api/M4-model.txt') == {k:v for k,v in model.items() if k not in facade}
    if a.emit:
        baseline.write_text('xemele-api-v1\n' + '\n'.join(json.dumps(facade[k], ensure_ascii=False, sort_keys=True, separators=(',', ':')) for k in sorted(facade)) + '\n')
    assert rows(baseline) == facade
    # The comparison predicates reject both omissions and changed signatures.
    omitted = dict(model); omitted.pop(next(iter(omitted)))
    changed = {k: dict(v) for k, v in model.items()}; changed[next(iter(changed))]['declaration'] += ' corruption'
    assert omitted != expected and changed != expected
    print(f'PASS: model={len(model)}, preserved={len(legacy)}, total={len(actual)}; 2 comparison corruption controls reject')
