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
 def test_xml_profile_preserves_original_component_acceptance_as_failure(self):
  case=next(c for c in m.XMLCASES.values() if c['kind']=='schema' and c['originalExpected']=='valid' and c['derivedExpected']=='invalid')
  x=m.view({'id':case['id'],'passed':True,'actual':'schema-valid'})
  self.assertTrue(x['originalPassed']);self.assertFalse(x['profilePassed']);self.assertFalse(x['approvedException'])
 def test_xml_profile_refusal_cannot_satisfy_invalid_oracle(self):
  for case in m.XMLCASES.values():
   if case['disposition']=='assess':
    with self.subTest(id=case['id']):self.assertFalse(m.view({'id':case['id'],'passed':False,'actual':'incomplete-refusal'})['profilePassed'])
 def test_xml_substrate_and_dependencies_never_count_as_profile_passes(self):
  cases=[c for c in m.XMLCASES.values() if c['disposition']!='assess']
  self.assertEqual(len(cases),6)
  for case in cases:
   with self.subTest(id=case['id']):
    x=m.view({'id':case['id'],'passed':True,'actual':'instance-valid'})
    self.assertTrue(x['originalPassed']);self.assertIsNone(x['profilePassed']);self.assertFalse(x['notExecutable'])
    self.assertEqual(x['substrate'],case['disposition']=='outside-XML-1.0-substrate')
    self.assertEqual(x['invalidSchemaDependency'],case['disposition']=='invalid-schema-dependency')
 def test_xml_manifest_is_exact_original_input(self):
  import hashlib,json
  data=json.loads(m.XMLFILE.read_text());files={c['fixture']:c['fixture_sha256'] for c in data['cases']}
  self.assertEqual(len(m.XMLCASES),51);self.assertEqual(len(files),39)
  files['saxonMeta/XmlVersions.testSet']=data['metadata_sha256']
  for name,digest in files.items():
   with self.subTest(file=name):self.assertEqual(hashlib.sha256((m.ROOT/'tests/corpora/xsd/upstream'/name).read_bytes()).hexdigest(),digest)
  manifest=''.join(f'{digest}  {name}\n' for name,digest in sorted(files.items()))
  self.assertEqual(hashlib.sha256(manifest.encode()).hexdigest(),data['manifest_sha256'])
if __name__=='__main__':unittest.main()
