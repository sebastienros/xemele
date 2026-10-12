"""One prepared allocation child under an exact coordinator grant; no builds/timing."""
import argparse
import hashlib
import json
import os
import subprocess
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--grant', required=True, help='Exact grant identifier received from the coordinator')
a = p.parse_args()
root = Path(__file__).resolve().parents[3]
manifest_path = root / 'docs/reports/M8-xsd-fixed-restriction/preparation.json'
manifest = json.loads(manifest_path.read_text())
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
# Evidence-only commits may follow preparation. Runtime/driver/test/input
# identities must still be unchanged; the exact prepared source remains recorded.
subprocess.run(['git', 'merge-base', '--is-ancestor', manifest['sourceRevision'], revision], cwd=root, check=True)
for name, expected in manifest['identities'].items():
    path = Path(name) if Path(name).is_absolute() else root / name
    with path.open('rb') as stream: actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if actual != expected: raise AssertionError('Prepared identity changed: ' + name)
env = os.environ.copy(); env.update(manifest['environment'])
result = subprocess.run(manifest['command'], cwd=root, env=env, timeout=manifest['workSeconds'])
if result.returncode: raise SystemExit(result.returncode)
# Retain no child after returning. Numeric output is opened only by later
# source-independent analysis, once cleanup is confirmed to the coordinator.
print('Allocation child exited 0; no active child/jobs; grant ' + a.grant)
