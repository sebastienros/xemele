#!/usr/bin/env python3
"""Reuse exact-wrapper positive/unknown-environment negative controls read-only."""
import pathlib
root=pathlib.Path(__file__).resolve().parents[3]
source=root/'docs/reports/M7-html-model/prepare_identity.py'
exec(compile(source.read_text().replace('M7-html-model','M7-html-pi-model'),str(source),'exec'))
