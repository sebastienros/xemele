"""SOURCE-only proving controls. Real child families, counters and timers disabled.
Run only through reviewed launch.py; every introduced session publishes ancestors.
Copied control algorithms follow accepted corpus test_supervisor.py; no peer import.
"""
import sys
sys.dont_write_bytecode = True
import json
import os
import signal
import subprocess
import threading
import time
import unittest
import uuid
from pathlib import Path
from unittest.mock import patch
import bindings as b
import family as f
from family import nested, supervision


class Controls(unittest.TestCase):
    def setUp(self):
        directory = Path(os.environ['XEMELE_IMPORT_RUN_DIRECTORY']) / 'controls'
        directory.mkdir(exist_ok=True)
        self.out = directory / self._testMethodName
        self.out.mkdir(exist_ok=False)

    def runner(self, name='family'):
        return f.Runner(self.out / name, 30)

    def assert_clean(self, runner):
        for record in runner.sequence:
            self.assertTrue(record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'])
            self.assertTrue(record['nestedOwnership']['allOwnedGroupsAbsent'])
            self.assertEqual(record['nestedOwnership']['pendingIntents'], [])
            for group in record['nestedOwnership']['groups']: self.assertEqual(supervision.members(group), [])
        self.assertTrue(runner.receipt['ownership']['allOwnedGroupsAbsent'])

    def test_normal_reaped_all_groups_absent_log_closed(self):
        with self.runner() as runner:
            runner.run([sys.executable, '-c', 'pass'], 15)
        self.assertTrue(runner.receipt['completed']); self.assert_clean(runner)

    def test_timeout_SIGTERM_exit_zero_fails_and_no_next(self):
        ready = self.out / 'ready'; never = self.out / 'next'
        code = "import signal,sys,time\nfrom pathlib import Path\nsignal.signal(signal.SIGTERM,lambda *_:sys.exit(0))\nPath(sys.argv[1]).touch()\nwhile True:time.sleep(.05)"
        runner = self.runner(); runner.work = time.monotonic() + 1
        try:
            result = runner.run([sys.executable, '-c', code, str(ready)], 15, check=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertTrue(ready.exists())
            row = runner.sequence[0]; self.assertEqual(row['reason'], 'TimeoutExpired'); self.assertEqual(row['exit'], 0)
            self.assertFalse(f.clean(row))
            with self.assertRaises(AssertionError): runner.run([sys.executable, '-c', f'from pathlib import Path;Path({str(never)!r}).touch()'], 15)
        finally: runner.close('ExpectedTimeout')
        self.assertEqual(len(runner.sequence), 1); self.assertFalse(never.exists()); self.assert_clean(runner)

    def test_forced_parent_death_separate_session_grandchild_no_next(self):
        ready = self.out / 'inner-ready.json'; parent_dir = self.out / 'intermediate'; never = self.out / 'never'
        payload = ("import os,json,time\nfrom pathlib import Path\n" +
                   f"p=Path({(str(ready)+'.pending')!r});p.write_text(json.dumps({{'parent':os.getppid(),'group':os.getpgrp()}}));p.rename(Path({str(ready)!r}))\nwhile True:time.sleep(.05)")
        parent = ("import os,sys,time\nsys.path.insert(0," + repr(str(b.HERE)) + ")\nfrom family import Runner\n" +
                  f"with Runner({str(parent_dir)!r},45) as runner:runner.run({[sys.executable,'-c',payload]!r},40)\n")
        runner = self.runner(); errors = []
        def kill_parent():
            try:
                deadline = time.monotonic() + 4
                while not ready.exists():
                    if time.monotonic() >= deadline: raise TimeoutError('grandchild readiness missing')
                    time.sleep(.01)
                identity = json.loads(ready.read_text()); registry = runner.directory / 'nested-000'
                groups = [json.loads(p.read_text()) for p in registry.glob('group-*.json')]
                self.assertIn(identity['parent'], [row['pgid'] for row in groups])
                child = next(row for row in groups if row['pgid'] == identity['group'])
                self.assertTrue((registry / ('ack-' + child['token'] + '.json')).exists())
                os.kill(identity['parent'], signal.SIGKILL)
            except BaseException as error: errors.append(repr(error))
        thread = threading.Thread(target=kill_parent); thread.start()
        try:
            runner.run([sys.executable,'-c',parent],15,check=False)
            self.assertEqual(runner.sequence[0]['exit'], -signal.SIGKILL)
            self.assertGreaterEqual(len(runner.sequence[0]['nestedOwnership']['groups']),2)
            with self.assertRaises(AssertionError): runner.run([sys.executable,'-c',f'from pathlib import Path;Path({str(never)!r}).touch()'],15)
        finally:
            thread.join(timeout=1); runner.close('ExpectedForcedParentDeath')
        self.assertFalse(thread.is_alive()); self.assertEqual(errors,[]); self.assertFalse(never.exists()); self.assert_clean(runner)

    def test_atomic_ACK_bootstrap_cannot_execute_partial_record(self):
        registry = self.out / 'atomic'; registry.mkdir()
        owners = nested.registries(os.environ) + [registry]
        token = 'atomic-publication-'+uuid.uuid4().hex; output = self.out / 'once'
        argv = [sys.executable,'-c',f'from pathlib import Path;Path({str(output)!r}).open("a").write("once\\n")']
        for owner in owners: nested.durable(owner / ('intent-'+token+'.json'),dict(token=token,argv=argv,parentPid=os.getpid(),launchDeadline=time.monotonic()+6))
        env = os.environ.copy(); env[nested.REGISTRIES] = json.dumps([str(p) for p in owners])
        log = (self.out / 'bootstrap.log').open('xb'); child = None; writer = None
        entered = threading.Event(); release = threading.Event(); errors = []
        try:
            child = subprocess.Popen([sys.executable,nested.__file__,'--launch',token]+argv,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
            group = dict(token=token,pid=child.pid,pgid=child.pid); deadline = time.monotonic()+2
            while not all(nested.complete_record(owner / ('group-'+token+'.json'),group) for owner in owners):
                if time.monotonic() >= deadline: raise TimeoutError('bootstrap not registered')
                time.sleep(.01)
            ack = dict(token=token,pid=child.pid)
            for owner in owners[:-1]: nested.durable(owner / ('ack-'+token+'.json'),ack)
            real = nested.json.dump
            def paused(value,target,**kwargs):
                if value == ack:
                    entered.set()
                    if not release.wait(timeout=2): raise TimeoutError('publication release missing')
                return real(value,target,**kwargs)
            def publish():
                try: nested.durable(registry / ('ack-'+token+'.json'),ack)
                except BaseException as error: errors.append(repr(error))
            with patch.object(nested.json,'dump',side_effect=paused):
                writer=threading.Thread(target=publish);writer.start();self.assertTrue(entered.wait(timeout=2))
                self.assertFalse((registry / ('ack-'+token+'.json')).exists());self.assertFalse(output.exists());self.assertIsNone(child.poll())
                self.assertEqual(len(list(registry.glob('.publish-*.tmp'))),1)
                release.set();writer.join(timeout=1)
            self.assertFalse(writer.is_alive());self.assertEqual(errors,[]);self.assertEqual(child.wait(timeout=2),0)
            self.assertEqual(output.read_text(),'once\n')
            with self.assertRaises(FileExistsError):nested.durable(registry / ('ack-'+token+'.json'),{'replacement':True})
        finally:
            release.set()
            if writer:writer.join(timeout=1)
            if child:
                supervision.kill_group(child.pid,signal.SIGKILL);child.wait(timeout=1)
            log.close();self.assertTrue(log.closed)
            cleanup=nested.drain(supervision,registry,time.monotonic()+5)
            self.assertTrue(cleanup['allOwnedGroupsAbsent'])

    def test_unpublished_intent_never_clean(self):
        registry=self.out/'pending';registry.mkdir();nested.durable(registry/'intent-unpublished.json',{'token':'unpublished'})
        row=nested.drain(supervision,registry,time.monotonic()+2.1)
        self.assertFalse(row['allOwnedGroupsAbsent']);self.assertEqual(row['pendingIntents'],['unpublished'])

    def test_all_absent_snapshot_after_deadline_never_clean(self):
        registry=self.out/'slow';registry.mkdir();clock=[10.0]
        for group in range(900000,900040):
            token=str(group);nested.durable(registry/('intent-'+token+'.json'),dict(token=token));nested.durable(registry/('group-'+token+'.json'),dict(token=token,pid=group,pgid=group))
        def late(groups,timeout):clock[0]=13.1;return {}
        with patch.object(nested.time,'monotonic',side_effect=lambda:clock[0]),patch.object(supervision,'kill_group'),patch.object(nested,'group_members_snapshot',side_effect=late) as scan:
            row=nested.drain(supervision,registry,15)
        self.assertEqual(scan.call_count,1);self.assertFalse(row['allOwnedGroupsAbsent'])

    def test_changed_tests_dependency_and_commands_refuse_frozen_manifest(self):
        fixture=self.out/'prepared';fixture.mkdir();measurement=self.out/'measurement'
        runtime=fixture/'runtime.dll';runtime.write_bytes(b'unchanged runtime')
        probe=fixture/'Runner.dll';probe.write_bytes(b'unchanged runner')
        dependency=fixture/'Xemele.dll';dependency.write_bytes(b'tested dependency')
        test=fixture/'Test.cs';test.write_bytes(b'tested source')
        receipt=fixture/'family-cleanup.json';nested.durable(receipt,{'completed':True})
        source={'identities':{b.key(test):b.digest(test),b.key(runtime):b.digest(runtime)}}
        manifest=dict(coordinatorReviewSha256='review',harnessSourceSha256=b.digest(b.SOURCE),directory=str(measurement),maximumSeconds=120,workSeconds=100,cleanupReserveSeconds=20,preparationReceipt=str(receipt),identities={b.key(p):b.digest(p) for p in [runtime,probe,dependency,test,receipt]})
        path=fixture/'prepared.json'
        with patch.object(b,'PROBE',probe),patch.object(b,'review',return_value=source):
            manifest['commands']=b.commands(measurement);path.write_text(json.dumps(manifest));sha=b.digest(path)
            b.verify_prepared(path,sha,'review')
            for changed in [test,dependency]:
                original=changed.read_bytes();changed.write_bytes(b'untested replacement')
                self.assertEqual(b.digest(runtime),source['identities'][b.key(runtime)])
                with self.assertRaises(AssertionError):b.verify_prepared(path,sha,'review')
                changed.write_bytes(original)
            manifest['commands'][1]=[sys.executable,'-c','pass'];path.write_text(json.dumps(manifest))
            with self.assertRaisesRegex(AssertionError,'Exact required'):b.verify_prepared(path,b.digest(path),'review')

    def test_failed_correctness_and_final_diff_cannot_publish_passing_receipt(self):
        from types import SimpleNamespace
        for failed in ['correctness','diff']:
            destination=self.out/(failed+'-success.json')
            fake=SimpleNamespace(closed=True,receipt={'completed':True},stopped=False,sequence=[],hard=time.monotonic()+5,directory=self.out)
            checks={'correctness':'passed','diff':'passed'};checks[failed]='failed'
            with self.assertRaises(AssertionError):b.publish_success(destination,fake,checks)
            self.assertFalse(destination.exists())
        # Real nonzero command takes the same family failure path used by the
        # correctness and final diff stages, even if a caller supplies pass labels.
        for stage in ['correctness','diff']:
            runner=self.runner('failed-'+stage)
            try:runner.run([sys.executable,'-c','raise SystemExit(7)'],15,check=False)
            finally:runner.close('ExpectedNonzeroStage')
            destination=self.out/(stage+'-after-nonzero.json')
            with self.assertRaises(AssertionError):b.publish_success(destination,runner,{'correctness':'passed','diff':'passed'})
            self.assertFalse(destination.exists());self.assert_clean(runner)
        failed_family=SimpleNamespace(closed=True,receipt={'completed':False},stopped=True,sequence=[],hard=time.monotonic()+5,directory=self.out)
        with self.assertRaises(AssertionError):b.publish_success(self.out/'unclean-success.json',failed_family,{'diff':'passed'})
        self.assertFalse((self.out/'unclean-success.json').exists())


if __name__ == '__main__':
    assert sys.argv[1:] == ['--released']
    b.review(os.environ['XEMELE_IMPORT_REVIEW_SHA256'])
    with nested.interruptions():
        result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Controls))
        nested.durable(Path(os.environ['XEMELE_IMPORT_RUN_DIRECTORY'])/'positive-controls.json',
                       {'completed':result.wasSuccessful(),'sourceSha256':b.digest(b.SOURCE),'testsRun':result.testsRun,'counterEnabled':False})
        raise SystemExit(0 if result.wasSuccessful() else 1)
