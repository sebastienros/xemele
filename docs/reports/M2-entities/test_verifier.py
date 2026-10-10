import importlib.util,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('entity_verify',Path(__file__).with_name('verify.py'));verify=importlib.util.module_from_spec(spec);spec.loader.exec_module(verify)
class CleanupTests(unittest.TestCase):
 def test_malformed_children_preserve_primary_failure_and_remove_current_outputs(self):
  for payload in ['{','{"Actual":{}}']:
   with self.subTest(payload=payload),tempfile.TemporaryDirectory() as temp:
    root=Path(temp);raw=root/'raw';raw.mkdir();child=root/'children';child.mkdir();pair=child/'new';pair.mkdir();(pair/'real.report.json').write_text(payload);report=root/'report';report.mkdir();log=root/'peer.log';log.write_bytes(b'existing peer output')
    primary=RuntimeError('primary check failure');summary={'result':'failed','failure':{'message':str(primary)}}
    try:
     try:raise primary
     finally:verify.finalize(summary,[],raw,child,set(),log,False,None,report)
    except RuntimeError as observed:self.assertIs(primary,observed)
    saved=json.loads((report/'summary.json').read_text());self.assertEqual('primary check failure',saved['failure']['message']);self.assertTrue(saved['cleanupErrors']);self.assertFalse(raw.exists());self.assertFalse(pair.exists());self.assertEqual(b'existing peer output',log.read_bytes())
 def test_early_main_failure_preserves_existing_inherited_log(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);log=root/'artifacts/M1-contracts/checks.txt';log.parent.mkdir(parents=True);log.write_bytes(b'prior peer log');error=RuntimeError('early git failure')
   with patch.object(verify,'ROOT',root),patch.object(verify,'REPORT',root/'reports'),patch('sys.argv',['verify.py','--report-prefix','early']),patch.object(verify.shutil,'which',return_value='/usr/bin/true'),patch.object(verify.subprocess,'run',side_effect=error):
    (root/'reports').mkdir()
    with self.assertRaises(RuntimeError) as observed:verify.main()
   self.assertIs(error,observed.exception);self.assertEqual(b'prior peer log',log.read_bytes());saved=json.loads((root/'reports/early/summary.json').read_text());self.assertEqual('early git failure',saved['failure']['message']);self.assertTrue(saved['temporaryOutputsRemoved'])
 def test_invoked_inherited_run_restores_prior_log(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);raw=root/'raw';raw.mkdir();report=root/'report';report.mkdir();log=root/'log';log.write_bytes(b'new current output');summary={'result':'passed'}
   self.assertTrue(verify.finalize(summary,[],raw,root/'children',set(),log,True,b'prior peer bytes',report));self.assertEqual(b'prior peer bytes',log.read_bytes())
if __name__=='__main__':unittest.main()
