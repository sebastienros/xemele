"""Independent static source-selected method/case cardinalities; no discovery oracle."""
from collections import Counter
import json
from pathlib import Path
import bindings as b

EXPECTED=Path(__file__).with_name('test-selection.json')


def validate(tests,expectation=None):
    expected=json.loads(EXPECTED.read_text()) if expectation is None else expectation
    assert tests and len(tests)==expected['totalCases'],'Selected case membership incomplete'
    ids=[t.attrib['id'] for t in tests]
    assert len(set(ids))==len(ids),'Duplicate runtime test identity'
    assert all(t.attrib['result']=='Pass' for t in tests),'Failed/skipped/non-Pass selected case'
    observed=Counter(t.attrib['type']+'.'+t.attrib['method'] for t in tests)
    required={name:data['cases'] for name,data in expected['methods'].items()}
    assert dict(observed)==required,'Selected namespace/method/theory case omitted or added'
    assert not (set(observed)&set(expected['exclusions']))
    return dict(expectedMethods=len(required),expectedCases=expected['totalCases'],observedMethods=len(observed),observedCases=len(tests),
                expectedSelectionSha256=b.digest(EXPECTED),observedIdentitySha256=b.identity_sha(sorted(ids)),
                limitation='Fixed source/binary/data identities plus exact independently reviewed method cardinalities; no runtime discovery-derived expectation')
