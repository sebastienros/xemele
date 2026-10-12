"""15 proving controls. Scratch only; actual child families, no .NET/counters."""
import sys
sys.dont_write_bytecode=True
import json
import os
import signal
import subprocess
import threading
import time
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import bindings as b
import family as f
import recovery
import terminal
import selection
import xml.etree.ElementTree as ET
from family import nested,supervision
from birth import identity,assert_same_or_absent


class Controls(unittest.TestCase):
    def setUp(self):
        self.root=Path(os.environ['XEMELE_MISSING_RUN_DIRECTORY'])/'controls'/self._testMethodName
        self.root.mkdir(parents=True,exist_ok=False)
    def runner(self,name='family'):
        return f.Runner(self.root/name,35,cleanup_reserve=10)
    def clean(self,runner):
        self.assertTrue(runner.receipt['ownership']['allOwnedGroupsAbsent'])
        for row in runner.sequence:
            self.assertTrue(row['directChildReaped'] and row['ownedGroupAbsent'] and row['logClosed'])
            self.assertTrue(row['nestedOwnership']['allOwnedGroupsAbsent'])
    def test_01_normal_reaped(self):
        with self.runner() as runner:runner.run([sys.executable,'-c','pass'],15)
        self.assertTrue(runner.receipt['completed']);self.clean(runner)
    def test_02_timeout_TERM_zero_blocks_next(self):
        code='import signal,sys,time\nsignal.signal(signal.SIGTERM,lambda *_:sys.exit(0))\nwhile True:time.sleep(.01)'
        runner=self.runner();runner.work=time.monotonic()+3
        try:
            self.assertNotEqual(runner.run([sys.executable,'-c',code],15,check=False).returncode,0)
            self.assertEqual(runner.sequence[0]['exit'],0);self.assertEqual(runner.sequence[0]['reason'],'TimeoutExpired')
            with self.assertRaises(AssertionError):runner.run([sys.executable,'-c','pass'],15)
        finally:runner.close('ExpectedTimeout')
        self.clean(runner)
    def test_03_ignoring_TERM_descendant_is_killed(self):
        inner=self.root/'inner';ready=self.root/'ready'
        payload='import signal,time\nfrom pathlib import Path\nsignal.signal(signal.SIGTERM,signal.SIG_IGN)\nPath('+repr(str(ready))+').touch()\nwhile True:time.sleep(.01)'
        code='import sys\nsys.path.insert(0,'+repr(str(b.HERE))+')\nfrom family import Runner\nwith Runner('+repr(str(inner))+',30,cleanup_reserve=10) as r:r.run('+repr([sys.executable,'-c',payload])+',15)'
        runner=self.runner();runner.work=time.monotonic()+4
        try:self.assertNotEqual(runner.run([sys.executable,'-c',code],15,check=False).returncode,0)
        finally:runner.close('ExpectedTimeout')
        self.assertTrue(ready.exists());self.clean(runner)
    def test_04_parent_death_reparented_group(self):
        ready=self.root/'ready.json';inner=self.root/'inner';errors=[]
        payload='import os,json,time\nfrom pathlib import Path\np=Path('+repr(str(ready)+'.tmp')+');p.write_text(json.dumps({"parent":os.getppid(),"group":os.getpgrp()}));p.rename('+repr(str(ready))+')\nwhile True:time.sleep(.01)'
        code='import sys\nsys.path.insert(0,'+repr(str(b.HERE))+')\nfrom family import Runner\nwith Runner('+repr(str(inner))+',30,cleanup_reserve=10) as r:r.run('+repr([sys.executable,'-c',payload])+',15)'
        runner=self.runner()
        def killer():
            try:
                end=time.monotonic()+5
                while not ready.exists():
                    if time.monotonic()>=end:raise TimeoutError('readiness')
                    time.sleep(.01)
                value=json.loads(ready.read_text());groups=[json.loads(p.read_text()) for p in (runner.directory/'nested-000').glob('group-*.json')]
                parent=next(r for r in groups if r['pid']==value['parent']);assert_same_or_absent(parent['birth'])
                self.assertIn(value['group'],[r['pid'] for r in groups]);os.kill(parent['pid'],signal.SIGKILL)
            except BaseException as caught:errors.append(type(caught).__name__)
        thread=threading.Thread(target=killer);thread.start()
        try:self.assertNotEqual(runner.run([sys.executable,'-c',code],15,check=False).returncode,0)
        finally:thread.join(1);runner.close('ExpectedParentDeath')
        self.assertFalse(thread.is_alive());self.assertEqual(errors,[]);self.clean(runner)
    def test_05_TERM_during_atomic_publication_never_executes(self):
        candidate=self.root/'candidate.json';marker=self.root/'payload-ran';ready=self.root/'ready'
        code='import sys,time,signal\nsys.path.insert(0,'+repr(str(b.HERE))+')\nfrom pathlib import Path\nimport nested_ownership as n\nreal=n.json.dump\ndef paused(value,target,**kw):\n Path('+repr(str(ready))+').touch()\n while True:time.sleep(.01)\nn.json.dump=paused\nwith n.interruptions():n.durable(Path('+repr(str(candidate))+'),{"completed":True})\nPath('+repr(str(marker))+').touch()'
        runner=self.runner();runner.work=time.monotonic()+3
        try:self.assertNotEqual(runner.run([sys.executable,'-c',code],15,check=False).returncode,0)
        finally:runner.close('ExpectedPublicationInterrupt')
        self.assertTrue(ready.exists());self.assertFalse(candidate.exists());self.assertFalse(marker.exists());self.clean(runner)
    def test_06_birth_mismatch_refuses_group_signal(self):
        old={'pid':12345,'startMicroseconds':1}
        with patch('birth.identity',return_value={'pid':12345,'startMicroseconds':2}):
            with self.assertRaises(RuntimeError):assert_same_or_absent(old)
        with patch('birth.identity',return_value=None):assert_same_or_absent(old)
    def test_07_expired_deadline_never_launches(self):
        runner=self.runner();runner.work=time.monotonic()-1
        with patch.object(supervision.subprocess,'Popen') as launch:
            try:
                with self.assertRaises(TimeoutError):runner.run([sys.executable,'-c','pass'],15)
                self.assertFalse(launch.called)
            finally:runner.close('ExpectedExpiredDeadline')
        self.assertFalse(runner.receipt['completed'])
    def test_08_late_publication_quarantined(self):
        path=self.root/'success.json';clock=[10.0];real=nested.durable
        def late(p,value):real(p,value);clock[0]=13.0
        with patch.object(f.time,'monotonic',side_effect=lambda:clock[0]),patch.object(nested,'durable',side_effect=late):
            with self.assertRaises(TimeoutError):f.publish_finite(path,{'completed':True},12.0)
        self.assertFalse(path.exists());self.assertEqual(len(list(self.root.glob('success.json.invalid-*'))),1)
    def test_09_review_source_and_plan_mutations_reject(self):
        source=self.root/'source.json';review=self.root/'review.json';input=self.root/'Source.cs';input.write_text('reviewed source')
        harness=self.root/'scratch-harness';harness.mkdir();module=harness/'source.py';module.write_text('pass')
        configuration=self.root/'configuration.props'
        runtime_sha=b.digest(b.REPORT/'source-manifest.json')
        manifest=dict(identities={b.key(input):b.digest(input)},apiInputs={},sourceMembers=[],toolchainDirectories=[],toolchainDirectoryInputs={},
                      packageDirectories=[],packageInputs={},harnessMembers=[b.key(module)],configurationPresence={b.key(configuration):False},
                      commandRecipes=[['exact','argv']],runtimeSourceManifestSha256=runtime_sha,runtimeSourceCommit='commit')
        manifest['commandRecipesSha256']=b.identity_sha(manifest['commandRecipes']);source.write_text(json.dumps(manifest))
        review.write_text(json.dumps(dict(result='SOURCE PASS',harnessSourceSha256=b.digest(source),runtimeSourceManifestSha256=runtime_sha,runtimeSourceCommit='commit',pythonExecutableSha256=b.digest(sys.executable),allowedActions=['controls'])))
        sha=b.digest(review)
        with patch.object(b,'SOURCE',source),patch.object(b,'REVIEW',review),patch.object(b,'HERE',harness),patch.object(b,'api_inputs',return_value={}),patch.object(b,'source_members',return_value=[]):
            b.review(sha)
            extra=harness/'unexpected.txt';extra.write_text('membership drift')
            with self.assertRaises(AssertionError):b.review(sha)
            extra.unlink();b.review(sha)
            configuration.write_text('<Project/>')
            with self.assertRaises(AssertionError):b.review(sha)
            configuration.unlink();b.review(sha)
            input.write_text('changed source')
            with self.assertRaises(AssertionError):b.review(sha)
            input.write_text('reviewed source');manifest['commandRecipes'][0]=['changed','plan'];source.write_text(json.dumps(manifest))
            with self.assertRaises(AssertionError):b.review(sha)
    def test_10_prelaunch_binary_replacement_and_membership_refuse_next(self):
        binary=self.root/'bin';binary.mkdir();dll=binary/'Test.dll';dep=binary/'Xemele.dll';dll.write_bytes(b'test');dep.write_bytes(b'product')
        values=b.directory_inputs([binary]);values.update(b.identities([b.DOTNET,sys.executable]));b.record_execution(self.root,0,['Test.dll'],values,values,[b.key(binary)])
        b.assert_prior_execution(self.root);dep.write_bytes(b'changed')
        with self.assertRaises(AssertionError):b.assert_prior_execution(self.root)
        dep.write_bytes(b'product');extra=binary/'new.dll';extra.write_bytes(b'new')
        with self.assertRaises(AssertionError):b.assert_prior_execution(self.root)
        extra.unlink();b.assert_prior_execution(self.root)
        runner=self.runner()
        try:
            with patch.object(b,'assert_prior_execution',side_effect=AssertionError('replacement')):
                with self.assertRaises(AssertionError):runner.run([sys.executable,'-c','pass'],15)
            with self.assertRaises(AssertionError):runner.run([sys.executable,'-c','pass'],15)
        finally:runner.close('ExpectedPrelaunchBindingFailure')
    def test_11_missing_and_malformed_positive_receipts_fail(self):
        path=self.root/'positive.json'
        with self.assertRaises(OSError):b.positive_controls(path,'missing','review')
        path.write_text('{')
        with self.assertRaises(json.JSONDecodeError):b.positive_controls(path,b.digest(path),'review')
    def test_12_unclean_family_blocks_deletion_and_success(self):
        raw=self.root/'command-0.log';raw.write_bytes(b'must remain until absence')
        fake=SimpleNamespace(directory=self.root,closed=True,stopped=False,sequence=[],hard=time.monotonic()+20,receipt={'completed':True,'ownership':{'allOwnedGroupsAbsent':False}})
        with self.assertRaises(AssertionError):terminal.cleanup(fake,'check')
        with self.assertRaises(AssertionError):terminal.publish_controller(fake,'check',{'payload':'sha'},'review')
        self.assertTrue(raw.exists());self.assertFalse((self.root/'controller-release.json').exists())
        for outcome in ['normal','failure','discard','recovery']:
            root=self.root/outcome;root.mkdir()
            raw=[root/'raw.json',root/'payload.bin',root/'extensionless',root/'nested'/'unknown.json']
            for path in raw:path.parent.mkdir(exist_ok=True);path.write_bytes(b'raw must disappear')
            cleanrow=dict(directChildReaped=True,ownedGroupAbsent=True,logClosed=True,nestedOwnership={'allOwnedGroupsAbsent':True})
            fake=SimpleNamespace(directory=root,closed=True,stopped=False,sequence=[cleanrow],hard=time.monotonic()+15,
                                 receipt=dict(completed=outcome=='normal',ownership={'allOwnedGroupsAbsent':True}))
            receipt='recovery-terminal.json' if outcome=='recovery' else 'terminal-cleanup.json'
            row=terminal.cleanup(fake,outcome,error=None if outcome=='normal' else 'ExpectedFailure',receipt=receipt)
            self.assertTrue(all(not path.exists() for path in raw));terminal.verify_membership(row,[root/receipt])
            survivor=root/'unexpected.json';survivor.write_text('must block release')
            with self.assertRaises(AssertionError):terminal.verify_membership(row,[root/receipt])
            survivor.unlink()
        root=self.root/'active-prepared';active=root/'preparation/active';active.mkdir(parents=True)
        binary=active/'Probe.dll';binary.write_bytes(b'exact owned active fixture')
        manifest=root/'preparation/prepared.json';nested.durable(manifest,dict(activeOutputIdentities=b.directory_inputs([active])))
        fake=SimpleNamespace(directory=root,closed=True,stopped=False,sequence=[cleanrow],hard=time.monotonic()+15,
                             receipt=dict(completed=True,ownership={'allOwnedGroupsAbsent':True}))
        row=terminal.cleanup(fake,'prepare',preserve=True);self.assertTrue(binary.exists());terminal.verify_membership(row,[root/'terminal-cleanup.json'])
        (active/'unknown.bin').write_bytes(b'unexpected survivor')
        with self.assertRaises(AssertionError):terminal.verify_membership(row,[root/'terminal-cleanup.json'])
        terminal.abort_preparation(fake,'ExpectedAbort');self.assertFalse(active.exists());self.assertFalse(manifest.exists())
        # A real closed command family discards retained prepared bytes while
        # its measurement directory has never been created. Publication must
        # succeed with that absence, then reject recreation of the absent root.
        runner=self.runner('prepared-discard')
        prior=self.root/'discard-prepared';probe=prior/'active/bin/Probe.dll';probe.parent.mkdir(parents=True);probe.write_bytes(b'owned prepared fixture')
        measurement=self.root/'never-started-measurement'
        try:
            runner.run([sys.executable,'-c','pass'],15)
        finally:runner.close()
        self.clean(runner);self.assertTrue(runner.receipt['completed'])
        payload=runner.directory/'discard-summary.json'
        nested.durable(payload,dict(completed=True,controller=str(runner.directory),state='discarded; never reusable'))
        row=terminal.cleanup(runner,'discard',prepared=dict(probePath=str(probe),measurementDirectory=str(measurement)))
        self.assertFalse(probe.exists());self.assertFalse((prior/'active').exists());self.assertFalse(measurement.exists())
        review=os.environ['XEMELE_MISSING_REVIEW_SHA256']
        terminal.publish_controller(runner,'discard',{b.key(payload):b.digest(payload)},review)
        extras=[runner.directory/'terminal-cleanup.json',runner.directory/'controller-release.json']
        terminal.verify_membership(row,extras)
        measurement.mkdir()
        with self.assertRaises(AssertionError):terminal.verify_membership(row,extras)
        (measurement/'unknown.json').write_text('must not become a retained receipt')
        with self.assertRaises(AssertionError):terminal.verify_membership(row,extras)
    def test_13_SIGKILL_exact_matched_coordinator_recovers_live_orphan(self):
        inner=self.root/'inner';ready=self.root/'ready.json';log=None;child=None;fallback=self.runner('fallback')
        payload='import os,json,time\nfrom pathlib import Path\nimport sys\nsys.path.insert(0,'+repr(str(b.HERE))+')\nfrom birth import identity\np=Path('+repr(str(ready)+'.tmp')+');p.write_text(json.dumps({"pid":os.getpid(),"birth":identity(os.getpid())}));p.rename('+repr(str(ready))+')\nwhile True:time.sleep(.01)'
        code='import sys,os,json\nsys.path.insert(0,'+repr(str(b.HERE))+')\nfrom pathlib import Path\nimport bindings as b\nfrom family import Runner,nested\nfrom birth import identity\nr=Runner('+repr(str(inner))+',30,cleanup_reserve=10)\n(r.directory/"raw.json").write_text("opaque raw must disappear")\n(r.directory/"payload.bin").write_bytes(b"raw payload")\n(r.directory/"extensionless").write_text("raw payload")\nnested.durable(r.directory/"owner.json",dict(controllerBirth=identity(os.getpid()),coordinator='+repr(recovery.COORDINATOR)+',coordinatorNonce="control13",sourceSha256=b.digest(b.SOURCE),reviewSha256=os.environ["XEMELE_MISSING_REVIEW_SHA256"],action="check",directory=str(r.directory),registry=str(r.registry),argv=[]))\nwith r:r.run('+repr([sys.executable,'-c',payload])+',15)'
        argv=[sys.executable,'-c',code]
        try:
            b.review(os.environ['XEMELE_MISSING_REVIEW_SHA256'])
            from diagnostics import capture_inputs
            capture_inputs(fallback.directory,0,argv,fallback.environment)
            log=(fallback.directory/'bootstrap.log').open('xb')
            # Registration only here. Automatic supervise/drain would kill the
            # orphan before the recovery operation under test could prove it.
            with nested.track_launches(supervision,fallback.environment,fallback.work):
                child=subprocess.Popen(argv,cwd=b.ROOT,env=fallback.environment,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
            end=min(time.monotonic()+6,fallback.work)
            while not ready.exists():
                if time.monotonic()>=end:raise TimeoutError('orphan readiness')
                time.sleep(.01)
            orphan=json.loads(ready.read_text());owner=inner/'owner.json';sha=b.digest(owner);owner_row=json.loads(owner.read_text())
            groups=[json.loads(p.read_text()) for p in fallback.registry.glob('group-*.json')]
            registration=next(row for row in groups if row['pid']==orphan['pid'])
            self.assertEqual(registration['birth'],orphan['birth'])
            self.assertTrue((fallback.registry/('ack-'+registration['token']+'.json')).exists())
            assert_same_or_absent(owner_row['controllerBirth']);child.kill();child.wait(timeout=1);log.close()
            self.assertIsNone(identity(child.pid));self.assertEqual(identity(orphan['pid']),orphan['birth'])
            review=os.environ['XEMELE_MISSING_REVIEW_SHA256']
            with self.assertRaises(AssertionError):recovery.recover(owner,'0'*64,'control13',review,15)
            self.assertEqual(identity(orphan['pid']),orphan['birth']);self.assertTrue((inner/'payload.bin').exists())
            with self.assertRaises(AssertionError):recovery.recover(owner,sha,'wrong-nonce',review,15)
            self.assertEqual(identity(orphan['pid']),orphan['birth']);self.assertFalse((inner/'recovery-intent.json').exists())
            self.assertIn(orphan['pid'],supervision.members(registration['pgid'])) # live immediately before matched recovery
            result=recovery.recover(owner,sha,'control13',review,15)
            self.assertTrue(result['completed']);self.assertIsNone(identity(orphan['pid']))
            self.assertFalse((inner/'payload.bin').exists());self.assertFalse((inner/'raw.json').exists());self.assertFalse((inner/'extensionless').exists())
            self.assertFalse((inner/'controller-release.json').exists())
            self.assertFalse(json.loads((inner/'recovery-result.json').read_text())['originalPhaseAccepted'])
        finally:
            # Independent bounded ancestor fallback still runs if readiness,
            # wrong-owner checks or the recovery implementation itself fails.
            try:
                if child is not None:
                    child.kill();child.wait(timeout=1)
            finally:
                if log is not None and not log.closed:log.close()
                fallback.close('ExpectedSIGKILLControl; ancestor fallback')
        self.assertTrue(fallback.receipt['ownership']['allOwnedGroupsAbsent'])
    def test_14_exact_selected_coverage_rejects_omissions_skip_duplicates(self):
        expectation=json.loads(selection.EXPECTED.read_text());tests=[]
        for name,row in expectation['methods'].items():
            type_name,method=name.rsplit('.',1)
            for index in range(row['cases']):tests.append(ET.Element('test',id=str(len(tests)),type=type_name,method=method,name=name+'('+str(index)+')',result='Pass'))
        selection.validate(tests,expectation)
        witness=next(t for t in tests if t.attrib['method']=='CompleteSubstitutionChoiceCannotRestrictAnUnrelatedLocalLeaf')
        with self.assertRaises(AssertionError):selection.validate([t for t in tests if t is not witness],expectation)
        with self.assertRaises(AssertionError):selection.validate([t for t in tests if not t.attrib['type'].startswith('Xemele.Tests.M8Regex.')],expectation)
        theory=next(t for t in tests if t.attrib['method']=='PartialUnionConstraintRejectsOnlyWhenEveryMemberIndependentlyRejects')
        with self.assertRaises(AssertionError):selection.validate([t for t in tests if t is not theory],expectation)
        previous=theory.attrib['result'];theory.attrib['result']='Skip'
        with self.assertRaises(AssertionError):selection.validate(tests,expectation)
        theory.attrib['result']=previous
        duplicate=ET.Element('test',**tests[0].attrib)
        with self.assertRaises(AssertionError):selection.validate(tests+[duplicate],expectation)
    def test_15_initial_birth_exception_and_none_cleanup_exact_spawned_handle(self):
        real=nested.birth_identity
        for mode in ['exception','none']:
            marker=self.root/(mode+'-payload');runner=self.runner(mode)
            def inspected(pid):
                if pid==os.getpid():return real(pid)
                if mode=='exception':raise LookupError('initial parent-side inspection fixture')
                return None
            try:
                with patch.object(nested,'birth_identity',side_effect=inspected):
                    self.assertNotEqual(runner.run([sys.executable,'-c','from pathlib import Path;Path('+repr(str(marker))+').touch()'],15,check=False).returncode,0)
                row=runner.sequence[0];failure=row['bootstrapRegistrationFailures'][0]
                self.assertEqual(failure['originalError'],'LookupError' if mode=='exception' else 'RuntimeError')
                self.assertTrue(failure['directChildReaped'] and failure['ownedGroupAbsent']);self.assertIsNone(identity(failure['pid']))
                self.assertFalse(marker.exists())
                with self.assertRaises(AssertionError):runner.run([sys.executable,'-c','pass'],15)
            finally:runner.close('ExpectedBirthInspectionFailure')
            self.clean(runner)



if __name__=='__main__':
    assert sys.argv[1:]==['--root']
    b.review(os.environ['XEMELE_MISSING_REVIEW_SHA256'])
    with nested.interruptions():
        result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Controls))
        assert result.testsRun==b.CONTROL_COUNT
        nested.durable(Path(os.environ['XEMELE_MISSING_RUN_DIRECTORY'])/'positive-controls.json',dict(completed=result.wasSuccessful(),
            controller=os.environ['XEMELE_MISSING_RUN_DIRECTORY'],testsRun=result.testsRun,sourceSha256=b.digest(b.SOURCE),counterEnabled=False))
        raise SystemExit(0 if result.wasSuccessful() else 1)
