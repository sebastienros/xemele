"""Offline adapter controls; reusable process/provenance guards stay unchanged."""
import importlib.util
from pathlib import Path
import unittest
import tempfile
import sys
import json
from contextlib import ExitStack
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('dtd_repair_adapter', Path(__file__).with_name('measure.py'))
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)

class AdapterTests(unittest.TestCase):
    def test_task_project_and_original_support_binding(self):
        self.assertEqual(adapter.engine.OWN.name, 'M2-dtd-corpus-repair')
        self.assertEqual(adapter.engine.PROJECT.parent, adapter.engine.OWN)
        self.assertEqual(adapter.engine.PROJECT.name, 'Xemele.EntityBase.Measurements.csproj')
        self.assertEqual(adapter.engine.EXPECTED, {'InternalNestedModel', 'RedirectedComposition'})
    def test_generated_inventory_positional_properties_preserve_freeze(self):
        with patch.object(adapter, 'original_inventory', return_value={}) as inventory:
            adapter.inventory('dotnet', {}, ['generated'], {'Configuration':'Release', 'Optimize':'true'}, {})
            args, kwargs = inventory.call_args
            self.assertEqual(args[1]['NuGetAudit'], 'false')
            self.assertEqual(args[3], {'Configuration':'Release', 'Optimize':'true', 'NuGetAudit':'false'})
            self.assertNotIn('properties', kwargs)
    def test_counter_disabled_commands_are_unchanged(self):
        with patch.object(adapter, 'original_run', return_value='oracle') as run:
            self.assertEqual(adapter.run(['dotnet', '--correctness']), 'oracle')
            run.assert_called_once_with(['dotnet', '--correctness'])
    def test_exact_task_filter_routes_to_new_fixture(self):
        with patch.object(adapter, 'original_run', return_value='proof') as run:
            adapter.run(['dotnet', '--filter', '*EntityBaseBenchmarks*'])
            run.assert_called_once_with(['dotnet', '--filter', '*DtdCorpusRepairBenchmarks*'])
    def test_reporting_rebind_preserves_actual_head_and_frozen_binary_revision(self):
        known = {'head':'new-reporting-head', 'runtimeRevision':'frozen-runtime-head',
                 'reportingRebind':{'runtimeHead':'frozen-runtime-head','reportingHead':'new-reporting-head'}}
        with patch.object(adapter,'original_benchmark_results',return_value='unchanged-guards') as original:
            self.assertEqual(adapter.benchmark_results('log',Path('raw'),'new-reporting-head',known,{}),'unchanged-guards')
            original.assert_called_once_with('log',Path('raw'),'frozen-runtime-head',known,{})
        self.assertEqual(known['head'],'new-reporting-head')
        with self.assertRaises(ValueError):
            adapter.benchmark_results('log',Path('raw'),'wrong-reporting-head',known,{})

    def allocation_acceptance(self, borrowed):
        # Executes the unchanged reused main's allocation acceptance/finalization,
        # with synthetic preparation and an offline child-output stub. No process,
        # .NET, allocation counter, Generator/Builder or workload is invoked.
        metrics = {'iterations': 1024, 'coldSetupAndOracles': 100, 'ownedModels': 200, 'ownedDocuments': 300, 'positiveControl': 400}
        output = borrowed + json.dumps(metrics)
        with tempfile.TemporaryDirectory() as directory, ExitStack() as stack:
            root = Path(directory); own = root / 'owned'; own.mkdir(); parent = own / 'bin'; parent.mkdir()
            known = {'head': 'a' * 40, 'dotnetSha256': 'offline-hash', 'sources': {}, 'parentClosure': {}, 'runtimeDirectory': str(root), 'runtimeClosure': {}}
            (own / 'preparation.json').write_text(json.dumps(known))
            for name, value in {'ROOT':root, 'OWN':own, 'PARENT':parent, 'PROJECT':own/'offline.csproj', 'DRIVER':parent/'offline.dll'}.items():
                stack.enter_context(patch.object(adapter.engine,name,value))
            stack.enter_context(patch.object(adapter.engine,'digest',return_value='offline-hash'))
            stack.enter_context(patch.object(adapter.engine,'git',return_value=('a'*40).encode()))
            stack.enter_context(patch.object(adapter.engine,'clean_source_tree'))
            stack.enter_context(patch.object(adapter.engine,'closure',return_value={}))
            stack.enter_context(patch.object(adapter.engine,'source_freeze',side_effect=lambda value:value))
            stack.enter_context(patch.object(adapter.engine.shutil,'which',return_value=sys.executable))
            stack.enter_context(patch.object(adapter,'original_run',return_value=output))
            stack.enter_context(patch.object(sys,'argv',['offline-control','--mode','allocation','--report','offline','--exclusive-grant','synthetic-control-only']))
            error = None
            try: adapter.engine.main()
            except ValueError as failure: error = failure
            summary = json.loads((own/'offline.json').read_text())
            self.assertEqual(summary['allocationBytes'],metrics)
            self.assertTrue(summary['temporaryOutputsRemoved'])
            return summary, error

    def test_actual_reused_acceptance_retains_owned_and_separate_borrowed(self):
        summary, error = self.allocation_acceptance('DTD_REPAIR_BORROWED {"bytes":0,"iterations":1024}\n')
        self.assertIsNone(error)
        self.assertEqual(summary['result'],'passed')
        self.assertEqual(summary['phases']['strict-result-validation'],'passed')
        self.assertEqual(summary['phases']['final-freeze'],'passed')
        self.assertEqual(set(summary['allocationBytes']), {'iterations','coldSetupAndOracles','ownedModels','ownedDocuments','positiveControl'})
        self.assertEqual(summary['borrowedAllocation'], {'records':[{'bytes':0,'iterations':1024}], 'validation':'passed'})

    def test_actual_reused_acceptance_missing_nonzero_duplicate_wrong_count_refuses(self):
        for borrowed in ['', 'DTD_REPAIR_BORROWED {"bytes":1,"iterations":1024}\n',
                         'DTD_REPAIR_BORROWED {"bytes":0,"iterations":1}\n',
                         'DTD_REPAIR_BORROWED {"bytes":0,"iterations":1024}\nDTD_REPAIR_BORROWED {"bytes":0,"iterations":1024}\n']:
            with self.subTest(borrowed=borrowed):
                summary, error = self.allocation_acceptance(borrowed)
                self.assertIsInstance(error, ValueError)
                self.assertEqual(summary['result'],'failed')
                self.assertEqual(summary['borrowedAllocation']['validation'],'failed')
                self.assertEqual(summary['phases']['strict-result-validation'],'unrun')

if __name__=='__main__':unittest.main()
