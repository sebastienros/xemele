#!/usr/bin/env python3
"""Provision the existing pinned XML corpus for clean-checkout CI checks."""
import argparse
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/Packaging'))
from hosted_corpus import prepare


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, help='reuse a pinned local archive without downloading')
    args = parser.parse_args()
    work = ROOT / 'artifacts/M9-ci-maintenance'
    work.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='corpus-', dir=work) as temporary:
        resources, report = prepare(ROOT, Path(temporary), args.archive)
        destination = ROOT / 'tests/corpora/xml/xmlconf'
        shutil.copytree(resources / 'xmlconf', destination, dirs_exist_ok=True)
    resource_root = destination.parent
    if os.environ.get('GITHUB_ENV'):
        with open(os.environ['GITHUB_ENV'], 'a', encoding='utf-8') as environment:
            environment.write(f'XEMELE_CORPUS_RESOURCES={resource_root}\n')
    print(json.dumps(dict(report, resourceRoot=str(resource_root)), sort_keys=True))


if __name__ == '__main__':
    main()
