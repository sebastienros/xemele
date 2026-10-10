"""Pinned metadata-only W3C importer. Never invokes Xemele or parses test inputs."""
import argparse
import base64
from collections import Counter
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import tarfile
from urllib.parse import urljoin, urlparse
from xml.parsers import expat

ROOT = Path(__file__).resolve().parents[3]
CORPUS = ROOT / 'tests/corpora/xml'
URL = 'https://www.w3.org/XML/Test/xmlts20130923.tar.gz'
ARCHIVE_HASH = '9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f'
HOST = 'https://archive.invalid/'
PROFILE = 'w3c-xml-20130923-inventory-v1'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def dump(value):
    return (json.dumps(value, ensure_ascii=True, indent=2) + '\n').encode()


def archive_files(data):
    if sha(data) != ARCHIVE_HASH:
        raise ValueError('Pinned archive SHA-256 mismatch')
    files = {}
    members = []
    with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as archive:
        for member in archive:
            path = PurePosixPath(member.name)
            if path.is_absolute() or '..' in path.parts or path.parts[0] != 'xmlconf':
                raise ValueError('Archive path escapes xmlconf')
            if not (member.isdir() or member.isfile()):
                raise ValueError('Archive links/special files prohibited')
            members.append({'path': member.name, 'kind': 'directory' if member.isdir() else 'file', 'size': member.size})
            if member.isfile():
                if member.name in files:
                    raise ValueError('Duplicate archive file')
                files[member.name] = archive.extractfile(member).read()
    return files, members


def contained(uri):
    parsed = urlparse(uri)
    if parsed.scheme != 'https' or parsed.netloc != 'archive.invalid' or parsed.query or parsed.fragment:
        raise ValueError('Metadata URI escapes pinned archive: ' + uri)
    path = parsed.path.lstrip('/')
    if not path.startswith('xmlconf/') or '..' in PurePosixPath(path).parts:
        raise ValueError('Metadata URI escapes xmlconf')
    return path


def discover(files):
    cases, stack, metadata = [], [], set()
    active = []
    current = None

    def configure(parser, source):
        parser.SetBase(HOST + source)
        parser.SetParamEntityParsing(expat.XML_PARAM_ENTITY_PARSING_ALWAYS)
        metadata.add(source)

        def start(name, attrs):
            nonlocal current
            # External parsed entities establish their own document base URI.
            base = stack[-1][1] if stack and stack[-1][2] == source else HOST + source
            base = urljoin(base, attrs.get('xml:base', ''))
            stack.append((name, base, source))
            if name == 'TEST':
                for key in ['ID', 'TYPE', 'URI', 'SECTIONS']:
                    if not attrs.get(key):
                        raise ValueError('Missing test metadata ' + key)
                if attrs['TYPE'] not in ('valid', 'invalid', 'not-wf', 'error'):
                    raise ValueError('Unknown source TYPE')
                current = {'id': source + '#' + attrs['ID'], 'sourceId': attrs['ID'],
                           'metadataPath': source, 'metadataLine': parser.CurrentLineNumber,
                           'attributes': dict(sorted(attrs.items())), 'inputPath': contained(urljoin(base, attrs['URI'])),
                           'outputPaths': {k: contained(urljoin(base, attrs[k])) for k in ('OUTPUT', 'OUTPUT3') if k in attrs},
                           'description': ''}
                cases.append(current)

        def end(name):
            nonlocal current
            if name == 'TEST':
                current['description'] = ' '.join(current['description'].split())
                current = None
            stack.pop()

        def text(value):
            if current is not None:
                current['description'] += value

        def external(context, base, system, public):
            path = contained(urljoin(base, system))
            if path not in files:
                raise ValueError('Missing metadata resource ' + path)
            if path in active:
                raise ValueError('Recursive metadata entity')
            child = parser.ExternalEntityParserCreate(context)
            configure(child, path)
            active.append(path)
            child.Parse(files[path], True)
            active.pop()
            return 1

        parser.StartElementHandler = start
        parser.EndElementHandler = end
        parser.CharacterDataHandler = text
        parser.ExternalEntityRefHandler = external

    parser = expat.ParserCreate()
    configure(parser, 'xmlconf/xmlconf.xml')
    parser.Parse(files['xmlconf/xmlconf.xml'], True)
    if len({c['id'] for c in cases}) != len(cases):
        raise ValueError('Duplicate authoritative source identity')
    for case in cases:
        if case['inputPath'] not in files:
            raise ValueError('Missing primary fixture ' + case['inputPath'])
        case['inputSha256'] = sha(files[case['inputPath']])
        case['missingOutputs'] = [p for p in case['outputPaths'].values() if p not in files]
    return cases, sorted(metadata)


