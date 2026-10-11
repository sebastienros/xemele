#!/usr/bin/env python3
"""Exact intended wrapper controls; no allocation or benchmark execution."""
import pathlib,subprocess,os
root=pathlib.Path(__file__).resolve().parents[3]
artifact=root/'artifacts/M7-html-model';artifact.mkdir(parents=True,exist_ok=True)
freeze=['python3',str(root/'docs/reports/M7-html-model/freeze.py'),'capture',str(artifact/'prepared.json')]
wrapper=['python3',str(root/'docs/reports/M7-html-model/run_measurements.py'),'--identity-control']
subprocess.run(freeze,cwd=root,check=True)
subprocess.run(wrapper,cwd=root,check=True)
negative=dict(os.environ);negative['XEMELE_UNKNOWN_IDENTITY_CONTROL']='must-reject'
result=subprocess.run(wrapper,cwd=root,env=negative)
if result.returncode==0:raise RuntimeError('Unknown effective environment was accepted')
subprocess.run(wrapper,cwd=root,check=True)
print('Exact-wrapper positive/final-positive accepted; unknown-env negative rejected; no measurements.')
