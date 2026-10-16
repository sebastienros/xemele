#!/usr/bin/env python3
"""Compare validated API fragments to a compiled assembly; never update baselines."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

from discover import DiscoveryError, discover, read_json

ROOT = Path(__file__).resolve().parents[2]
CHECKER = ROOT / 'tools/ApiBaseline/Checker/bin/Release/net10.0/ApiCheck.dll'


def compose(root, bootstrap_empty_api=False):
    root = root.resolve()
    inventory = discover(root, bootstrap_empty_api)
    provenance = []
    for source in inventory['apiFragments']:
        for number, line in enumerate((root / source).read_text(encoding='utf-8').splitlines()[1:], 2):
            entry = read_json(line, f'{source}:{number}')
            provenance.append(dict(entry, source=source, line=number))
    provenance.sort(key=lambda entry: entry['member'])
    # Approved extractor emits id=member; inventory IDs survive in provenance.
    canonical = 'xemele-api-v1\n' + ''.join(
        json.dumps(dict(declaration=row['declaration'], id=row['member'], member=row['member']),
                   ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n'
        for row in provenance)
    return canonical, dict(inventory=inventory, provenance=provenance)


def compare(root, assembly, checker=CHECKER, bootstrap_empty_api=False):
    canonical, report = compose(root, bootstrap_empty_api)
    assembly, checker = assembly.resolve(), checker.resolve()
    if not assembly.is_file() or not checker.is_file():
        raise DiscoveryError('compiled assembly/checker missing; build both in Release first')
    report['assembly'] = str(assembly)
    report['assemblySha256'] = hashlib.sha256(assembly.read_bytes()).hexdigest()
    report['checker'] = str(checker)
    # Temporary bridge to the unchanged approved checker; no tracked union/index.
    with tempfile.TemporaryDirectory(prefix='xemele-api-union-') as directory:
        fragment = Path(directory) / 'union.txt'
        fragment.write_text(canonical, encoding='utf-8', newline='\n')
        result = subprocess.run(['dotnet', str(checker), 'compare', str(assembly), str(fragment)],
                                capture_output=True, text=True, timeout=60)
    report['matched'] = result.returncode == 0
    report['checkerExit'] = result.returncode
    report['checkerOutput'] = (result.stdout + result.stderr).strip()
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--assembly', type=Path, default=ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll')
    parser.add_argument('--checker', type=Path, default=CHECKER)
    parser.add_argument('--bootstrap-empty-api', action='store_true')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    try:
        report = compare(args.root, args.assembly, args.checker, args.bootstrap_empty_api)
        output = json.dumps(report, ensure_ascii=False, sort_keys=True, indent=2) + '\n'
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(output, encoding='utf-8')
        if args.report:
            print(json.dumps(dict(matched=report['matched'], members=len(report['provenance']),
                                  bootstrapEmptyApi=report['inventory']['bootstrapEmptyApi'],
                                  checkerOutput=report['checkerOutput'], report=str(args.report)), sort_keys=True))
        else:
            print(output, end='')
        return 0 if report['matched'] else 1
    except (DiscoveryError, OSError, UnicodeError, subprocess.TimeoutExpired) as error:
        print(f'API comparison error: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
