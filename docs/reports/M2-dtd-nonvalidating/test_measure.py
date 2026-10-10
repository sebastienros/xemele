"""Offline adapter controls; reusable process/provenance guards stay unchanged."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('nonvalidating_adapter', Path(__file__).with_name('measure.py'))
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)

class AdapterTests(unittest.TestCase):
    def test_task_project_and_original_support_binding(self):
        self.assertEqual(adapter.engine.OWN.name, 'M2-dtd-nonvalidating')
        self.assertEqual(adapter.engine.PROJECT.parent, adapter.engine.OWN)
        self.assertEqual(adapter.engine.PROJECT.name, 'Xemele.EntityBase.Measurements.csproj')
        self.assertEqual(adapter.engine.EXPECTED, {'InternalNestedModel', 'RedirectedComposition'})
    def test_counter_disabled_commands_are_unchanged(self):
        with patch.object(adapter, 'original_run', return_value='oracle') as run:
            self.assertEqual(adapter.run(['dotnet', '--correctness']), 'oracle')
            run.assert_called_once_with(['dotnet', '--correctness'])
    def test_exact_task_filter_routes_to_new_fixture(self):
        with patch.object(adapter, 'original_run', return_value='proof') as run:
            adapter.run(['dotnet', '--filter', '*EntityBaseBenchmarks*'])
            run.assert_called_once_with(['dotnet', '--filter', '*NonvalidatingBenchmarks*'])
    def test_borrowed_zero_proof_preserves_owned_result(self):
        output='NONVALIDATING_BORROWED {"bytes":0,"iterations":1024}\n{"ownedModels":12,"positiveControl":1}'
        with patch.object(adapter, 'original_run', return_value=output):
            value=adapter.json.loads(adapter.run(['dotnet','--allocation']))
        self.assertEqual(value['ownedModels'],12)
        self.assertEqual(value['borrowed']['bytes'],0)
    def test_missing_duplicate_nonzero_or_wrong_count_refuses(self):
        for output in ['{}', 'NONVALIDATING_BORROWED {"bytes":1,"iterations":1024}\n{}',
                       'NONVALIDATING_BORROWED {"bytes":0,"iterations":1}\n{}',
                       'NONVALIDATING_BORROWED {"bytes":0,"iterations":1024}\nNONVALIDATING_BORROWED {"bytes":0,"iterations":1024}\n{}']:
            with self.subTest(output=output), patch.object(adapter,'original_run',return_value=output):
                with self.assertRaises(ValueError):adapter.run(['dotnet','--allocation'])

if __name__=='__main__':unittest.main()
