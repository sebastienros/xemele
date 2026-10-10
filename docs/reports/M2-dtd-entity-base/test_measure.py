"""Wrapper guard tests: no .NET, allocation counters or timing measurements.

These tests are source-only candidates until HOST RELEASE authorizes execution.
"""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('entity_base_measure', Path(__file__).with_name('measure.py'))
measure = importlib.util.module_from_spec(spec)
spec.loader.exec_module(measure)


class MeasurementGuards(unittest.TestCase):
    def test_guards_are_explicit_exceptions_not_optimized_away_assertions(self):
        with self.assertRaisesRegex(ValueError, 'exact failure'):
            measure.require(False, 'exact failure')

    def test_closure_includes_nested_payload_and_detects_mutation(self):
        with tempfile.TemporaryDirectory() as root:
            directory = Path(root)
            (directory / 'nested').mkdir()
            (directory / 'driver.dll').write_bytes(b'parent')
            child = directory / 'nested' / 'product.dll'
            child.write_bytes(b'child')
            before = measure.closure(directory)
            self.assertEqual(set(before), {'driver.dll', 'nested/product.dll'})
            child.write_bytes(b'changed')
            self.assertNotEqual(before, measure.closure(directory))

    def test_complete_source_freeze_detects_external_and_generated_input_change(self):
        with tempfile.TemporaryDirectory() as root:
            source = Path(root) / 'generated.cs'
            source.write_bytes(b'generated input')
            identities = {str(source): {'sha256': measure.digest(source), 'owner': 'generated MSBuild input'}}
            self.assertEqual(identities, measure.source_freeze(identities))
            source.write_bytes(b'different input')
            self.assertNotEqual(identities, measure.source_freeze(identities))

    def test_nonzero_build_and_missing_exact_cases_fail(self):
        with tempfile.TemporaryDirectory() as root:
            for text in [
                '// command took 1 seconds and exited with 1\n',
                '// command took 1 seconds and exited with 0\n// command took 1 seconds and exited with 0\nGlobal total time: executed benchmarks: 2\n',
                'There are not any results runs\n',
            ]:
                with self.assertRaises(ValueError):
                    measure.benchmark_results(text, Path(root), 'a' * 40, {})

    def test_actual_mismatching_hashes_and_partial_metrics_survive_early_failure(self):
        import json
        proof = {'stage': 'before', 'gatePassed': False, 'actual': {'closure': {'product.dll': 'actual-hash'}, 'generatedInputs': {'code.notcs': 'actual-code'}}}
        text = "// command took 1 seconds and exited with 1\n// Benchmark: EntityBaseBenchmarks.InternalNestedModel: fixture\n"
        text += 'WorkloadActual 1: 8 op, 800 ns, 100 ns/op\n// GC: 0 0 0 128 8\n// Benchmark Process 42 has exited with code 1.\n'
        text += 'ENTITY_BASE_PROVENANCE ' + json.dumps(proof) + '\n'
        known = {'generated': {'proof': {'closure': {'product.dll': 'expected-hash'}, 'generatedInputs': {'code.notcs': 'expected-code'}}}}
        summary = {}
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                measure.benchmark_results(text, Path(root), 'a' * 40, known, summary)
        row = summary['benchmarks']['observations'][0]
        self.assertEqual(row['childExits'], [1])
        self.assertEqual(row['gc'], [[0, 0, 0, 128, 8]])
        self.assertEqual(row['actual'][0]['operations'], 8)
        changed = row['comparisons'][0]['differences']['closure']['changed'][0]
        self.assertEqual(changed, {'file': 'product.dll', 'expected': 'expected-hash', 'actual': 'actual-hash'})
        self.assertEqual(row['proofs'][0]['actual']['generatedInputs']['code.notcs'], 'actual-code')

    def test_failed_preworkload_gate_does_not_claim_unobserved_intervals(self):
        import json
        failure = {'stage': 'before', 'reason': 'manifest-hash', 'expected': 'approved', 'actual': 'changed'}
        text = '// Benchmark: EntityBaseBenchmarks.RedirectedComposition: fixture\nENTITY_BASE_GATE_FAILURE ' + json.dumps(failure) + '\n'
        summary = {}
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                measure.benchmark_results(text, Path(root), 'a' * 40, {}, summary)
        row = summary['benchmarks']['observations'][0]
        self.assertEqual(row['actual'], [])
        self.assertEqual(row['result'], [])
        self.assertEqual(row['gc'], [])
        self.assertEqual(summary['benchmarks']['gateFailures'], [failure])

    def test_phase_accounting_preserves_true_passed_and_unrun_phases(self):
        summary = {'stage': 'initial', 'phases': {'initial': 'running', 'execution': 'unrun', 'validation': 'unrun', 'freeze': 'unrun'}}
        measure.phase(summary, 'execution')
        self.assertEqual(summary['phases'], {'initial': 'passed', 'execution': 'running', 'validation': 'unrun', 'freeze': 'unrun'})

    def test_changed_task_metadata_does_not_allow_repeating_same_compiled_source(self):
        old = {'result': 'passed', 'mode': 'allocation', 'preparedIdentities': {'sources': {'owned.cs': {'gitBlob': 'blob-a', 'sha256': 'same'}, 'obj/AssemblyInfo.cs': {'sha256': 'revision-a'}}}}
        new = {'sources': {'owned.cs': {'gitBlob': 'blob-a', 'sha256': 'same'}, 'obj/AssemblyInfo.cs': {'sha256': 'revision-b'}}}
        with tempfile.TemporaryDirectory() as root:
            import json
            directory = Path(root)
            (directory / 'previous.json').write_text(json.dumps(old))
            with patch.object(measure, 'OWN', directory):
                with self.assertRaisesRegex(ValueError, 'Unchanged successful observation'):
                    measure.reject_unchanged_success('allocation', new)
                new['sources']['owned.cs']['gitBlob'] = 'blob-b'
                measure.reject_unchanged_success('allocation', new)


if __name__ == '__main__':
    unittest.main()
