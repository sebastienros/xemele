"""SOURCE controls: real owned processes and durable lifecycle ordering. Do not run before acceptance."""
import importlib.util
import json
import os
import sys
import signal
import shutil
import threading
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

    def test_forced_intermediate_parent_kill_after_nested_ACK_drains_all_groups_and_no_next(self):
        ready = self.out / 'inner-ready.json'
        parent_out = self.out / 'intermediate'; parent_out.mkdir()
        never = self.out / 'next-marker'
        payload = ("import json,os,time\nfrom pathlib import Path\n" +
                   f"p=Path({(str(ready)+'.pending')!r});p.write_text(json.dumps({{'parent':os.getppid(),'group':os.getpgrp()}}));p.rename(Path({str(ready)!r}))\n" +
                   "while True: time.sleep(.05)\n")
        first = self.case('inner'); first['argv'] = [sys.executable, '-c', payload]
        first['journal'] = str(first['journal']); first['workSeconds'] = 100
        parent_code = ("import importlib.util,os,time\nfrom pathlib import Path\n" +
            f"s=importlib.util.spec_from_file_location('runner',{str(Path(m.__file__))!r});r=importlib.util.module_from_spec(s);s.loader.exec_module(r)\n" +
            f"r.execute_cases([{first!r}],Path({str(parent_out)!r}),os.environ.copy(),time.monotonic()+100,time.monotonic()+110)\n")
        errors = []
        def force_parent_death():
            try:
                deadline = time.monotonic() + 4
                while not ready.exists():
                    if time.monotonic() >= deadline: raise TimeoutError('inner ready after ACK not observed')
                    time.sleep(.01)
                identity = json.loads(ready.read_text())
                registry = self.out / 'nested-000'
                published = [json.loads(file.read_text()) for file in registry.glob('group-*.json')]
                self.assertIn(identity['parent'], [entry['pgid'] for entry in published])
                inner = next(entry for entry in published if entry['pgid'] == identity['group'])
                self.assertTrue((registry / ('ack-' + inner['token'] + '.json')).exists())
                os.kill(identity['parent'], signal.SIGKILL)  # inner parent's finally cannot run
            except BaseException as error:
                errors.append(repr(error))
        killer = threading.Thread(target=force_parent_death)
        killer.start()
        m.supervision.ARTIFACTS = self.out
        sequence = []
        try:
            commands = [[sys.executable, '-c', parent_code],
                        [sys.executable, '-c', f"from pathlib import Path;Path({str(never)!r}).touch()"]]
            with m.nested.interruptions():
                for index, command in enumerate(commands):
                    now = time.monotonic()
                    record = m.nested.supervise_family_command(m.supervision, command, index, os.environ.copy(), now+5, now+15)
                    sequence.append(record)
                    m.durable(self.out / f'outer-lifecycle-{index}.json', record)
                    if not m.supervision.workload_succeeded(record): break
        finally:
            killer.join(timeout=1)
        self.assertFalse(killer.is_alive())
        self.assertEqual(errors, [])
        self.assertTrue(ready.exists())
        self.assertEqual(len(sequence), 1)
        self.assertEqual(sequence[0]['reason'], 'failed')
        self.assertEqual(sequence[0]['exit'], -signal.SIGKILL)
        self.assertTrue(sequence[0]['directChildReaped'])
        self.assertTrue(sequence[0]['ownedGroupAbsent'])
        self.assertTrue(sequence[0]['logClosed'])
        ownership = sequence[0]['nestedOwnership']
        self.assertTrue(ownership['allOwnedGroupsAbsent'])
        self.assertGreaterEqual(len(ownership['groups']), 2)
        self.assertEqual(ownership['pendingIntents'], [])
        for group in ownership['groups']: self.assertEqual(m.supervision.members(group), [])
        self.assertFalse(never.exists())

    def test_unresolved_prelaunch_intent_cannot_report_clean(self):
        registry = self.out / 'pending'; registry.mkdir()
        m.nested.durable(registry / 'intent-unpublished.json', {'token': 'unpublished'})
        record = m.nested.drain(m.supervision, registry, time.monotonic()+2.1)
        self.assertFalse(record['allOwnedGroupsAbsent'])
        self.assertEqual(record['pendingIntents'], ['unpublished'])
        self.assertIn(record['cleanupError'], ('TimeoutError', 'TimeoutExpired'))

    def test_ACK_publication_paused_after_creation_bootstrap_waits_then_executes_once(self):
        registry = self.out / 'atomic-ack'; registry.mkdir()
        owners = m.nested.registries(os.environ) + [registry]
        token = 'atomic-publication-control'
        output = self.out / 'executions.txt'
        argv = [sys.executable, '-c', f"from pathlib import Path;Path({str(output)!r}).open('a').write('once\\n')"]
        for owner in owners:
            m.nested.durable(owner / ('intent-' + token + '.json'), dict(token=token, argv=argv,
                parentPid=os.getpid(), launchDeadline=time.monotonic()+5))
        environment = os.environ.copy()
        environment[m.nested.REGISTRIES] = json.dumps([str(owner) for owner in owners])
        stream = (self.out / 'ack-bootstrap.log').open('xb')
        child = None; writer = None
        entered = threading.Event(); release = threading.Event(); errors = []
        try:
            child = m.supervision.subprocess.Popen([sys.executable, m.nested.__file__, '--launch', token] + argv,
                env=environment, stdout=stream, stderr=m.supervision.subprocess.STDOUT, start_new_session=True)
            expected_group = dict(token=token, pid=child.pid, pgid=child.pid)
            deadline = time.monotonic()+2
            while not all(m.nested.complete_record(owner / ('group-'+token+'.json'), expected_group) for owner in owners):
                if time.monotonic() >= deadline: raise TimeoutError('registered bootstrap not observed')
                time.sleep(.01)
            ack = dict(token=token, pid=child.pid)
            for owner in owners[:-1]: m.nested.durable(owner / ('ack-'+token+'.json'), ack)
            real_dump = m.nested.json.dump
            def pause_before_JSON_write(value, target, **kwargs):
                if value == ack:
                    entered.set()  # Temporary file has been exclusively created; it is still empty.
                    if not release.wait(timeout=2): raise TimeoutError('publication pause not released')
                return real_dump(value, target, **kwargs)
            def publish():
                try: m.nested.durable(registry / ('ack-'+token+'.json'), ack)
                except BaseException as error: errors.append(repr(error))
            with patch.object(m.nested.json, 'dump', side_effect=pause_before_JSON_write):
                writer = threading.Thread(target=publish); writer.start()
                self.assertTrue(entered.wait(timeout=2))
                temporaries = list(registry.glob('.publish-*.tmp'))
                self.assertEqual(len(temporaries), 1)
                self.assertEqual(temporaries[0].stat().st_size, 0)
                self.assertFalse((registry / ('ack-'+token+'.json')).exists())
                self.assertFalse(m.nested.complete_record(registry / ('ack-'+token+'.json'), ack))
                time.sleep(.1)
                self.assertIsNone(child.poll())
                self.assertFalse(output.exists(), 'bootstrap must wait for the complete ACK publication')
                release.set(); writer.join(timeout=1)
            self.assertFalse(writer.is_alive())
            self.assertEqual(errors, [])
            self.assertEqual(child.wait(timeout=2), 0)
            self.assertEqual(output.read_text(), 'once\n')
            self.assertTrue(m.nested.complete_record(registry / ('ack-'+token+'.json'), ack))
            with self.assertRaises(FileExistsError): m.nested.durable(registry / ('ack-'+token+'.json'), {'replaced':True})
            self.assertTrue(m.nested.complete_record(registry / ('ack-'+token+'.json'), ack))
        finally:
            release.set()
            if writer is not None: writer.join(timeout=1)
            if child is not None:
                if child.poll() is None: m.supervision.kill_group(child.pid, signal.SIGKILL)
                child.wait(timeout=.5)
            stream.close()

    def test_expired_nested_deadline_fails_without_process_table_scan(self):
        registry = self.out / 'expired'; registry.mkdir()
        with patch.object(m.nested.time, 'monotonic', return_value=10), \
             patch.object(m.nested, 'group_members_snapshot', return_value={}) as snapshot:
            record = m.nested.drain(m.supervision, registry, 9)
        snapshot.assert_not_called()
        self.assertFalse(record['allOwnedGroupsAbsent'])
        self.assertEqual(record['cleanupError'], 'TimeoutError')

    def test_one_all_absent_snapshot_crossing_deadline_cannot_report_clean(self):
        registry = self.out / 'slow-sweep'; registry.mkdir()
        groups = list(range(900000,900040))
        for group in groups:
            token = str(group)
            m.nested.durable(registry / ('intent-'+token+'.json'), dict(token=token))
            m.nested.durable(registry / ('group-'+token+'.json'), dict(token=token,pid=group,pgid=group))
        clock = [10.0]
        def absent_after_deadline(actual_groups, timeout):
            self.assertEqual(actual_groups, set(groups))
            self.assertGreater(timeout, 0)
            self.assertLessEqual(timeout, 2)
            clock[0] = 13.1  # scan deadline = hard15 - receipt reserve2; all queried groups are absent.
            return {}
        with patch.object(m.nested.time, 'monotonic', side_effect=lambda:clock[0]), \
             patch.object(m.supervision, 'kill_group'), \
             patch.object(m.supervision, 'members', side_effect=AssertionError('per-group ps scan forbidden')), \
             patch.object(m.nested, 'group_members_snapshot', side_effect=absent_after_deadline) as snapshot:
            record = m.nested.drain(m.supervision, registry, 15)
        self.assertEqual(snapshot.call_count, 1)
        self.assertEqual(record['remainingGroups'], {})
        self.assertFalse(record['allOwnedGroupsAbsent'])
        self.assertEqual(record['cleanupError'], 'TimeoutError')

    def api_binding_fixture(self):
        root = self.root / 'api-source'; here = root / 'docs/reports/corpus'
        here.mkdir(parents=True); api = root / 'docs/api'; api.mkdir(parents=True)
        fragment = api / 'current.txt'; fragment.write_text('public verified API\n')
        manifest = root / 'accepted.json'
        manifest.write_text(json.dumps({'apiInputs': {'docs/api/current.txt': m.digest(fragment)}}))
        binding = dict(final_freeze_ready=True, verified_integration='verified-fixture',
                       source_manifest='accepted.json', source_manifest_sha256=m.digest(manifest),
                       apiInputs={'docs/api/current.txt': m.digest(fragment)})
        path = here / 'verified-api-preparation.json'; path.write_text(json.dumps(binding))
        return root, here, fragment, manifest, path, binding

    def test_api_binding_includes_exact_manifest_and_complete_membership(self):
        root, here, fragment, manifest, path, binding = self.api_binding_fixture()
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here), patch.object(m.subprocess, 'run') as ancestor:
            self.assertEqual(m.verified_api_inputs(), {**binding['apiInputs'], 'accepted.json': m.digest(manifest)})
            self.assertEqual(ancestor.call_args.args[0], ['git', 'merge-base', '--is-ancestor', 'verified-fixture', 'HEAD'])
            self.assertEqual(ancestor.call_args.kwargs['timeout'], 5)

    def test_api_binding_hold_prevents_final_freeze(self):
        root, here, fragment, manifest, path, binding = self.api_binding_fixture()
        binding['final_freeze_ready'] = False; path.write_text(json.dumps(binding))
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here), patch.object(m.subprocess, 'run') as ancestor:
            with self.assertRaisesRegex(AssertionError, 'final source/API freeze held'): m.verified_api_inputs()
            ancestor.assert_not_called()

    def test_api_binding_changed_fragment_fails_with_unchanged_manifest(self):
        root, here, fragment, manifest, path, binding = self.api_binding_fixture()
        fragment.write_text('unverified replacement\n')
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here):
            with self.assertRaisesRegex(AssertionError, 'verified API fragment changed'): m.verified_api_inputs()

    def test_api_binding_added_or_removed_fragment_fails_membership(self):
        root, here, fragment, manifest, path, binding = self.api_binding_fixture()
        extra = fragment.parent / 'unverified.txt'; extra.write_text('unknown addition')
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here):
            with self.assertRaisesRegex(AssertionError, 'complete docs/api fragment membership changed'): m.verified_api_inputs()
            extra.unlink(); fragment.unlink()
            with self.assertRaisesRegex(AssertionError, 'complete docs/api fragment membership changed'): m.verified_api_inputs()

    def test_api_binding_changed_manifest_or_membership_fails(self):
        root, here, fragment, manifest, path, binding = self.api_binding_fixture()
        manifest.write_text(json.dumps({'apiInputs': {}}))
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here):
            with self.assertRaisesRegex(AssertionError, 'verified API manifest changed'): m.verified_api_inputs()
            binding['source_manifest_sha256'] = m.digest(manifest); path.write_text(json.dumps(binding))
            with self.assertRaisesRegex(AssertionError, 'accepted API membership changed'): m.verified_api_inputs()

    def test_api_binding_unintegrated_manifest_revision_fails(self):
        root, here, fragment, manifest, path, binding = self.api_binding_fixture()
        error = m.subprocess.CalledProcessError(1, ['git', 'merge-base'])
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here), patch.object(m.subprocess, 'run', side_effect=error):
            with self.assertRaises(m.subprocess.CalledProcessError): m.verified_api_inputs()

    def runtime_binding_fixture(self):
        root = self.root / 'runtime-source'; here = root / 'docs/reports/M8-xsd-corpus'; here.mkdir(parents=True)
        binding = json.loads((m.HERE / 'verified-api-preparation.json').read_text())
        paths = set(binding['runtime_production_inputs']) | set(binding['baselines_sha256']) | {p for p in binding['reviewed_build_inputs'] if not Path(p).is_absolute()}
        paths.update(binding[key] for key in ['runtime_source_manifest', 'runtime_review_plan', 'runtime_api_proof', 'repair_proof', 'verified_task', 'integration_record'])
        historical = binding['historical_missing_binding']
        paths.update(historical[k] for k in ['source_manifest','runtime_source_manifest','runtime_review_plan','verified_task','integration_record'])
        repair = json.loads((m.ROOT / binding['repair_proof']).read_text())
        paths.update([repair['canonical_projection'],repair['allocation_custody_record']])
        for name in paths:
            target = root / name; target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(m.ROOT / name, target)
        file = here / 'verified-api-preparation.json'; file.write_text(json.dumps(binding))
        return root, here, file, binding

    def test_verified_runtime_source_and_api_evidence_membership(self):
        root, here, file, binding = self.runtime_binding_fixture()
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here):
            observed = m.verified_runtime_inputs()
        self.assertTrue(set(binding['runtime_production_inputs']).issubset(observed))
        self.assertTrue(set(binding['baselines_sha256']).issubset(observed))
        self.assertEqual(observed[binding['verified_task']], binding['verified_task_sha256'])
        self.assertEqual(observed[binding['runtime_api_proof']], binding['runtime_api_proof_sha256'])

    def test_changed_runtime_source_membership_or_bytes_refuses_freeze(self):
        root, here, file, binding = self.runtime_binding_fixture()
        extra = root / 'src/Xemele/unreviewed.cs'; extra.write_text('unknown source')
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here):
            with self.assertRaisesRegex(AssertionError, 'verified production source membership changed'): m.verified_runtime_inputs()
            extra.unlink()
            selected = next(iter(binding['runtime_production_inputs'])); source = root / selected
            source.write_bytes(source.read_bytes() + b' ')
            with self.assertRaisesRegex(AssertionError, 'verified runtime/source/API baseline changed'): m.verified_runtime_inputs()

    def test_changed_verified_task_proof_manifest_or_baseline_refuses_freeze(self):
        root, here, file, binding = self.runtime_binding_fixture()
        names = [binding[key] for key in ['verified_task', 'runtime_api_proof', 'repair_proof', 'runtime_source_manifest', 'runtime_review_plan', 'integration_record']] + list(binding['baselines_sha256'])
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here):
            for name in names:
                target = root / name; original = target.read_bytes(); target.write_bytes(original + b' ')
                with self.subTest(path=name), self.assertRaises(AssertionError): m.verified_runtime_inputs()
                target.write_bytes(original)

    def test_unverified_runtime_task_cannot_be_rebound_as_verified(self):
        root, here, file, binding = self.runtime_binding_fixture()
        task_path = root / binding['verified_task']; task = json.loads(task_path.read_text()); task['status'] = 'in_progress'
        task_path.write_text(json.dumps(task)); binding['verified_task_sha256'] = m.digest(task_path); file.write_text(json.dumps(binding))
        with patch.object(m, 'ROOT', root), patch.object(m, 'HERE', here):
            with self.assertRaises(AssertionError): m.verified_runtime_inputs()

    def test_rebound_same_count_repair_without_full_api_equality_refuses(self):
        root, here, file, binding = self.runtime_binding_fixture()
        record_path = root / binding['integration_record']; record = json.loads(record_path.read_text()); record['full93EqualVerified'] = False
        record_path.write_text(json.dumps(record)); binding['integration_record_sha256'] = m.digest(record_path)
        proof_path = root / binding['repair_proof']; repair = json.loads(proof_path.read_text()); repair['integration_record_sha256'] = binding['integration_record_sha256']
        proof_path.write_text(json.dumps(repair)); binding['repair_proof_sha256'] = m.digest(proof_path); file.write_text(json.dumps(binding))
        with patch.object(m,'ROOT',root),patch.object(m,'HERE',here):
            with self.assertRaises(AssertionError):m.verified_runtime_inputs()

    def test_rebound_canonical_api_declaration_drift_refuses(self):
        root, here, file, binding = self.runtime_binding_fixture()
        proof_path = root / binding['repair_proof']; repair = json.loads(proof_path.read_text()); canonical = root / repair['canonical_projection']
        data = canonical.read_text().splitlines(); row=json.loads(data[1]);row['declaration']+=' altered';data[1]=json.dumps(row);canonical.write_text('\n'.join(data)+'\n')
        repair['canonical_projection_sha256']=m.digest(canonical);proof_path.write_text(json.dumps(repair));binding['repair_proof_sha256']=m.digest(proof_path);file.write_text(json.dumps(binding))
        with patch.object(m,'ROOT',root),patch.object(m,'HERE',here):
            with self.assertRaises(AssertionError):m.verified_runtime_inputs()

    def supplement_fixture(self):
        with patch.dict(sys.modules, {'run': m}):
            driver = m.module('supplement_source_fixture', m.HERE / 'supplements.py')
        facts = json.loads((m.CORPUS / 'application-supplemental-inputs.json').read_text())
        ids = [name for group in facts['xml_groups'] for name in group['supplemental_ids']] + [facts['late_hint']['supplemental_id']]
        rows = [dict(id=name, engine='product', classification='supplemental',
                     kind='supplemental-schema' if i < 4 and i % 2 == 0 else 'supplemental-instance',
                     expected='invalid' if i == 4 else 'valid', actual='instance-invalid' if i == 4 else 'schema-valid' if i % 2 == 0 else 'instance-valid',
                     originalPassed=None, countAsOriginalPass=False, countAsGatePass=False, complete=True, profilePassed=True, codes=['XSD.HintOrder'] if i == 4 else []) for i,name in enumerate(ids)]
        rows[-1]['originalObservation'] = facts['late_hint']['original_observation']
        (self.out / 'profile-controls.json').write_text(json.dumps(dict(actual='passed', originalPassed=None, countAsOriginalPass=False, countAsGatePass=False)))
        file = self.out / 'supplements.json'; file.write_text(json.dumps(rows))
        return driver, file, rows

    def test_separate_supplement_recipe_is_finite_and_has_no_original_credit(self):
        driver, file, rows = self.supplement_fixture()
        self.assertEqual(len(driver.commands(self.out)), 3)
        self.assertEqual((driver.SECONDS,driver.WORK_SECONDS,driver.CLEANUP_SECONDS),(900,300,10))
        self.assertEqual(driver.inspect_outputs(self.out), dict(xmlApplicationRows=4,lateHintRows=1,originalPassCredit=0,gatePassCredit=0))

    def test_supplement_mutations_refusals_unexecuted_or_original_credit_refuse(self):
        driver, file, rows = self.supplement_fixture()
        mutations = [dict(id='neighbor'), dict(engine='bcl'), dict(classification='applicable'),dict(kind='instance'),dict(expected='valid'),
                     dict(complete=False),dict(profilePassed=False),dict(originalPassed=False),dict(countAsOriginalPass=True),dict(countAsGatePass=True),
                     dict(actual='incomplete-refusal'),dict(actual='instance-hint-ordering-limitation'),dict(codes=[])]
        for changes in mutations:
            altered=json.loads(json.dumps(rows));altered[-1].update(changes);file.write_text(json.dumps(altered))
            with self.subTest(changes=changes),self.assertRaises(AssertionError):driver.inspect_outputs(self.out)
        for altered in [rows[:-1],rows+rows[:1],list(reversed(rows))]:
            file.write_text(json.dumps(altered))
            with self.assertRaises(AssertionError):driver.inspect_outputs(self.out)
        altered=json.loads(json.dumps(rows));altered[-1]['originalObservation']['passed']=True;file.write_text(json.dumps(altered))
        with self.assertRaises(AssertionError):driver.inspect_outputs(self.out)

    def preparation_binding_fixture(self):
        runner = self.out / 'runner.dll'; product = self.out / 'Xemele.dll'
        runner.write_bytes(b'unchanged runner'); product.write_bytes(b'tested product dependency')
        commands_file = self.out / 'commands.json'; tested_file = self.out / 'tested-inputs.json'
        m.durable(commands_file, dict(sourceRevision=m.revision(), commands=m.preparation_commands(),
                                     campaignSeconds=1800, childWorkSeconds=300, cleanupSeconds=10))
        identities = {str(file):m.digest(file) for file in (runner,product)}
        m.durable(tested_file, dict(sourceRevision=m.revision(), commandsSha256=m.digest(commands_file), identities=identities))
        return runner, product, commands_file, tested_file, identities

    def test_changed_dependency_with_unchanged_runner_refuses_preparation(self):
        runner, product, commands_file, tested_file, identities = self.preparation_binding_fixture()
        with patch.object(m, 'input_identities', return_value=identities):
            self.assertEqual(m.verify_preparation_bindings(commands_file, tested_file, m.digest(tested_file)), identities)
            product.write_bytes(b'replacement untested product dependency')
            self.assertEqual(m.digest(runner), identities[str(runner)])
            with self.assertRaisesRegex(AssertionError, 'tested dependency/runtime/source identity changed'):
                m.verify_preparation_bindings(commands_file, tested_file, m.digest(tested_file))

    def test_replaced_tested_identity_manifest_refuses_original_frozen_hash(self):
        runner, product, commands_file, tested_file, identities = self.preparation_binding_fixture()
        frozen_sha = m.digest(tested_file)
        product.write_bytes(b'untested replacement')
        tested = json.loads(tested_file.read_text())
        tested['identities'][str(product)] = m.digest(product)
        tested_file.write_text(json.dumps(tested))
        with patch.object(m, 'input_identities', return_value=tested['identities']):
            with self.assertRaisesRegex(AssertionError, 'frozen tested manifest changed'):
                m.verify_preparation_bindings(commands_file, tested_file, frozen_sha)

    def test_replaced_required_command_refuses_even_updated_snapshot_hash(self):
        runner, product, commands_file, tested_file, identities = self.preparation_binding_fixture()
        commands = json.loads(commands_file.read_text())
        commands['commands'][2] = [sys.executable, '-c', 'pass']
        commands_file.write_text(json.dumps(commands))
        tested = json.loads(tested_file.read_text()); tested['commandsSha256'] = m.digest(commands_file)
        tested_file.write_text(json.dumps(tested))
        with patch.object(m, 'input_identities', return_value=identities):
            with self.assertRaisesRegex(AssertionError, 'exact required preparation commands changed'):
                m.verify_preparation_bindings(commands_file, tested_file, m.digest(tested_file))

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
