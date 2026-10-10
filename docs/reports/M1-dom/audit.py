#!/usr/bin/env python3
"""Explicit committed, staged, working and untracked DOM ownership audit."""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--base', required=True, help='Coordination claim before this feature or integration.')
parser.add_argument('--transient', action='store_true', help='Write result to ignored artifacts rather than the tracked report.')
args = parser.parse_args()
def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True).strip()
task = json.loads((ROOT / 'docs/tasks/M1-dom.json').read_text())
groups = {
    'committed': git('diff', '--name-only', args.base, 'HEAD').splitlines(),
    'staged': git('diff', '--cached', '--name-only').splitlines(),
    'working': git('diff', '--name-only').splitlines(),
    'untracked': git('ls-files', '--others', '--exclude-standard').splitlines(),
}
for group, paths in groups.items():
    for path in paths:
        assert path == 'docs/tasks/M1-dom.json' or any(path.startswith(p) if p.endswith('/') else path == p for p in task['owns']), (group, path)
for arguments in [('diff', '--check', args.base, 'HEAD'), ('diff', '--check', '--cached'), ('diff', '--check')]:
    subprocess.run(['git', *arguments], cwd=ROOT, check=True)
result = {'base': args.base, 'revision': git('rev-parse', 'HEAD'), 'paths': groups, 'result': 'passed'}
destination = ROOT / ('artifacts/M1-dom/final-owned-audit.json' if args.transient else 'docs/reports/M1-dom/final-owned-audit.json')
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(json.dumps(result, indent=2) + '\n')
print('Owned committed/staged/working/untracked and whitespace audit passed at ' + result['revision'])
