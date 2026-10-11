#!/usr/bin/env python3
"""Reuse verified bounded wrapper unchanged except owned task path routing."""
import pathlib
root=pathlib.Path(__file__).resolve().parents[3]
source=root/'docs/reports/M7-html-model/run_measurements.py'
# No predecessor file is edited. Its lifecycle/deadline/group cleanup is reused.
exec(compile(source.read_text().replace('M7-html-model','M7-html-pi-model'),str(source),'exec'))