def lexical_view(data):
    # Inspection only: tolerant decoding preserves malformed tests as rejection cases.
    if data.startswith((b'\xff\xfe', b'\xfe\xff')):
        return data.decode('utf-16', errors='replace')
    if data.startswith(b'\x00<\x00?'):
        return data.decode('utf-16be', errors='replace')
    if data.startswith(b'<\x00?\x00'):
        return data.decode('utf-16le', errors='replace')
    return data.decode('utf-8-sig', errors='replace')


def classify(case, data):
    attrs = case['attributes']
    text = lexical_view(data)
    # Do not mistake literal strings inside comments/CDATA/PIs for feature syntax.
    syntax = re.sub(r'<!--.*?(?:-->|$)|<!\[CDATA\[.*?(?:\]\]>|$)|<\?(?!xml\b).*?(?:\?>|$)', '', text, flags=re.S)
    features = []
    if 'VERSION' in attrs and '1.0' not in attrs['VERSION'].split():
        features.append('xml-1.1-or-other-version')
    if attrs.get('RECOMMENDATION') == 'NS1.1':
        features.append('namespaces-1.1')
    if 'EDITION' in attrs and '5' not in attrs['EDITION'].split() and attrs.get('RECOMMENDATION', 'XML1.0').startswith('XML'):
        features.append('historical-xml-edition')
    if attrs.get('NAMESPACE', attrs.get('NAMESPACES', 'yes')) == 'no':
        features.append('namespace-processing-disabled')
    if re.search(r'<!DOCTYPE\b', syntax):
        features.append('dtd-prohibited-in-m1')
    if attrs.get('ENTITIES', 'none') != 'none':
        features.append('external-' + attrs['ENTITIES'] + '-entities')
    if attrs['TYPE'] == 'invalid':
        features.append('dtd-validity-oracle')
    if attrs['TYPE'] == 'error':
        features.append('optional-error-no-binary-oracle')
    encoding = re.match(r'\s*<\?xml\b[^?]*\bencoding\s*=\s*([\'"])(.*?)\1', text, flags=re.S)
    if encoding and re.fullmatch(r'[A-Za-z][A-Za-z0-9._-]*', encoding[2]) and encoding[2].upper() not in ('UTF-8', 'UTF-16', 'UTF-16LE', 'UTF-16BE'):
        features.append('encoding-outside-m1:' + encoding[2])
    if data.startswith((b'\x00\x00\xfe\xff', b'\xff\xfe\x00\x00', b'\x00\x00\x00<', b'<\x00\x00\x00', b'\x4c\x6f\xa7\x94')):
        features.append('byte-signature-outside-m1')
    # Unknown entities without DTD remain supported rejection syntax, not a gap.
    outcome = 'unsupported-profile' if features else ('accepted' if attrs['TYPE'] == 'valid' else 'rejected')
    expected_m1 = None
    # Catalog feature inspection cannot establish first-error ordering across
    # declaration/encoding/version and grammar failures. Leave that diagnostic
    # unasserted rather than predicting a later DOCTYPE rejection.
    earlier_scope_gap = any(f.startswith(('xml-1.1', 'encoding-outside', 'byte-signature')) for f in features)
    if not earlier_scope_gap and 'dtd-prohibited-in-m1' in features:
        expected_m1 = 'rejected:XML.DtdProhibited'
    return {'outcome': outcome, 'reasons': features or ['xml-1.0-fifth-edition-m1-' + outcome],
            'contractRejection': expected_m1, 'productExecuted': False, 'standardsPassed': False}


