"""Rehydrate full source locally, preserving a private offline cache across worktree deletion."""
import argparse
import base64
import json
from pathlib import Path
import shutil
import subprocess
import urllib.request
import import_suite as suite

CACHE = Path('/private/tmp/xemele-corpus-cache/w3c-xml-20130923')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--download', action='store_true', help='fetch official pinned archive if local/cached copy is absent')
    parser.add_argument('--preserve', action='store_true', help='copy archive/bundle and materialize full URI resource tree outside worktree')
    args = parser.parse_args()
    archive = suite.CORPUS / 'xmlts20130923.tar.gz'
    saved = CACHE / archive.name
    if not archive.exists():
        if saved.exists():
            shutil.copyfile(saved, archive)
        elif args.download:
            data = urllib.request.urlopen(suite.URL, timeout=60).read()
            suite.archive_files(data)  # pin/containment before any archive content is used
            archive.write_bytes(data)
        else:
            raise FileNotFoundError('Pinned archive absent; restore preserved private cache or explicitly use --download')
    manifest, inventory, bundle = suite.generate(archive.read_bytes())
    for name, content in [('manifest.json', suite.dump(manifest)), ('inventory.json', suite.dump(inventory))]:
        if (suite.CORPUS / name).read_bytes() != content:
            raise ValueError('Committed metadata differs from pinned source: ' + name)
    (suite.CORPUS / 'source.bundle.json').write_bytes(bundle)
    suite.materialize(suite.archive_files(archive.read_bytes())[0], suite.CORPUS)
    if args.preserve:
        module = CACHE / 'tests/corpora/xml'
        module.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(archive, saved)
        for name in ['manifest.json', 'inventory.json', 'NOTICE.md', 'source.bundle.json']:
            shutil.copyfile(suite.CORPUS / name, module / name)
        files, _ = suite.archive_files(archive.read_bytes())
        suite.materialize(files, CACHE / 'resources')
        runner = suite.ROOT / 'docs/reports/M1-corpus-import/Runner/bin/Release/net10.0/Xemele.CorpusRunner.dll'
        subprocess.run(['dotnet', str(runner), 'import', str(CACHE), suite.PROFILE, str(CACHE / 'cache')], check=True, stdout=subprocess.DEVNULL)
        subprocess.run(['dotnet', str(runner), 'provision', str(CACHE), suite.PROFILE, str(CACHE / 'cache')], check=True, stdout=subprocess.DEVNULL)
        print(json.dumps({'preservedCache': str(CACHE), 'resourceRoot': str(CACHE / 'resources'),
                          'verifiedFiles': len(files), 'parserCasesExecuted': 0, 'standardsPassed': 0}))
    else:
        print('Pinned local transport bundle verified/recreated; no network needed when archive/cache exists.')

if __name__ == '__main__': main()
