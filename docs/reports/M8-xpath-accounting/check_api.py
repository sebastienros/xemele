#!/usr/bin/env python3
"""Compare complete compiled public metadata, and the exact frozen internal join."""
import argparse
import json
import pathlib
import subprocess
import tempfile

p = argparse.ArgumentParser()
p.add_argument('--dotnet', required=True)
p.add_argument('--baseline', required=True)
p.add_argument('--assembly', default='src/Xemele/bin/Release/net10.0/Xemele.dll')
a = p.parse_args()
root = pathlib.Path(__file__).resolve().parents[3]
extractor = root / 'docs/reports/M8-xpath-accounting/ApiCheck/bin/Release/net10.0/ApiCheck.dll'

def rows(path):
    lines = pathlib.Path(path).read_text().splitlines()
    assert lines[0] == 'xemele-api-v1'
    return {row['id']: row for row in map(json.loads, lines[1:])}

with tempfile.TemporaryDirectory(prefix='m8-accounting-api-') as temporary:
    paths = [pathlib.Path(temporary) / name for name in ('base.txt', 'actual.txt', 'internal.txt')]
    for mode, assembly, output in [('emit', a.baseline, paths[0]), ('emit', a.assembly, paths[1]), ('accounting', a.assembly, paths[2])]:
        subprocess.run([a.dotnet, '--fx-version', '10.0.11', str(extractor), mode, str(root / assembly), str(output)], check=True)
    baseline, actual, internal = map(rows, paths)
    assert baseline == actual and len(actual) >= 897, 'public API drift'
    expected = rows(root / 'docs/reports/M8-xpath-accounting/internal.golden.txt')
    assert internal == expected and len(internal) == 5, 'frozen accounting join drift'
    for collection, frozen in [(actual, baseline), (internal, expected)]:
        omitted = dict(collection); omitted.pop(next(iter(omitted)))
        changed = {k: dict(v) for k, v in collection.items()}; changed[next(iter(changed))]['declaration'] += ' corruption'
        assert omitted != frozen and changed != frozen
    print(f'PASS: {len(actual)} public members unchanged; five frozen internal join rows match; four comparison corruption controls reject')
