"""SOURCE controls: real owned processes and durable lifecycle ordering. Do not run before acceptance."""
import importlib.util
import json
import os
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import patch

s = importlib.util.spec_from_file_location('corpus_runner', Path(__file__).with_name('run.py'))
m = importlib.util.module_from_spec(s)
s.loader.exec_module(m)


class Supervisor(unittest.TestCase):
    def setUp(self):
        m.OUT.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix='supervisor-controls-', dir=m.OUT)
        self.root = Path(self.temporary.name)
        self.out = self.root / 'campaign'
        self.out.mkdir(exist_ok=False)

    def tearDown(self):
        self.temporary.cleanup()

    def case(self, name, passed=True, exit_code=0, prefix='', work=3, rows=None, engine='product'):
        journal = self.out / (name + '.jsonl')
        row = dict(id=name, engine=engine, classification='applicable', kind='instance',
                   expected='valid', actual='instance-valid' if passed else 'instance-invalid',
                   passed=passed, codes=[])
        observations = [row] if rows is None else rows
        code = prefix + '\nfrom pathlib import Path\n'
        code += f'Path({str(journal)!r}).write_text({("".join(json.dumps(r)+chr(10) for r in observations))!r})\n'
        code += f'raise SystemExit({exit_code})\n'
        return dict(argv=[sys.executable, '-c', code], engine=engine, testSet=name,
                    journal=journal, expected=[row], workSeconds=work)

    def execute(self, cases, work=None):
        now = time.monotonic()
        return m.execute_cases(cases, self.out, os.environ.copy(), now + (10 if work is None else work), now + 20)

    def assert_clean(self, result):
        for receipt in result['sequence']:
            self.assertTrue(receipt['directChildReaped'])
            self.assertTrue(receipt['ownedGroupAbsent'])
            self.assertTrue(receipt['logClosed'])
            if receipt['pid'] is not None:
                self.assertEqual(m.supervision.members(receipt['pid']), [])
        self.assertTrue(result['allLaunchedChildrenClean'])
        self.assertEqual(json.loads((self.out / 'campaign-cleanup.json').read_text()), result)

    def test_unknown_set_fails(self):
        with self.assertRaises(AssertionError): m.select_sets('unknown', {'testSets': ['known']})

    def test_exact_ids_exit_zero(self):
        self.assertTrue(m.verify([{'id': 'a'}], [{'id': 'a'}], 0))

    def test_same_count_substitution_rejected(self):
        with self.assertRaises(AssertionError): m.verify([{'id': 'b'}], [{'id': 'a'}], 0)

    def test_duplicate_rejected(self):
        with self.assertRaises(AssertionError): m.verify([{'id': 'a'}, {'id': 'a'}], [{'id': 'a'}], 0)

    def test_exit_seven_predicate(self):
        self.assertFalse(m.verify([{'id': 'a'}], [{'id': 'a'}], 7))

    def test_timeout_predicate(self):
        self.assertFalse(m.verify([{'id': 'a'}], [{'id': 'a'}], 'timeout'))

    def test_normal_real_child_and_cleanup_receipt(self):
        result = self.execute([self.case('normal')])
        self.assertEqual(result['status'], 0)
        self.assertTrue(m.supervision.workload_succeeded(result['sequence'][0]))
        self.assert_clean(result)

    def test_timeout_SIGTERM_exit_zero_is_failure_and_no_next(self):
        ready = self.out / 'ready'
        code = ('import signal,time\nfrom pathlib import Path\n'
                'signal.signal(signal.SIGTERM,lambda *args: exit(0))\n'
                f'Path({str(ready)!r}).write_text("ready")\n'
                'while True: time.sleep(.05)\n')
        first = self.case('timeout', work=.6)
        first['argv'] = [sys.executable, '-c', code]
        second = self.case('must-not-start')
        with patch.object(m, 'inspect_rows', side_effect=AssertionError('timeout journal must not be inspected')) as inspect:
            result = self.execute([first, second])
        self.assertTrue(ready.exists(), 'TERM handler must have been installed')
        self.assertEqual(result['sequence'][0]['exit'], 0)
        self.assertEqual(result['sequence'][0]['reason'], 'TimeoutExpired')
        self.assertFalse(m.supervision.workload_succeeded(result['sequence'][0]))
        self.assertEqual(result['status'], 1)
        self.assertEqual(len(result['sequence']), 1)
        self.assertFalse(second['journal'].exists())
        inspect.assert_not_called()
        self.assert_clean(result)

    def test_real_nonzero_after_all_rows_stops_without_inspection(self):
        first, second = self.case('exit-seven', exit_code=7), self.case('must-not-start')
        with patch.object(m, 'inspect_rows', side_effect=AssertionError('failed child journal must not be inspected')) as inspect:
            result = self.execute([first, second])
        self.assertTrue(first['journal'].exists())
        self.assertEqual(len(first['journal'].read_text().splitlines()), 1)
        self.assertEqual(result['sequence'][0]['reason'], 'failed')
        self.assertEqual(result['sequence'][0]['exit'], 7)
        self.assertEqual(result['status'], 1)
        self.assertEqual(len(result['sequence']), 1)
        self.assertFalse(second['journal'].exists())
        inspect.assert_not_called()
        self.assert_clean(result)

    def test_semantic_nonpass_continues_to_BCL_harness_exit_zero(self):
        result = self.execute([self.case('disagreement', passed=False), self.case('reference', engine='bcl')])
        self.assertEqual(result['status'], 0)
        self.assertEqual(len(result['sequence']), 2)
        self.assertEqual(result['countsByEngine']['product']['observationNonpassed'], 1)
        self.assertEqual(result['countsByEngine']['bcl']['observationPassed'], 1)
        self.assert_clean(result)

    def test_supplement_and_conditional_receipt_counts_do_not_claim_original_passes(self):
        supplement = self.case('case/unicode16-supplemental')
        # Use a safe journal path; the ID remains the actual supplemental shape.
        supplement['journal'] = self.out / 'supplement.jsonl'
        row = supplement['expected'][0]
        supplement['argv'] = [sys.executable, '-c', f"from pathlib import Path; Path({str(supplement['journal'])!r}).write_text({(json.dumps(row)+chr(10))!r})"]
        conditional = self.case('conditional')
        conditional['expected'][0]['adjudication'] = {'supplemental': False}
        result = self.execute([supplement, conditional])
        self.assertEqual(result['status'], 0)
        self.assertEqual(result['countsByEngine']['product']['observationPassed'], 2)
        self.assertFalse(any(k.startswith('original') for k in result['countsByEngine']['product']))
        self.assert_clean(result)

    def test_durable_receipt_before_raw_inspection(self):
        real_inspect, real_fsync = m.inspect_rows, m.os.fsync
        fsynced = []
        def sync(fd):
            real_fsync(fd)
            fsynced.append(fd)
        def inspect(spec):
            receipt = json.loads((self.out / 'lifecycle-000.json').read_text())
            self.assertTrue(m.supervision.workload_succeeded(receipt))
            self.assertGreaterEqual(len(fsynced), 2, 'file and directory fsync precede row read')
            self.assertEqual(m.supervision.members(receipt['pid']), [])
            return real_inspect(spec)
        with patch.object(m.os, 'fsync', side_effect=sync), patch.object(m, 'inspect_rows', side_effect=inspect):
            result = self.execute([self.case('durable')])
        self.assertEqual(result['status'], 0)
        self.assert_clean(result)

    def test_coverage_failure_stops_next_child_without_padding(self):
        first, second = self.case('missing', rows=[]), self.case('must-not-start')
        result = self.execute([first, second])
        self.assertEqual(result['status'], 1)
        self.assertIsNotNone(result['coverageFailure'])
        self.assertEqual(len(result['sequence']), 1)
        self.assertEqual(first['journal'].read_text(), '')
        self.assertFalse(second['journal'].exists())
        self.assert_clean(result)

    def test_duplicate_completed_rows_stop_next_child(self):
        first = self.case('duplicate')
        row = first['expected'][0]
        first = self.case('duplicate', rows=[row, row])
        second = self.case('must-not-start')
        result = self.execute([first, second])
        self.assertEqual(result['status'], 1)
        self.assertIn('duplicate', result['coverageFailure']['error'])
        self.assertFalse(second['journal'].exists())
        self.assert_clean(result)

    def test_expired_global_work_deadline_launches_nothing(self):
        case = self.case('must-not-start')
        result = self.execute([case], work=-1)
        self.assertEqual(result['status'], 1)
        self.assertIsNone(result['sequence'][0]['pid'])
        self.assertFalse(case['journal'].exists())
        self.assert_clean(result)

    def test_receipts_never_replace_prior_attempt(self):
        path = self.out / 'receipt.json'
        m.durable(path, {'original': True})
        with self.assertRaises(FileExistsError): m.durable(path, {'original': False})
        self.assertEqual(json.loads(path.read_text()), {'original': True})

    def test_second_execution_cannot_replace_logs_or_receipts(self):
        case = self.case('normal')
        result = self.execute([case])
        original = (self.out / 'lifecycle-000.json').read_bytes()
        with self.assertRaises(FileExistsError): self.execute([case])
        self.assertEqual((self.out / 'lifecycle-000.json').read_bytes(), original)
        self.assert_clean(result)


if __name__ == '__main__':
    unittest.main()