def generate(data):
    files, members = archive_files(data)
    cases, metadata = discover(files)
    for case in cases:
        case['m1'] = classify(case, files[case['inputPath']])
    bundle = dump({'schemaVersion': 1, 'files': [{'path': p, 'base64': base64.b64encode(b).decode()} for p,b in sorted(files.items())]})
    manifest = {'schemaVersion': 1, 'id': 'w3c-xml-20130923', 'sourceUrl': URL,
                'revision': '20130923;archive-sha256:' + ARCHIVE_HASH, 'sha256': sha(bundle),
                'license': 'W3C-19980720; bundled contributor notices retained; redistribution conflict unresolved',
                'notice': 'NOTICE.md; archive bytes unchanged; generated transport bundle for local internal use only',
                'adapterVersion': 'xemele-offline-bundle-v1', 'profile': PROFILE, 'sourcePath': 'source.bundle.json',
                'fixtures': [{'path': p, 'sha256': sha(b), 'classification': 'accepted'} for p,b in sorted(files.items())]}
    inventory = {'schemaVersion': 1, 'sourceUrl': URL, 'archiveSha256': ARCHIVE_HASH,
                 'archiveBytes': len(data), 'archiveMembers': members, 'metadataDocuments': metadata,
                 'identityRule': 'metadataPath#ID; no identities deduplicated; primary bytes may be shared',
                 'files': [{'path': p, 'bytes': len(b), 'sha256': sha(b)} for p,b in sorted(files.items())],
                 'cases': cases, 'summary': {'cases': len(cases), 'files': len(files),
                 'sourceTypes': dict(sorted(Counter(c['attributes']['TYPE'] for c in cases).items())),
                 'm1Outcomes': dict(sorted(Counter(c['m1']['outcome'] for c in cases).items())),
                 'featureReasons': dict(sorted(Counter(r for c in cases if c['m1']['outcome'] == 'unsupported-profile' for r in c['m1']['reasons']).items())),
                 'externalEntityCases': sum(c['attributes'].get('ENTITIES', 'none') != 'none' for c in cases),
                 'versionAttributes': dict(sorted(Counter(c['attributes'].get('VERSION', 'all') for c in cases).items())),
                 'missingOutputReferences': sum(len(c['missingOutputs']) for c in cases),
                 'parserCasesExecuted': 0, 'standardsPassed': 0}}
    return manifest, inventory, bundle


def materialize(files, directory):
    directory = directory.resolve()
    for name, data in files.items():
        path = directory / name
        if not path.resolve().is_relative_to(directory):
            raise ValueError('Resource destination escapes directory')
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        if sha(path.read_bytes()) != sha(data):
            raise ValueError('Materialized resource checksum mismatch')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    data = (CORPUS / 'xmlts20130923.tar.gz').read_bytes()
    manifest, inventory, bundle = generate(data)
    for name, content in [('manifest.json', dump(manifest)), ('inventory.json', dump(inventory)), ('source.bundle.json', bundle)]:
        path = CORPUS / name
        if args.check:
            if path.read_bytes() != content:
                raise ValueError('Nonreproducible ' + name)
        else:
            path.write_bytes(content)
    if not args.check:
        materialize(archive_files(data)[0], CORPUS)
    else:
        for file in inventory['files']:
            if sha((CORPUS / file['path']).read_bytes()) != file['sha256']:
                raise ValueError('Missing/corrupt materialized resource')
    print(json.dumps(inventory['summary'], sort_keys=True))

if __name__ == '__main__':
    main()
