#!/usr/bin/env python3
"""Run only under a prepared exclusive coordinator grant; never builds."""
import json
import os
import pathlib
import subprocess
import sys
root = pathlib.Path(__file__).resolve().parents[3]
manifest = root / 'artifacts/M4-navigation/prepared.json'
plan = json.loads((root / 'docs/reports/M4-navigation/measurement-plan.json').read_text())
if sys.argv[1:] != ['--exclusive-grant']:
    raise SystemExit('Requires the coordinator prepared exclusive grant; this flag is not itself authorization.')
subprocess.run(['python3', str(root / 'docs/reports/M4-navigation/freeze.py'), 'check', str(manifest)], cwd=root, check=True)
environment = dict(os.environ)
environment.update(plan['environment'])
environment['XEMELE_NAVIGATION_REVISION'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
for run in plan['runs']:
    with (root / run['log']).open('w') as output:
        subprocess.run(run['command'], cwd=root, env=environment, stdout=output, stderr=subprocess.STDOUT, timeout=run['max_seconds'], check=True)
    print(run['name'] + ' completed', flush=True)
subprocess.run(['python3', str(root / 'docs/reports/M4-navigation/freeze.py'), 'check', str(manifest)], cwd=root, check=True)
