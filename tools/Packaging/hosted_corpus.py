"""Provision exact upstream XML bytes into the owned hosted job directory."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import tarfile
import time
import urllib.request


def sha(payload):
    return hashlib.sha256(payload).hexdigest()


def prepare(root, work, archive=None):
    manifest_path = root / 'tests/corpora/xml/manifest.json'
    inventory_path = root / 'tests/corpora/xml/inventory.json'
    manifest = json.loads(manifest_path.read_bytes())
    inventory = json.loads(inventory_path.read_bytes())
    expected = '9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f'
    assert manifest['revision'].endswith('archive-sha256:' + expected)
    assert manifest['sourceUrl'] == 'https://www.w3.org/XML/Test/xmlts20130923.tar.gz'
    if archive is None:
        archive = work / 'xmlts20130923.tar.gz'
        deadline = time.monotonic() + 120
        with urllib.request.urlopen(manifest['sourceUrl'], timeout=15) as response, archive.open('xb') as output:
            size = 0
            while chunk := response.read(65536):
                size += len(chunk)
                assert size <= 2 * 1024 * 1024 and time.monotonic() < deadline, 'Pinned archive download exceeded its bounds'
                output.write(chunk)
    archive = Path(archive)
    assert sha(archive.read_bytes()) == expected, 'Pinned archive SHA-256 mismatch'
    resources = work / 'corpus-resources'
    resources.mkdir()
    records = inventory['files']
    assert len(records) == 3386 and len({r['path'] for r in records}) == len(records)
    with tarfile.open(archive, 'r:gz') as source:
        members = {m.name: m for m in source.getmembers()}
        for record in records:
            name = record['path']
            relative = PurePosixPath(name)
            assert not relative.is_absolute() and '..' not in relative.parts and '\\' not in name
            member = members[name]
            assert member.isfile() and member.size == record['bytes']
            with source.extractfile(member) as stream:
                payload = stream.read()
            assert len(payload) == record['bytes'] and sha(payload) == record['sha256'], name
            destination = resources.joinpath(*relative.parts)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(payload)
    return resources, dict(sourceUrl=manifest['sourceUrl'], archiveSha256=expected,
                           manifestSha256=sha(manifest_path.read_bytes()), inventorySha256=sha(inventory_path.read_bytes()),
                           files=len(records), exactOriginalBytes=True, parserCasesExecuted=0)
