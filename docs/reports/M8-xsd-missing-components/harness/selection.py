"""Independent static source-selected method/case cardinalities; no discovery oracle."""
from collections import Counter
import json
from pathlib import Path
import bindings as b

EXPECTED=Path(__file__).with_name('test-selection.json')


def mismatch_summary(required,observed,expected_cases,observed_cases):
    missing=sorted(set(required)-set(observed))
    unexpected=sorted(set(observed)-set(required))
    different=sorted(name for name in set(required)&set(observed) if required[name]!=observed[name])
    parts=[f'Selected membership mismatch: methods expected={len(required)} observed={len(observed)}',
           f'cases expected={expected_cases} observed={observed_cases}',
           f'missing={len(missing)} unexpected={len(unexpected)} countDiff={len(different)}']
    truncated=False
    # One deterministic witness per category; escape control characters so the
    # bounded assertion remains one structured exception line after raw deletion.
    for kind,names in [('missing',missing),('unexpected',unexpected),('count',different)]:
        if not names:continue
        name=names[0];identifier=json.dumps(name[:130],ensure_ascii=True)
        clipped=len(name)>130 or len(identifier)>132
        if clipped:identifier=identifier[:129]+'...'
        parts.append(f'{kind} {identifier} expected={required.get(name,0)} observed={observed.get(name,0)}')
        truncated|=clipped or len(names)>1
    message='; '.join(parts)
    truncated|=len(message)>680
    return message[:680]+('; truncated=1' if truncated else '; truncated=0')


def validate(tests,expectation=None):
    expected=json.loads(EXPECTED.read_text()) if expectation is None else expectation
    assert tests and len(tests)==expected['totalCases'],'Selected case membership incomplete'
    ids=[t.attrib['id'] for t in tests]
    assert len(set(ids))==len(ids),'Duplicate runtime test identity'
    assert all(t.attrib['result']=='Pass' for t in tests),'Failed/skipped/non-Pass selected case'
    observed=Counter(t.attrib['type']+'.'+t.attrib['method'] for t in tests)
    required={name:data['cases'] for name,data in expected['methods'].items()}
    assert dict(observed)==required,mismatch_summary(required,observed,expected['totalCases'],len(tests))
    assert not (set(observed)&set(expected['exclusions']))
    return dict(expectedMethods=len(required),expectedCases=expected['totalCases'],observedMethods=len(observed),observedCases=len(tests),
                expectedSelectionSha256=b.digest(EXPECTED),observedIdentitySha256=b.identity_sha(sorted(ids)),
                limitation='Fixed source/binary/data identities plus exact independently reviewed method cardinalities; no runtime discovery-derived expectation')
