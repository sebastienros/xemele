#!/usr/bin/env python3
"""Independent source/fixture SHA-256 and original bootstrap expectation checks."""
import base64
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def verify(root):
    directory = Path(root) / 'tests/corpora/bootstrap'
    manifest = json.loads((directory / 'manifest.json').read_text())
    payload = (directory / manifest['sourcePath']).read_bytes()
    digest = hashlib.sha256(payload).hexdigest()
    assert digest == manifest['sha256']
    assert manifest['revision'] == 'sha256:' + digest
    assert manifest['license'] == 'MIT'
    assert 'MIT License' in (directory / 'NOTICE.txt').read_text()
    bundle = json.loads(payload)
    assert bundle['schemaVersion'] == manifest['schemaVersion'] == 1
    files = {entry['path']: base64.b64decode(entry['base64'], validate=True) for entry in bundle['files']}
    assert len(files) == len(bundle['files']) == len(manifest['fixtures'])
    counts = {'accepted': 0, 'rejected': 0}
    for fixture in manifest['fixtures']:
        actual = (directory / fixture['path']).read_bytes()
        assert actual == files[fixture['path']]
        assert hashlib.sha256(actual).hexdigest() == fixture['sha256']
        try:
            ET.fromstring(actual)
            classification = 'accepted'
        except ET.ParseError:
            classification = 'rejected'
        assert classification == fixture['classification']
        counts[classification] += 1
    print(json.dumps({'sourceSha256': digest, 'fixturesVerified': len(files),
                      'independentElementTreeCrossCheck': counts,
                      'xemeleParserCasesExecuted': 0}, sort_keys=True))


if __name__ == '__main__':
    verify(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).resolve().parents[3])
