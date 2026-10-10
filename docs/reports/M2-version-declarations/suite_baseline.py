"""Exact identity preservation; an optional verified-main compiled discovery adds only known rows."""
import hashlib, json
SUITES = {
    'Xemele.Conformance.Tests': (255, 'a43104c9986029e0680218003fc7d820c2583c42b53fe16a323f2cab04e634dc', 3),
    'Xemele.Differential.Tests': (299, '845b5c10e9d4952e309b2d419fcc429fe568c8496889dce7fe6ab319389cdc6e', 12)
}
def identity_digest(ids):
    return hashlib.sha256(json.dumps(sorted(ids),ensure_ascii=True,separators=(',',':')).encode()).hexdigest()
def discovery_name(name):
    # Only runner quoting of string delimiters differs; actual XML/input escapes stay unchanged.
    return name.replace(chr(92) + chr(34), chr(34))
def validate_suite(assembly, ids, baseline=None):
    count,digest,added=SUITES[assembly]
    assert len(ids)==len(set(ids)), 'Duplicate suite identities'
    actual=set(ids);additions=set();expected_additions=[]
    if baseline is not None:
        record=baseline['suites'][assembly]
        assert record['discoveryExit']==0 and record['total']==count+added and record['inheritedDiscoveryCount']==count
        assert record['historicalExecutionIdentitySha256']==digest
        expected_additions=[tuple(row) for row in record['addedExactIdentities']]
        assert all(len(row)==3 and all(isinstance(v,str) for v in row) for row in expected_additions), 'Malformed exact profile identities'
        expected={(t,m,discovery_name(n)) for t,m,n in expected_additions}
        assert len(expected_additions)==len(expected)==added
        for identity in actual:
            t,m,n=identity
            if (t,m,discovery_name(n)) in expected:additions.add(identity)
        assert len(additions)==added and {(t,m,discovery_name(n)) for t,m,n in additions}==expected, 'Exact compiled profile additions differ'
    assert len(actual)==count+len(additions)
    inherited=actual-additions
    assert len(inherited)==count and identity_digest(inherited)==digest, 'Exact historic suite XML identity digest differs'
    return {'total':len(actual),'identitySha256':identity_digest(actual),'inherited':count,'inheritedIdentitySha256':digest,'exactCompiledDiscoveryAdditions':expected_additions,'exactExecutionAdditions':sorted(additions)}
