import importlib.util,unittest
from pathlib import Path
s=importlib.util.spec_from_file_location('dispositions',Path(__file__).with_name('dispositions.py'));m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
class Dispositions(unittest.TestCase):
 def test_exact_exception_does_not_become_pass(self):
  x=m.view({'id':m.TIME,'passed':False,'actual':'schema-valid'});self.assertFalse(x['originalPassed']);self.assertFalse(x['profilePassed']);self.assertTrue(x['approvedException'])
 def test_exception_does_not_waive_refusal(self):self.assertFalse(m.view({'id':m.TIME,'passed':False,'actual':'incomplete-refusal'})['approvedException'])
 def test_err_schema_both_oracles_survive(self):
  x=m.view({'id':m.ERR,'passed':False,'actual':'schema-invalid'});self.assertFalse(x['originalPassed']);self.assertTrue(x['profilePassed']);self.assertFalse(x['approvedException'])
 def test_dependent_instance_is_unexecuted_never_pass(self):
  x=m.view({'id':m.DEP,'passed':False,'actual':'schema-unavailable'});self.assertFalse(x['originalPassed']);self.assertIsNone(x['profilePassed']);self.assertTrue(x['notExecutable'])
 def test_queried_case_not_blanket_waived(self):
  x=m.view({'id':'different-queried-case','passed':False,'actual':'schema-invalid'});self.assertFalse(x['profilePassed']);self.assertFalse(x['approvedException'])
if __name__=='__main__':unittest.main()
