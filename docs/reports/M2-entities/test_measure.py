import importlib.util,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('entity_measure',Path(__file__).with_name('measure.py'));measure=importlib.util.module_from_spec(spec);spec.loader.exec_module(measure)
class MeasurementProtocolTests(unittest.TestCase):
 def test_selected_failure_summary_keeps_per_case_counts_and_failed_exit(self):
  text='// Benchmark: EntityCompositionBenchmarks.InternalDocument: declared [Length=16]\nWorkloadActual   1: values\nWorkloadResult   1: values\n// GC: values\n// Runtime=.NET 10.0.11\n// GC=Concurrent Workstation\n// Benchmark Process 123 has exited with code 7.\n'
  row=measure.selected_observations(text)[0];self.assertEqual(('EntityCompositionBenchmarks','InternalDocument','16'),row['case']);self.assertEqual(1,row['workloadActual']);self.assertEqual(1,row['workloadResult']);self.assertEqual(1,row['gcRecords']);self.assertEqual([('123','7')],row['childExits'])
 def test_initial_provenance_failure_still_summarized_and_raw_removed(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);(root/'docs/reports/M2-entities').mkdir(parents=True);error=RuntimeError('initial source provenance failure')
   with patch.object(measure,'ROOT',root),patch('sys.argv',['measure.py','--exclusive-grant','mock-only','--report-prefix','initial','--mode','allocations']),patch.object(measure.shutil,'which',return_value='/usr/bin/true'),patch.object(measure.subprocess,'check_output',side_effect=error):
    with self.assertRaises(RuntimeError) as observed:measure.main()
   self.assertIs(error,observed.exception);saved=json.loads((root/'docs/reports/M2-entities/initial/summary.json').read_text());self.assertEqual('initial source provenance failure',saved['failure']['message']);self.assertTrue(saved['temporaryOutputsRemoved'])
 def test_timeout_terminates_and_reaps_group_without_replacing_error(self):
  class Process:
   pid=123
   def poll(self):return None
   def wait(self,timeout):self.reaped=True;return -15
  with tempfile.TemporaryDirectory() as temp,patch.object(measure.os,'killpg') as kill,patch.object(measure.time,'monotonic',side_effect=[0,2]):
   path=Path(temp)/'log';path.write_text('');process=Process();summary={}
   with self.assertRaises(TimeoutError):measure.bounded_wait(process,path,summary,total_seconds=1)
   kill.assert_called_once_with(123,measure.signal.SIGTERM);self.assertTrue(process.reaped);self.assertTrue(summary['terminatedProcessGroup']);self.assertEqual('total',summary['timeout']['stage'])
if __name__=='__main__':unittest.main()
