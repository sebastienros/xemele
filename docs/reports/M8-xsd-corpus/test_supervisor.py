import importlib.util, unittest
from pathlib import Path
s=importlib.util.spec_from_file_location('corpus_runner',Path(__file__).with_name('run.py'));m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
class Supervisor(unittest.TestCase):
 def test_unknown_set_fails(self):
  with self.assertRaises(AssertionError):m.select_sets('unknown',{'testSets':['known']})
 def test_exact_ids_exit_zero(self):self.assertTrue(m.verify([{'id':'a'}],[{'id':'a'}],0))
 def test_same_count_substitution_rejected(self):
  with self.assertRaises(AssertionError):m.verify([{'id':'b'}],[{'id':'a'}],0)
 def test_duplicate_rejected(self):
  with self.assertRaises(AssertionError):m.verify([{'id':'a'},{'id':'a'}],[{'id':'a'}],0)
 def test_exit_seven_after_all_rows_fails(self):self.assertFalse(m.verify([{'id':'a'}],[{'id':'a'}],7))
 def test_timeout_after_all_rows_fails(self):self.assertFalse(m.verify([{'id':'a'}],[{'id':'a'}],'timeout'))
if __name__=='__main__':unittest.main()
