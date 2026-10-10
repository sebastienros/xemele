"""Synthetic protocol controls only: no allocation counters or benchmark executors."""
import copy, importlib.util, json, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('version_measure',Path(__file__).with_name('measure.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

class MeasurementProtocolTests(unittest.TestCase):
    def fixture(self):
        revision='a'*40
        closure={('Xemele.dll' if i==0 else 'Xemele.Benchmarks.dll' if i==1 else 'child.dll' if i==2 else f'payload{i}'):f'{i:064x}' for i in range(47)}
        meta={name:{'Name':name,'Version':'1.0.0.0','FullVersion':'1.0.0+'+revision,'Sha256':closure[name]} for name in ['Xemele.dll','Xemele.Benchmarks.dll','child.dll']}
        cases=[dict(zip(['type','method','corpus','chunked'],c)) for c in sorted(m.expected_cases())]
        known={'revision':revision,'childClosure':closure,'generatedInputIdentities':{'child.csproj':'p','child.notcs':'c','child.sh':'s'},'childMetadata':meta,'caseIdentities':cases,'generatorIdentity':{'Name':'probe'}}
        return known
    def text(self):
        out='// command took 1s and exited with 0\n// command took 1s and exited with 0\n'
        out+='// VersionFrozenChild: '+json.dumps({'child':'/child','project':'/project','files':47,'revision':'a'*40})+'\n'
        for i,case in enumerate(self.fixture()['caseIdentities']):
            out+='// VersionCaseStart: '+json.dumps(case)+'\n// Benchmark: authored case\n'
            for stage in ['Actual','Result']:
                for iteration in [1,2,3]:out+=f'Workload{stage} {iteration}: 100 op, 2000.00 ns, 20.00 ns/op\n'
            out+='// GC: 0 0 0 400 100\n// Runtime=.NET 10.0.11 Arm64 RyuJIT\n'
            out+=f'// Benchmark Process {1000+i} has exited with code 0.\n'
            out+='// VersionCaseResult: '+json.dumps({'identity':case,'exit':0,'process':1000+i,'success':True,'executableFound':True,'errors':[]})+'\n'
        return out+'// VersionExecutedCases: 14\n'
    def test_exact_complete_variant(self):
        k=self.fixture();m.validate_variant(k,k['childClosure'],k['generatedInputIdentities'],k['childMetadata'],k['revision'])
    def test_raw_closure_missing_extra_changed_are_rejected(self):
        k=self.fixture()
        for operation in ['missing','extra','changed']:
            actual=copy.deepcopy(k['childClosure'])
            if operation=='missing':actual.pop('payload3')
            elif operation=='extra':actual['extra']='extra'
            else:actual['Xemele.dll']='bad'
            with self.subTest(operation=operation),self.assertRaises(m.ProtocolError):m.validate_variant(k,actual,k['generatedInputIdentities'],k['childMetadata'],k['revision'])
    def test_generated_notcs_project_script_equality(self):
        k=self.fixture()
        for name in k['generatedInputIdentities']:
            actual=dict(k['generatedInputIdentities']);actual[name]='changed'
            with self.subTest(name=name),self.assertRaises(m.ProtocolError):m.validate_variant(k,k['childClosure'],actual,k['childMetadata'],k['revision'])
    def test_stale_revision_full_version_hash_and_case_rejected(self):
        k=self.fixture()
        for change in ['revision','version','hash','case']:
            altered=copy.deepcopy(k);revision=k['revision']
            if change=='revision':revision='b'*40
            elif change=='version':altered['childMetadata']['Xemele.dll']['FullVersion']='1.0.0+'
            elif change=='hash':altered['childMetadata']['Xemele.dll']['Sha256']='bad'
            else:altered['caseIdentities'][0]['corpus']='unknown'
            with self.subTest(change=change),self.assertRaises(m.ProtocolError):m.validate_variant(k,k['childClosure'],k['generatedInputIdentities'],altered['childMetadata'],revision) if change!='case' else m.validate_variant(altered,k['childClosure'],k['generatedInputIdentities'],k['childMetadata'],revision)
    def test_all_exact_14_cases_42_actual_42_result_14_gc(self):
        result=m.validate_benchmarks(self.text());self.assertEqual((42,42,14),(result['actual'],result['result'],result['gc']));self.assertEqual(14,len(result['cases']))
    def test_missing_extra_duplicate_iteration_records_rejected(self):
        text=self.text()
        alterations=[text.replace('WorkloadActual 1: 100 op, 2000.00 ns, 20.00 ns/op\n','',1),text+'WorkloadResult 4: 100 op, 2000.00 ns, 20.00 ns/op\n',text.replace('WorkloadResult 2:','WorkloadResult 1:',1),text.replace('// GC: 0 0 0 400 100\n','',1),text+'// GC: 0 0 0 400 100\n']
        for i,value in enumerate(alterations):
            with self.subTest(i=i),self.assertRaises(m.ProtocolError):m.validate_benchmarks(value)
    def test_case_identity_exit_runtime_gc_or_result_tampering_rejected(self):
        text=self.text()
        alterations=[text.replace('"corpus": "1.0"','"corpus": "unknown"',1),text.replace('exited with code 0.','exited with code 7.',1),text.replace('"exit": 0','"exit": 7',1),text.replace('"success": true','"success": false',1),text.replace('"executableFound": true','"executableFound": false',1),text.replace('"errors": []','"errors": ["failure"]',1),text.replace('10.0.11','10.0.12',1),text.replace('Arm64','X64',1),text.replace('400 100','0 100',1),text.replace('400 100','400 0',1),text.replace('WorkloadActual 1: 100 op','WorkloadActual 1: 90 op',1),text.replace('20.00 ns/op','0.00 ns/op',1)]
        for i,value in enumerate(alterations):
            with self.subTest(i=i),self.assertRaises(m.ProtocolError):m.validate_benchmarks(value)
    def test_failed_missing_extra_build_exits_rejected(self):
        text=self.text()
        for value in [text.replace('exited with 0','exited with 1',1),text.replace('// command took 1s and exited with 0\n','',1),text+'// command took 1s and exited with 0\n']:
            with self.assertRaises(m.ProtocolError):m.validate_benchmarks(value)
    def test_exact_gc_totals_match_actual_generated_method_oracle(self):
        rows=m.validate_benchmarks(self.text());baseline={c:4 for c in m.expected_cases()};self.assertTrue(m.validate_gc_totals(rows,baseline))
        rows['cases'][0]['gc'][0]['allocatedBytes']+=1
        with self.assertRaises(m.ProtocolError):m.validate_gc_totals(rows,baseline)
    def allocation_text(self,positive=False):
        identity={'Product':{'Sha256':'product'},'Probe':{'Sha256':'probe'}}
        common={'Product':identity['Product'],'Probe':identity['Probe'],'Runtime':'10.0.11','ProcessId':123}
        long=dict(common,Mode='positive' if positive else 'real',CoreBytes=64 if positive else 0,DigitBytes=64 if positive else 0,OwnedBytes=120064,VersionLength=60002,WarmCoreConsumed=50017,WarmFrameConsumed=50017,MeasuredCoreConsumed=10003,MeasuredFrameConsumed=10000,CoreCallbacks=6,RetainedVersion=True)
        ordinary=dict(common,Mode='ordinary-positive' if positive else 'ordinary-real',CoreBytes=64 if positive else 0,OwnedBytes=96 if positive else 32,Callbacks=6)
        return identity,[long,ordinary]
    def test_exact_allocation_totals_real_and_positive(self):
        for positive in [False,True]:
            identity,rows=self.allocation_text(positive);self.assertEqual(rows,m.validate_allocations('\n'.join(map(json.dumps,rows)),positive,identity))
    def test_allocation_retention_counts_types_and_owned_totals_rejected(self):
        identity,rows=self.allocation_text()
        for key,value in [('CoreBytes',1),('CoreBytes',False),('DigitBytes',1),('OwnedBytes',120065),('RetainedVersion',False),('VersionLength',60001),('CoreCallbacks',5),('MeasuredCoreConsumed',10000),('WarmFrameConsumed',50016)]:
            altered=copy.deepcopy(rows);altered[0][key]=value
            with self.subTest(key=key,value=value),self.assertRaises(m.ProtocolError):m.validate_allocations('\n'.join(map(json.dumps,altered)),False,identity)
    def lifecycle(self,positive=False):
        k=self.fixture();cases=[]
        for identity in k['caseIdentities']:
            t,method,corpus,b=m.case_id(identity);length=4098 if corpus=='long' else len(corpus)
            expected=(22 if corpus=='invalid' else length+28) if t=='DeclarationVersionBenchmarks' else (16 if corpus=='invalid' else 24 if corpus=='absent-version' else length+35)
            perop=4+(64 if positive else 0)
            cases.append({'identity':identity,'iterations':512,'expectedValue':expected,'consumed':512*expected,'allocatedBytes':512*perop,'bytesPerOperation':perop})
        return dict(mode='lifecycle-positive' if positive else 'lifecycle-real',cases=cases,product=k['childMetadata']['Xemele.dll'],benchmark=k['childMetadata']['Xemele.Benchmarks.dll'],probe=k['generatorIdentity'],runtime='10.0.11',architecture='Arm64',processId=124 if positive else 123)
    def test_lifecycle_real_positive_and_independent_consumption(self):
        for positive in [False,True]:self.assertEqual(14,len(m.validate_lifecycle(json.dumps(self.lifecycle(positive)),positive,self.fixture())['cases']))
        altered=self.lifecycle();altered['cases'][0]['consumed']+=1
        with self.assertRaises(m.ProtocolError):m.validate_lifecycle(json.dumps(altered),False,self.fixture())
    def test_durable_baseline_revalidated_and_positive_delta_exact(self):
        k=self.fixture();summary=dict(result='passed',mode='allocations',revision=k['revision'],preparationSha256='p',lifecycleAllocations=[self.lifecycle(),self.lifecycle(True)],before={'frozen':True},after={'frozen':True})
        self.assertEqual({c:4 for c in m.expected_cases()},m.allocation_baselines(summary,k,'p'))
        summary['lifecycleAllocations'][1]['cases'][0]['bytesPerOperation']=69;summary['lifecycleAllocations'][1]['cases'][0]['allocatedBytes']=69*512
        with self.assertRaises(m.ProtocolError):m.allocation_baselines(summary,k,'p')
    def test_total_build_and_child_deadlines_terminate_reap_preserve_failure(self):
        class Process:
            pid=123
            def poll(self):return None
            def wait(self,timeout):self.reaped=True;return -15
        for stage in ['total','build','benchmark-child']:
            with self.subTest(stage=stage),tempfile.TemporaryDirectory() as d:
                log=Path(d)/'log';log.write_text('// VersionCaseStart: {}\n' if stage=='benchmark-child' else '')
                times=[0,0,2] if stage=='benchmark-child' else [0,2]
                process=Process();summary={}
                with patch.object(m.os,'killpg',side_effect=[None,None,ProcessLookupError()]) as kill,patch.object(m.time,'monotonic',side_effect=times):
                    with self.assertRaises(TimeoutError):m.bounded_wait(process,log,summary,total_seconds=1 if stage=='total' else 10,child_seconds=1 if stage=='benchmark-child' else 10,build_seconds=1 if stage=='build' else 10 if stage=='benchmark-child' else None)
                self.assertEqual(stage,summary['timeout']['stage']);self.assertTrue(process.reaped);self.assertTrue(summary['terminatedProcessGroup']);self.assertEqual([m.signal.SIGTERM,m.signal.SIGKILL,0],[c.args[1] for c in kill.call_args_list])
    def test_cleanup_denial_preserves_primary_and_attempts_remaining_paths(self):
        with tempfile.TemporaryDirectory() as d:
            paths=[Path(d)/n for n in ['a','b']]
            for p in paths:p.mkdir()
            historical=Path(d)/'historical';historical.mkdir()
            error={'stage':'measurement','type':'TimeoutError','message':'primary timeout'};summary={'result':'failed','failure':error}
            def remove(path):
                if path.name=='a':raise PermissionError('injected denial')
                path.rmdir()
            m.cleanup(summary,paths,remove);self.assertIs(error,summary['failure']);self.assertFalse(paths[1].exists());self.assertTrue(historical.exists());self.assertFalse(summary['temporaryOutputsRemoved']);json.dumps(summary)
    def test_initial_failure_gets_durable_summary_and_removes_only_raw(self):
        with tempfile.TemporaryDirectory() as d:
            report=Path(d);raw=report/'.measurement';raw.mkdir();(raw/'partial.log').write_text('partial');historical=report/'history.json';historical.write_text('historical')
            with patch.object(m,'REPORT',report),patch.object(m,'RAW',raw),patch.object(m.shutil,'which',return_value='/usr/bin/true'):
                self.assertEqual(1,m.main(['--exclusive-grant','synthetic-control-only','--mode','allocations']))
            summary=json.loads((report/'measurement-allocations-summary.json').read_text());self.assertEqual('FileNotFoundError',summary['failure']['type']);self.assertTrue(summary['temporaryOutputsRemoved']);self.assertEqual('historical',historical.read_text())
            with patch.object(m,'REPORT',report),patch.object(m,'RAW',raw):
                with self.assertRaises(m.ProtocolError):m.main(['--exclusive-grant','synthetic-control-only','--mode','allocations'])

    def test_summary_write_denial_keeps_primary_failure_and_cleanup(self):
        import io
        with tempfile.TemporaryDirectory() as d:
            report=Path(d);raw=report/'.measurement';raw.mkdir();(raw/'partial').write_text('partial')
            original=Path.write_text
            def write(path,*args,**kwargs):
                if path.name=='measurement-allocations-summary.json':raise PermissionError('injected summary-write denial')
                return original(path,*args,**kwargs)
            captured=io.StringIO()
            with patch.object(m,'REPORT',report),patch.object(m,'RAW',raw),patch.object(m.shutil,'which',return_value='/usr/bin/true'),patch.object(Path,'write_text',write),patch('sys.stdout',captured):
                self.assertEqual(1,m.main(['--exclusive-grant','synthetic-control-only','--mode','allocations']))
            summary=json.loads(captured.getvalue());self.assertEqual('FileNotFoundError',summary['failure']['type']);self.assertEqual('PermissionError',summary['summaryWriteError']['type']);self.assertTrue(summary['temporaryOutputsRemoved']);self.assertFalse(raw.exists())

    def test_variant_mismatch_actual_expected_maps_survive_cleanup(self):
        known=self.fixture();actual=dict(known['childClosure']);actual['Xemele.dll']='changed';evidence={}
        with tempfile.TemporaryDirectory() as d:
            raw=Path(d)/'raw';raw.mkdir();(raw/'child.dll').write_text('transient');summary={'result':'failed','variant':evidence}
            with self.assertRaises(m.ProtocolError) as failure:m.validate_variant(known,actual,known['generatedInputIdentities'],known['childMetadata'],known['revision'],evidence)
            summary['failure']={'type':type(failure.exception).__name__,'message':str(failure.exception)};m.cleanup(summary,[raw])
            saved=Path(d)/'summary.json';saved.write_text(json.dumps(summary));restored=json.loads(saved.read_text())
            self.assertEqual('changed',restored['variant']['actualClosure']['Xemele.dll']);self.assertEqual(known['childClosure']['Xemele.dll'],restored['variant']['expectedClosure']['Xemele.dll']);self.assertEqual(known['childMetadata'],restored['variant']['actualMetadata']);self.assertTrue(restored['temporaryOutputsRemoved'])
    def test_streamed_csharp_mismatch_survives_malformed_case_parse(self):
        mismatch={'kind':'child-closure','expected':{'a':'expected'},'actual':{'a':'changed'},'equal':False}
        metadata={'name':'child.dll','expected':{'FullVersion':'expected'},'actual':{'FullVersion':'changed'},'equal':False}
        text='// VersionIdentityCheck: '+json.dumps(mismatch)+'\n// VersionMetadataCheck: '+json.dumps(metadata)+'\n// VersionCaseStart: {truncated\n'
        captured=m.identity_records(text)
        with self.assertRaises(json.JSONDecodeError):m.observations(text)
        with tempfile.TemporaryDirectory() as d:
            raw=Path(d)/'raw';raw.mkdir();(raw/'log').write_text(text);summary={'result':'failed','streamed':captured,'failure':{'message':'primary parse failure'}};m.cleanup(summary,[raw]);saved=json.loads(json.dumps(summary))
            self.assertEqual([mismatch],saved['streamed']['identityChecks']);self.assertEqual([metadata],saved['streamed']['metadataChecks']);self.assertTrue(saved['temporaryOutputsRemoved']);self.assertEqual('primary parse failure',saved['failure']['message'])
    def runtime_fixture(self):
        directory='/shared/Microsoft.NETCore.App/10.0.11';closure={f'file{i}.dll':f'{i:064x}' for i in range(110)}
        closure.update({'System.Private.CoreLib.dll':'core','libcoreclr.dylib':'native','Microsoft.NETCore.App.deps.json':'metadata'})
        return dict(runtime='10.0.11',architecture='Arm64',processPath='/sdk/dotnet',processSha256='host',runtimeDirectory=directory,coreLib=dict(path=directory+'/System.Private.CoreLib.dll',fullName='System.Private.CoreLib, Version=10.0.0.0',version='10.0.0.0',fullVersion='10.0.11+actual',mvid='loaded-mvid',sha256='core'),closure=closure)
    def test_actual_runtime_native_bcl_metadata_and_loaded_path_changes_rejected(self):
        known=self.runtime_fixture();self.assertTrue(m.validate_runtime(known,known))
        for change in ['native','bcl','metadata','loaded-path','mvid','version','extra','missing']:
            actual=copy.deepcopy(known);evidence={}
            if change=='native':actual['closure']['libcoreclr.dylib']='changed'
            elif change=='bcl':actual['closure']['file3.dll']='changed'
            elif change=='metadata':actual['closure']['Microsoft.NETCore.App.deps.json']='changed'
            elif change=='loaded-path':actual['coreLib']['path']='/wrong/CoreLib.dll'
            elif change=='mvid':actual['coreLib']['mvid']='changed'
            elif change=='version':actual['runtime']='10.0.12'
            elif change=='extra':actual['closure']['extra']='extra'
            else:actual['closure'].pop('file3.dll')
            with self.subTest(change=change),self.assertRaises(m.ProtocolError):m.validate_runtime(known,actual,evidence)
            self.assertEqual(actual,evidence['actual']);self.assertEqual(known,evidence['expected'])
    def test_zero_exit_parent_residual_descendant_is_terminated_and_fails(self):
        class Process:
            pid=4321
            def poll(self):return 0
            def wait(self,timeout):return 0
        alive=True;signals=[]
        def kill(pid,sig):
            nonlocal alive
            signals.append(sig)
            if sig==m.signal.SIGKILL:alive=False
            if sig==0 and not alive:raise ProcessLookupError()
        with tempfile.TemporaryDirectory() as d:
            log=Path(d)/'log';log.write_text('');summary={}
            with patch.object(m.os,'killpg',side_effect=kill),patch.object(m.time,'monotonic',side_effect=[0,0,2]):
                with self.assertRaises(m.ProtocolError):m.bounded_wait(Process(),log,summary,total_seconds=10,child_seconds=10)
            self.assertFalse(alive);self.assertEqual([0,m.signal.SIGTERM,m.signal.SIGKILL,0],signals);self.assertEqual([{'pid':4321,'exit':0}],summary['parentExits']);self.assertTrue(summary['processGroups'][0]['residualDescendants']);self.assertTrue(summary['terminatedProcessGroup'])
    def test_zero_exit_parent_empty_group_is_verified(self):
        class Process:
            pid=5432
            def poll(self):return 0
            def wait(self,timeout):return 0
        with tempfile.TemporaryDirectory() as d:
            log=Path(d)/'log';log.write_text('');summary={}
            with patch.object(m.os,'killpg',side_effect=ProcessLookupError()) as kill:
                self.assertEqual(0,m.bounded_wait(Process(),log,summary,total_seconds=10,child_seconds=10))
            self.assertEqual([((5432,0),{})],[(c.args,c.kwargs) for c in kill.call_args_list]);self.assertEqual([{'pid':5432,'emptyAfterReap':True}],summary['processGroups'])

    def test_partial_actual_gc_records_survive_later_case_parse_failure(self):
        text=self.text()+'// VersionCaseStart: {truncated\n';retained=[]
        with self.assertRaises(json.JSONDecodeError):m.observations(text,retained)
        self.assertEqual(15,len(retained));self.assertEqual(42,sum(len(r['actual']) for r in retained));self.assertEqual(14,sum(len(r['gc']) for r in retained));self.assertIn('identityParseFailure',retained[-1]);json.dumps(retained)

if __name__=='__main__':unittest.main()
