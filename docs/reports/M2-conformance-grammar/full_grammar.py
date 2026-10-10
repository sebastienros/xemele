"""Original-byte execution against independent normative/original-suite models.

Reuse the verified bounded child, source verifier and model projector. Store
only count/hash maps and exact disagreement resolutions, never payloads.
"""
import asyncio
import base64
import collections
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

OWN = Path(__file__).resolve().parent
ROOT = OWN.parents[2]
sys.path.insert(0, str(ROOT / 'docs/reports/M2-conformance-profile'))
from child import Child
from classify import BASE, verified
import audit
from semantic_oracle import Expected

DOTNET = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
PRODUCT = ROOT / 'samples/M2-conformance-grammar/bin/Release/net10.0/Xemele.Tests.dll'
REFERENCE = ROOT / 'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
audit.product = PRODUCT
audit.DOTNET = DOTNET


def hashed(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=True, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def freeze():
    binding = audit.freeze()
    revision = binding['revision']
    paths = subprocess.check_output(['git', 'ls-files', '--', 'samples/M2-conformance-grammar',
                                    'tests/Xemele.Tests/M2-conformance', 'tests/fixtures/M2-conformance/Grammar',
                                    'docs/reports/M2-conformance-grammar'], cwd=ROOT, text=True).splitlines()
    for path in paths:
        if Path(path).suffix not in ('.cs', '.csproj', '.py') and not path.startswith('tests/fixtures/'):
            continue
        content = (ROOT / path).read_bytes()
        if content != subprocess.check_output(['git', 'show', revision + ':' + path], cwd=ROOT):
            raise RuntimeError('uncommitted preparation: ' + path)
        binding['sourceInputs'][path] = hashlib.sha256(content).hexdigest()
    return binding


def original_output(case, expected, inventory, files, overrides, counts, witnesses):
    for kind, path in inventory[case['id']]['outputPaths'].items():
        original = Expected(verified(path, files[path]['sha256']), BASE + path, {})
        if original.events == expected.events:
            counts['original-suite-output-models'] += 1
            witnesses.append([case['id'], path, files[path]['sha256'], hashed(original.events)])
            if case['id'] in overrides:
                raise ValueError('unused effective-profile output override')
        else:
            override = overrides[case['id']]
            assert override['inputSha256'] == case['inputSha256']
            assert override['outputPath'] == path and override['outputSha256'] == files[path]['sha256']
            differences = [(e, o) for e, o in zip(expected.events, original.events) if e != o]
            assert len(differences) == 1 and len(expected.events) == len(original.events)
            e, o = differences[0]
            if override['kind'] == 'text':
                assert e == {'kind': 'text', 'value': override['value']}
                assert o['kind'] == 'text'
            else:
                assert e['kind'] == o['kind'] == 'start'
                assert e['namespaceUri'] == o['namespaceUri'] and e['localName'] == o['localName']
                assert len(e['attributes']) == len(o['attributes']) == 1
                assert e['attributes'][0] == {'namespaceUri': '', 'localName': 'bar', 'value': override['value']}
            counts['authored-effective10-output-overrides'] += 1
            witnesses.append([case['id'], path, files[path]['sha256'], hashed(original.events), hashed(override)])


async def execute(report):
    before = freeze()
    ledger = json.loads((ROOT / 'tests/corpora/xml/m2-applicability.json').read_bytes())
    inventory = json.loads((ROOT / 'tests/corpora/xml/inventory.json').read_bytes())
    originals = {c['id']: c for c in inventory['cases']}
    files = {f['path']: f for f in ledger['files']}
    overrides = {c['id']: c for c in json.loads((ROOT / 'tests/fixtures/M2-conformance/Grammar/effective10-output-overrides.json').read_bytes())}
    assert len(ledger['cases']) == len(originals) == 2585 and len(files) == 3386
    for file in files.values():
        assert len(verified(file['path'], file['sha256'])) == file['bytes']
    counts = collections.Counter()
    resolutions, model_hashes, output_witnesses = [], {}, []
    children = []
    complete = False
    try:
        for binary in (PRODUCT, REFERENCE):
            children.append(await Child.start([DOTNET, str(binary)]))
        for case in ledger['cases']:
            data = verified(case['inputPath'], case['inputSha256'])
            resources = {BASE + p: verified(p, files[p]['sha256']) for p in case['offlinePaths']}
            request = dict(protocolVersion=1, caseId=case['id'], operation='xml.events', profile='xml-1.0',
                           inputBase64=base64.b64encode(data).decode(), options={'sourceUri': case['sourceUri']},
                           offlineResources={k: base64.b64encode(v).decode() for k, v in resources.items()})
            p = await children[0].exchange(json.dumps(request, ensure_ascii=True))
            r = await children[1].exchange(json.dumps(request, ensure_ascii=True))
            assert p['caseId'] == r['caseId'] == case['id']
            identity = p['identity']
            assert all(identity[k].endswith('+' + before['revision']) for k in ('driverBuild', 'productBuild'))
            assert r['engine']['build'].endswith('+' + before['revision'])
            assert identity['runtime'] == r['engine']['runtime'] == '.NET 10.0.11'
            outcome = {'success': 'accepted', 'parse-rejected': 'rejected'}.get(p['outcome'])
            reference = {'success': 'accepted', 'parse-rejected': 'rejected'}.get(r['outcome'])
            if outcome is None or reference is None:
                raise RuntimeError('Infrastructure failure: ' + case['id'] + ': ' + str(p.get('diagnostic')) + '; ' + str(r.get('diagnostics')))
            assert outcome in case['expectedOutcomes'], case['id'] + ' product outcome gap'
            counts['product:' + outcome] += 1
            counts['reference:' + reference] += 1
            expected = None
            if outcome == 'accepted':
                # Independently prepare before reading any product semantic field.
                expected = Expected(data, case['sourceUri'], resources)
                actual = audit.model(p['events'])
                assert expected.events == actual, case['id'] + ' independent semantic gap'
                details = p['details']
                assert details.get('version') == expected.version and details.get('encoding') == expected.encoding
                assert details.get('standalone') == (None if expected.standalone is None else expected.standalone == 'yes')
                assert details.get('doctype') == expected.doctype
                unresolved = details['unresolved']
                assert all(u['anchorValid'] and u['childBoundary'] >= 0 for u in unresolved)
                assert [{k: u[k] for k in ('name', 'lexicalReference', 'parentNamespace', 'parentName', 'sourceUri', 'sequence')} for u in unresolved] == expected.unresolved
                assert details['incompleteDefaults'] == expected.incomplete_defaults, case['id'] + ' symbolic default gap'
                # Original accepted cases override or never apply these defaults.
                # Applied symbolic members remain explicit in this projection.
                assert not details['incompleteValues'], case['id'] + ' applied symbolic member needs an authored expectation'
                counts['symbolic-default-declarations'] += len(expected.incomplete_defaults)
                assert details['resourceUris'] == expected.opens, case['id'] + ' resource origin mismatch'
                assert details['disposals'] == p['opens'] == len(expected.opens)
                counts['independent:accepted-model'] += 1
                counts['unresolved-content-occurrences'] += len(unresolved)
                original_output(case, expected, originals, files, overrides, counts, output_witnesses)
                # Every callback pauses; alternate bulk primary decode boundaries.
                request['options'].update(chunk=4096, pause=True)
                resumed = await children[0].exchange(json.dumps(request, ensure_ascii=True))
                assert resumed['outcome'] == 'success'
                assert audit.model(resumed['events']) == expected.events
                assert resumed['details'] == details and resumed['opens'] == p['opens'], case['id'] + ' pause/transport mismatch'
                counts['bulk-decode-all-callback-pause-models'] += 1
                model_hashes[case['id']] = {'input': case['inputSha256'], 'events': hashed(expected.events),
                                          'provenance': hashed({k: details.get(k) for k in ('version', 'encoding', 'standalone', 'doctype', 'unresolved', 'incompleteDefaults', 'incompleteValues', 'resourceUris')})}
            else:
                assert p.get('events') is None and p.get('details') is None
                counts['independent:normative-rejection'] += 1
            if reference == 'accepted':
                rm = audit.model(r['events'])
                if expected is not None:
                    assert rm == expected.events, case['id'] + ' unresolved reference semantic disagreement'
                    counts['reference:independent-model-match'] += 1
            if reference not in case['expectedOutcomes']:
                # Exact identity-bound resolution: independent accepted model or
                # verified normative rejection classification, not a waiver.
                resolutions.append({'id': case['id'], 'inputSha256': case['inputSha256'], 'referenceOutcome': reference,
                                    'referenceObservation': r['diagnostics'][0]['message'] if r['diagnostics'] else 'accepted',
                                    'effectiveOutcome': outcome, 'rule': case['rule'], 'normative': case['normative'],
                                    'independentResolution': case['rationale'],
                                    'expectedModelSha256': hashed(expected.events) if expected else None})
        complete = True
    finally:
        cleanup = await asyncio.gather(*(child.close(complete) for child in children), return_exceptions=True)
        if any(isinstance(error, BaseException) for error in cleanup):
            raise RuntimeError('Failed child cleanup: ' + str(cleanup))
    assert freeze() == before, 'Source/output/runtime closure changed'
    counts['verified-original-resources'] = len(files)
    counts['resolved-reference-disagreements'] = len(resolutions)
    assert counts['independent:accepted-model'] == counts['product:accepted']
    assert counts['authored-effective10-output-overrides'] == len(overrides) == 15
    report.update(result='passed', counts=dict(counts), caseModelHashes=model_hashes,
                  originalOutputWitnesses=output_witnesses, referenceResolutions=resolutions,
                  provenance=before, childExitAndCleanup='passed', sourceOutputClosureUnchanged=True)


def main():
    path = OWN / (sys.argv[1] + '.json')
    assert not path.exists(), 'Preserve existing reports'
    summary = {'result': 'failed', 'scope': 'effective-xml10-events-v1 plus exact declaration/doctype, unresolved anchor, symbolic sidecar and resource ownership projection; lexical comments/PI/prefix/namespace declaration order covered by focused preservation tests'}
    try:
        asyncio.run(asyncio.wait_for(execute(summary), 180))
    except BaseException as error:
        summary['failure'] = str(error) or type(error).__name__
        raise
    finally:
        path.write_text(json.dumps(summary, ensure_ascii=True, indent=2) + '\n')
        print(json.dumps({k: summary[k] for k in ('result', 'counts', 'failure') if k in summary}))


if __name__ == '__main__':
    main()
