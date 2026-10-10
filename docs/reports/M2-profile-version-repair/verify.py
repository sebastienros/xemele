#!/usr/bin/env python3
"""Independent bounded diff/hash control; no product outcome is an input."""
import collections, hashlib, json, os, pathlib, subprocess
ROOT = pathlib.Path(__file__).resolve().parents[3]
CLAIM = 'a0e243e85e0a7cf9a8ea74d8020893c42dde6b0f'
ID = 'xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-38'
PATH = 'tests/corpora/xml/m2-applicability.json'
baseline = json.loads(subprocess.check_output(['git', 'show', CLAIM + ':' + PATH], cwd=ROOT))
current = json.loads((ROOT / PATH).read_bytes())
changed = []
for before, after in zip(baseline['cases'], current['cases'], strict=True):
    if before != after:
        assert before['id'] == after['id'] == ID
        assert {k for k in before if before[k] != after[k]} == {'expectedOutcomes', 'rule', 'normative', 'rationale'}
        assert before['expectedOutcomes'] == ['rejected'] and after['expectedOutcomes'] == ['accepted']
        assert after['rule'] == 'superseded-e38-version'
        assert after['normative'] == ['https://www.w3.org/XML/xml-V10-4e-errata#E10', 'https://www.w3.org/TR/2008/REC-xml-20081126/#NT-VersionNum', 'https://www.w3.org/TR/2008/REC-xml-20081126/#NT-TextDecl']
        changed.append(after['id'])
assert changed == [ID]
assert {k for k in baseline if baseline[k] != current[k]} == {'cases', 'summary'}
counts = baseline['summary'].copy()
counts['well-formedness'] -= 1
counts['superseded-e38-version'] = 1
assert current['summary'] == counts == dict(collections.Counter(c['rule'] for c in current['cases']))
cache = pathlib.Path(os.environ.get('XEMELE_CORPUS_RESOURCES', '/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources'))
for relative, expected in [('E38.xml','b6be7039a5dfeaad8eaad16eb3976c754888985bdd598b518a81a0f9547e3cf6'), ('E38.ent','028229ed9dda8481d7996ef03a1e17300255abef2bf082011312f09b28623eef')]:
    assert hashlib.sha256((cache / 'xmlconf/eduni/errata-2e' / relative).read_bytes()).hexdigest() == expected
assert hashlib.sha256((ROOT / 'tests/corpora/xml/inventory.json').read_bytes()).hexdigest() == current['inventorySha256']
print('PASS: one classification, 2584 unchanged rows, 3386 unchanged resource records; original primary/entity hashes and all other ledger metadata preserved.')
