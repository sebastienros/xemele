#!/usr/bin/env python3
"""Negative controls for interrupted report inspection/cleanup; no product rerun."""
import json
from pathlib import Path
import tempfile
from unittest.mock import patch
from verify import finish_raw_outputs


def main():
    assertions = 0
    def check(value):
        nonlocal assertions
        assert value
        assertions += 1
    with tempfile.TemporaryDirectory(prefix='version-cleanup-control-') as directory:
        base = Path(directory)
        root = base/'artifacts'; root.mkdir()
        historical = root/'historical.report.json'; historical.write_text('{historical unchanged}')
        before = set(root.rglob('*'))
        child = root/'new-child'; child.mkdir()
        malformed = child/'real.report.json'; malformed.write_text('{"Mode":')
        blocked = child/'blocked.log'; blocked.write_text('injected removal failure')
        ordinary = child/'ordinary.log'; ordinary.write_text('must still be removed')
        attempted = []
        original_unlink = Path.unlink
        def injected_unlink(path, *args, **kwargs):
            attempted.append(path)
            if path == blocked: raise PermissionError('negative control cleanup denial')
            return original_unlink(path, *args, **kwargs)
        summary = {'result': 'failed', 'failure': 'primary check timeout'}
        with patch.object(Path, 'unlink', injected_unlink):
            finish_raw_outputs(root, before, True, summary)
        check(summary['failure'] == 'primary check timeout')
        check(summary['result'] == 'failed')
        check(len(summary['inspectionErrors']) == 1)
        check(any('negative control cleanup denial' in e['error'] for e in summary['cleanupErrors']))
        check(summary['newRawOutputsRemoved'] is False)
        check(malformed in attempted and ordinary in attempted and blocked in attempted)
        check(not malformed.exists() and not ordinary.exists() and blocked.exists())
        check(historical.read_text() == '{historical unchanged}')
        check(json.loads(json.dumps(summary))['failure'] == 'primary check timeout')
        # An otherwise passing run must become a failure on cleanup denial.
        passing = {'result': 'passed'}
        with patch.object(Path, 'unlink', injected_unlink):
            finish_raw_outputs(root, before, True, passing)
        check(passing['result'] == 'failed' and passing['cleanupErrors'])
        check(passing['newRawOutputsRemoved'] is False)
        # Once filesystem denial ends, remaining paths are removable and history survives.
        repaired = {'result': 'passed'}
        finish_raw_outputs(root, before, True, repaired)
        check(repaired['result'] == 'passed' and repaired['newRawOutputsRemoved'])
        check(set(root.rglob('*')) == before)
        # Malformed JSON alone fails inspection but never prevents cleanup or summary.
        child.mkdir(); malformed.write_text('{')
        malformed_only = {'result': 'passed'}
        finish_raw_outputs(root, before, True, malformed_only)
        check(malformed_only['result'] == 'failed' and malformed_only['inspectionErrors'])
        check(not malformed_only['cleanupErrors'] and malformed_only['newRawOutputsRemoved'])
        check(set(root.rglob('*')) == before)
        check(bool(json.dumps(malformed_only)))
    print(f'Passed {assertions} cleanup controls: truncated report, injected cleanup denial, primary timeout retained, all paths attempted, history preserved, JSON summary available. Temporary controls removed.')


if __name__ == '__main__':
    main()
