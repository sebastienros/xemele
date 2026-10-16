#!/usr/bin/env python3
"""Discover and validate API baseline fragments and corpus manifests, without an index."""
import argparse
import hashlib
import json
from pathlib import Path
import sys


class DiscoveryError(ValueError):
    pass


def read_json(text, location):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise DiscoveryError(f"{location}: duplicate JSON field {key}")
            result[key] = value
        return result
    try:
        return json.loads(text, object_pairs_hook=pairs)
    except json.JSONDecodeError as error:
        raise DiscoveryError(f"{location}: invalid JSON: {error.msg}") from error


def inside(path, root):
    resolved = path.resolve()
    if not resolved.is_relative_to(root):
        raise DiscoveryError(f"{path}: path escapes repository")
    return resolved


def discover(root, bootstrap_empty_api=False):
    root = root.resolve()
    if not root.is_dir():
        raise DiscoveryError(f"{root}: repository directory missing")
    api_files = sorted((root / 'docs/api').glob('*.txt'))
    declarations = {}
    members = {}
    api = []
    for path in api_files:
        inside(path, root)
        lines = path.read_text(encoding='utf-8').splitlines()
        if not lines or lines[0] != 'xemele-api-v1':
            raise DiscoveryError(f"{path}: unsupported baseline format; expected xemele-api-v1")
        previous = None
        for number, line in enumerate(lines[1:], 2):
            location = f"{path}:{number}"
            entry = read_json(line, location)
            if not isinstance(entry, dict) or set(entry) != {'id', 'member', 'declaration'}:
                raise DiscoveryError(f"{location}: expected id, member and declaration fields only")
            if any(not isinstance(value, str) or not value.strip() or '\n' in value or '\r' in value for value in entry.values()):
                raise DiscoveryError(f"{location}: fields must be nonempty single-line strings")
            canonical = json.dumps(entry, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
            if line != canonical:
                raise DiscoveryError(f"{location}: noncanonical JSON; expected {canonical}")
            identity = entry['id']
            if identity in declarations:
                reason = 'duplicate' if declarations[identity] == entry['declaration'] else 'conflicting'
                raise DiscoveryError(f"{location}: {reason} API id {identity}")
            member = entry['member']
            if len(member) < 3 or member[:2] not in ('T:', 'M:', 'P:', 'F:', 'E:') or any(c.isspace() for c in member):
                raise DiscoveryError(f'{location}: invalid canonical member identity')
            if member in members:
                reason = 'duplicate' if members[member] == entry['declaration'] else 'conflicting'
                raise DiscoveryError(f"{location}: {reason} API member {member}")
            members[member] = entry['declaration']
            if previous is not None and identity <= previous:
                raise DiscoveryError(f"{location}: API ids must be sorted")
            declarations[identity] = entry['declaration']
            previous = identity
        api.append(path.relative_to(root).as_posix())
    # Join tasks may own header-only fragments; the aggregate must still
    # describe a real surface, with every nonempty fragment validated above.
    if api_files and not declarations:
        raise DiscoveryError('empty API fragment aggregate: expected at least one declaration')
    if not api_files:
        runtime = root / 'src/Xemele'
        sources = [path for path in runtime.rglob('*.cs') if not {'bin', 'obj'}.intersection(path.relative_to(runtime).parts)]
        if not bootstrap_empty_api or not (runtime / 'Xemele.csproj').is_file() or sources:
            raise DiscoveryError('missing API baselines: expected docs/api/<name>.txt; bootstrap exemption requires an empty runtime project')
    corpora = []
    corpus_ids = set()
    fixture_paths = set()
    for path in sorted((root / 'tests/corpora').glob('*/manifest.json')):
        inside(path, root)
        manifest = read_json(path.read_text(encoding='utf-8'), str(path))
        if not isinstance(manifest, dict) or not isinstance(manifest.get('id'), str) or not manifest['id'].strip() or not isinstance(manifest.get('fixtures'), list) or not manifest['fixtures']:
            raise DiscoveryError(f"{path}: expected nonempty id and fixtures array")
        if type(manifest.get('schemaVersion')) is not int or manifest['schemaVersion'] != 1:
            raise DiscoveryError(f'{path}: unsupported corpus schemaVersion; expected 1')
        for field in ('sourceUrl', 'revision', 'sha256', 'license', 'notice', 'adapterVersion', 'profile'):
            if not isinstance(manifest.get(field), str) or not manifest[field].strip():
                raise DiscoveryError(f'{path}: missing corpus metadata {field}')
        if not manifest['sourceUrl'].startswith(('https://', 'http://')):
            raise DiscoveryError(f'{path}: sourceUrl must be an HTTP(S) URL')
        if manifest['revision'].lower() in ('main', 'master', 'latest', 'head'):
            raise DiscoveryError(f'{path}: revision must be an immutable version or commit')
        if len(manifest['sha256']) != 64 or any(c not in '0123456789abcdef' for c in manifest['sha256']):
            raise DiscoveryError(f'{path}: invalid corpus sha256')
        if manifest['id'] in corpus_ids:
            raise DiscoveryError(f"{path}: duplicate corpus id {manifest['id']}")
        corpus_ids.add(manifest['id'])
        for fixture in manifest['fixtures']:
            if not isinstance(fixture, dict) or not isinstance(fixture.get('path'), str) or not fixture['path']:
                raise DiscoveryError(f"{path}: fixture requires a path")
            if fixture.get('classification') not in ('accepted', 'rejected'):
                raise DiscoveryError(f'{path}: expected fixture classification accepted or rejected')
            relative = Path(fixture['path'])
            if relative.is_absolute():
                raise DiscoveryError(f"{path}: fixture path must be relative")
            actual = inside(path.parent / relative, root)
            if actual in fixture_paths:
                raise DiscoveryError(f"{path}: duplicate fixture path {fixture['path']}")
            fixture_paths.add(actual)
            if not actual.is_file():
                raise DiscoveryError(f"{path}: missing fixture {fixture['path']}")
            if 'sha256' not in fixture:
                raise DiscoveryError(f'{path}: fixture requires sha256')
            if 'sha256' in fixture:
                digest = fixture['sha256']
                if not isinstance(digest, str) or len(digest) != 64 or any(c not in '0123456789abcdef' for c in digest):
                    raise DiscoveryError(f"{path}: invalid fixture sha256")
                if hashlib.sha256(actual.read_bytes()).hexdigest() != digest:
                    raise DiscoveryError(f"{path}: checksum mismatch for {fixture['path']}")
        corpora.append(path.relative_to(root).as_posix())
    return {'formatVersion': 1, 'apiFragments': api, 'apiDeclarations': len(declarations), 'corpusManifests': corpora, 'fixtures': len(fixture_paths), 'bootstrapEmptyApi': not bool(api_files)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--bootstrap-empty-api', action='store_true', help='permit missing baselines only for an empty runtime project')
    args = parser.parse_args()
    try:
        print(json.dumps(discover(args.root, args.bootstrap_empty_api), sort_keys=True))
        return 0
    except (DiscoveryError, OSError, UnicodeError) as error:
        print(f'discovery error: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
