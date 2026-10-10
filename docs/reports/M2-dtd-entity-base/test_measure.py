"""Wrapper guard tests: no .NET, allocation counters or timing measurements.

These tests are source-only candidates until HOST RELEASE authorizes execution.
"""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch, Mock

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

    def test_build_failure_diagnostics_survive_without_structured_proof(self):
        with tempfile.TemporaryDirectory() as root:
            log = Path(root) / 'build.log'
            log.write_text('// command took 1 sec and exited with 0\n'
                           'Build timed out after 240 seconds.\n'
                           'Unhandled exception. System.Exception: Independent generated build failed.\n')
            facts = measure.output_facts(log)
        self.assertEqual(facts['generatedBuildExits'], [0])
        self.assertEqual(facts['buildOnlyRecords'], [])
        self.assertEqual(facts['diagnostics'], ['Build timed out after 240 seconds.',
                                              'Unhandled exception. System.Exception: Independent generated build failed.'])
        self.assertEqual(facts['diagnosticLineCount'], 2)
        self.assertFalse(facts['diagnosticsTruncated'])

    def test_build_diagnostics_are_bounded(self):
        with tempfile.TemporaryDirectory() as root:
            log = Path(root) / 'build.log'
            log.write_text(('error ' + 'x' * 1200 + '\n') * 25)
            facts = measure.output_facts(log)
        self.assertEqual(len(facts['diagnostics']), 20)
        self.assertTrue(all(len(line) == 1000 for line in facts['diagnostics']))
        self.assertEqual(facts['diagnosticLineCount'], 25)
        self.assertTrue(facts['diagnosticsTruncated'])

    def test_post_build_baseline_cleanup_preserves_restored_dependency_closure(self):
        with tempfile.TemporaryDirectory() as root:
            parent = Path(root)
            (parent / 'driver.dll').write_bytes(b'parent')
            with patch.object(measure, 'PARENT', parent):
                initial = measure.parent_directories()
                restored = parent / 'runtimes/osx-arm64/native'
                restored.mkdir(parents=True)
                (restored / 'dependency.dylib').write_bytes(b'restored dependency')
                baseline = measure.parent_directories()
                expected = measure.closure(parent)
                child = parent / 'generated-child'
                child.mkdir()
                (child / 'generated.dll').write_bytes(b'generated child')
                self.assertNotEqual(initial, baseline)
                outputs = measure.parent_directories() - baseline
                self.assertEqual(outputs, {child})
                self.assertEqual(measure.cleanup_outputs(outputs), [])
                self.assertEqual(measure.closure(parent), expected)
                self.assertTrue((restored / 'dependency.dylib').exists())

    def test_actual_binding_mismatch_is_retained_before_validation_refusal(self):
        import json
        actual = {'expected': '/owned.csproj', 'actual': ['/foreign.csproj'], 'passed': False}
        summary = {}
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaisesRegex(ValueError, 'project binding'):
                measure.benchmark_results('ENTITY_BASE_PROJECT_BINDING ' + json.dumps(actual) + '\n', Path(root), 'a' * 40,
                                          {'generated': {'projectBinding': {'expected': '/owned.csproj', 'actual': ['/owned.csproj'], 'passed': True}}}, summary)
        self.assertEqual(summary['benchmarks']['projectBindings'], [actual])

    def test_generated_query_uses_exact_observed_builder_properties(self):
        generated = {'buildRoot': '/owned/child', 'proof': {'directory': '/owned/child/bin/Release/net10.0'}}
        line = '// start dotnet build -c Release --no-restore --nodeReuse:false /p:UseSharedCompilation=false /p:Deterministic=true /p:Optimize=true '
        line += '/p:ArtifactsPath="/owned/child/" /p:OutDir="/owned/child/bin/Release/net10.0/" /p:OutputPath="/owned/child/bin/Release/net10.0/" /p:PublishDir="/owned/child/publish/" --output "/owned/child/bin/Release/net10.0/" in /owned/child\n'
        context = measure.generated_build_properties(line, generated)
        self.assertEqual(context['properties']['ArtifactsPath'], '/owned/child/')
        self.assertEqual(context['properties']['Configuration'], 'Release')
        self.assertEqual(context['observedArgv'][1], 'build')
        self.assertEqual(context['workingDirectory'], '/owned/child')
        self.assertEqual(context['observedArgv'][-2:], ['--output', '/owned/child/bin/Release/net10.0/'])
        summary = {}
        with self.assertRaisesRegex(ValueError, 'properties differ'):
            measure.generated_build_properties(line.replace('/owned/child/publish/', '/foreign/publish/'), generated, summary)
        self.assertEqual(summary['generatedBuilderContext']['properties']['PublishDir'], '/foreign/publish/')
        self.assertEqual(summary['generatedBuilderContext']['expectedProperties']['PublishDir'], '/owned/child/publish/')
        summary = {}
        with self.assertRaisesRegex(ValueError, 'working directory differs'):
            measure.generated_build_properties(line.replace(' in /owned/child', ' in /foreign'), generated, summary)
        self.assertEqual(summary['generatedBuilderContext']['workingDirectory'], '/foreign')

    def test_failed_inventory_query_retains_exact_diagnostics_and_exit(self):
        actual = Mock(returncode=1, pid=42)
        actual.communicate.return_value = (b'', b'error NETSDK1004: actual evaluated assets missing\n')
        record = {}
        with patch.object(measure.subprocess, 'Popen', return_value=actual), patch.object(measure.os, 'killpg', side_effect=ProcessLookupError):
            with self.assertRaisesRegex(ValueError, 'nonzero exit: 1'):
                measure.inventory_query(['dotnet', 'msbuild', '/actual.csproj'], {}, record)
        self.assertEqual(record['exit'], 1)
        self.assertEqual(record['diagnostics']['diagnostics'], ['error NETSDK1004: actual evaluated assets missing'])
        self.assertEqual(record['argv'], ['dotnet', 'msbuild', '/actual.csproj'])

    def test_timed_out_inventory_query_preserves_partial_diagnostics(self):
        import subprocess
        record = {}
        actual = Mock(pid=42)
        actual.poll.return_value = None
        actual.communicate.side_effect = subprocess.TimeoutExpired('dotnet', 60, stderr=b'error: query timed out\n')
        with patch.object(measure.subprocess, 'Popen', return_value=actual), patch.object(measure, 'stop_group') as cleanup:
            with self.assertRaises(subprocess.TimeoutExpired):
                measure.inventory_query(['dotnet', 'msbuild'], {}, record)
        self.assertIsNone(record['exit'])
        self.assertEqual(record['diagnostics']['diagnostics'], ['error: query timed out'])
        self.assertTrue(record['terminatedProcessGroup'])
        cleanup.assert_called_once_with(actual)

    def test_inventory_requires_actual_assets_and_collects_actual_variant_inputs(self):
        with tempfile.TemporaryDirectory() as root:
            directory = Path(root)
            project = directory / 'child.csproj'; project.write_text('<Project />')
            extension = directory / 'actual-artifacts/obj/child'; extension.mkdir(parents=True)
            assets = extension / 'project.assets.json'; assets.write_text('{}')
            import_file = extension / 'child.csproj.nuget.g.props'; import_file.write_text('<Project />')
            source = extension / 'AssemblyInfo.cs'; source.write_text('// actual variant')
            reference = directory / 'actual-reference.dll'; reference.write_bytes(b'reference')
            analyzer = directory / 'actual-analyzer.dll'; analyzer.write_bytes(b'analyzer')
            properties = {'Configuration': 'Release', 'ArtifactsPath': str(directory / 'actual-artifacts') + '/'}
            result = {'Properties': dict(properties, ProjectAssetsFile=str(assets), MSBuildProjectExtensionsPath=str(extension), MSBuildAllProjects=str(import_file)),
                      'Items': {'Compile': [{'FullPath': str(source)}], 'ReferencePath': [{'FullPath': str(reference)}], 'Analyzer': [{'FullPath': str(analyzer)}]}}
            summary = {}
            with patch.object(measure, 'inventory_query', return_value=result) as query:
                inventory = measure.source_inventory(Path('dotnet'), {}, [project], properties, summary)
                command = query.call_args.args[0]
                self.assertIn('-p:ArtifactsPath=' + properties['ArtifactsPath'], command)
                for path in [assets, import_file, source, reference, analyzer]:
                    self.assertEqual(inventory[str(path.resolve())]['sha256'], measure.digest(path))
                self.assertEqual(summary['inventoryQueries'][0]['evaluatedProperties']['ProjectAssetsFile'], str(assets))
                assets.unlink()
                with self.assertRaisesRegex(ValueError, 'Missing actual evaluated assets'):
                    measure.source_inventory(Path('dotnet'), {}, [project], properties, {})

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

    def test_missing_required_metadata_partial_proof_survives_raw_summary_cleanup(self):
        import json
        proof = {'stage': 'before', 'gatePassed': False, 'captureComplete': False,
                 'actual': {'closure': {'survivor.dll': 'actual-sibling'}, 'generatedInputs': {'child.notcs': 'actual-code'}, 'entry': None},
                 'captureErrors': [{'phase': 'metadata', 'field': 'entry', 'path': '/missing.dll', 'type': 'System.IO.FileNotFoundException', 'message': 'missing required entry'}]}
        text = '// Benchmark: EntityBaseBenchmarks.InternalNestedModel: fixture\nENTITY_BASE_PROVENANCE ' + json.dumps(proof) + '\n'
        summary = {}
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                measure.benchmark_results(text, Path(root), 'a' * 40, {}, summary)
        saved = json.loads(json.dumps(summary))['benchmarks']['observations'][0]['proofs'][0]
        self.assertEqual(saved['actual']['closure'], {'survivor.dll': 'actual-sibling'})
        self.assertEqual(saved['actual']['generatedInputs'], {'child.notcs': 'actual-code'})
        self.assertIsNone(saved['actual']['entry'])
        self.assertEqual(saved['captureErrors'][0]['field'], 'entry')
        self.assertEqual(saved['captureErrors'][0]['type'], 'System.IO.FileNotFoundException')

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
