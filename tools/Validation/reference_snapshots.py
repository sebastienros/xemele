"""Capture independent RNG results; never derive expectations from Xemele."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
CORPUS = ROOT / 'tests/corpora/relaxng'
FROZEN = ROOT / 'tools/ReferenceRunners/RelaxNg'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    identity_path = Path(os.environ.get('XEMELE_RNG_REFERENCE_IDENTITY',
                         ROOT / 'artifacts/M9-ci-maintenance/references/reference-build.json'))
    identities = json.loads(identity_path.read_text(encoding='utf-8'))
    frozen = json.loads((FROZEN / 'reference-build.json').read_text(encoding='utf-8'))
    inputs = {}
    cases = []
    for name in ['jing-inventory.json', 'libxml2-inventory.json', 'profile-inventory.json']:
        path = CORPUS / name
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        inventory = json.loads(path.read_text(encoding='utf-8'))
        cases.extend(inventory['cases'])
        for fixture in inventory['resources'] + [f for c in inventory['cases']
                                                 for f in [c['schema'], *c['instances']]]:
            path = CORPUS / fixture['path']
            assert path.resolve().is_relative_to(CORPUS.resolve())
            assert sha(path) == fixture['sha256']
            inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    requests = ['\t'.join([case['id'], str(CORPUS / case['schema']['path']),
                          *[str(CORPUS / fixture['path']) for fixture in case['instances']]])
                for case in cases]
    for engine in ['jing', 'libxml2']:
        identity = identities[engine]
        for key in ['revision', 'version', 'sourceSha256']:
            assert identity[key] == frozen[engine][key], f'{engine} pin differs: {key}'
        runner = FROZEN / ('RngReference.java' if engine == 'jing' else 'libxml2_reference.c')
        assert sha(runner) == identity['sourceSha256']
        if engine == 'jing':
            assert sha(Path(identity['jarPath'])) == identity['jarSha256']
            for name, digest in identity['runnerClassHashes'].items():
                assert sha(Path(identity['classPath']) / name) == digest
            command = [os.environ.get('XEMELE_JAVA', 'java'), '-cp',
                       identity['classPath'] + os.pathsep + identity['jarPath'], 'RngReference']
        else:
            assert sha(Path(identity['binaryPath'])) == identity['binarySha256']
            command = [identity['binaryPath']]
        with tempfile.TemporaryDirectory(prefix='xemele-reference-') as temporary:
            path = Path(temporary) / 'requests.tsv'
            path.write_text('\n'.join(requests) + '\n', encoding='utf-8')
            result = subprocess.run([*command, str(path)], check=True, capture_output=True,
                                    text=True, encoding='utf-8', timeout=120)
        rows = [dict(zip(['caseId', 'phase', 'outcome'], line.split('\t'), strict=True))
                for line in result.stdout.splitlines()]
        expected_count = sum(1 + len(case['instances']) for case in cases)
        assert len(rows) == expected_count
        assert len({(row['caseId'], row['phase']) for row in rows}) == expected_count
        snapshot = dict(schemaVersion=1, engine=engine,
                        identity={key: identity[key] for key in ['revision', 'version', 'sourceSha256']},
                        inputs=inputs, results=sorted(rows, key=lambda row: (row['caseId'], row['phase'])))
        destination = CORPUS / f'reference-results-{engine}.json'
        destination.write_text(json.dumps(snapshot, indent=2, sort_keys=True) + '\n', encoding='utf-8')
        print(f'{engine}: captured {len(rows)} independent outcomes -> {destination.relative_to(ROOT)}')


if __name__ == '__main__':
    main()
