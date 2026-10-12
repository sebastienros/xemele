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
 def test_explicit_profile_preserves_original_failures_and_depends_on_actual_rejection(self):
  self.assertEqual(len(m.EXPLICITCASES),4)
  for case in m.EXPLICITCASES.values():
   with self.subTest(id=case['id']):
    x=m.view({'id':case['id'],'passed':False,'actual':'schema-invalid'});self.assertFalse(x['originalPassed']);self.assertTrue(x['profilePassed']);self.assertFalse(x['approvedException'])
    self.assertFalse(m.view({'id':case['id'],'passed':False,'actual':'incomplete-refusal'})['profilePassed'])
    self.assertFalse(m.view({'id':case['id'],'passed':True,'actual':'schema-valid'})['profilePassed'])
 def test_explicit_profile_sources_are_exact_and_dependencies_unassessed(self):
  import hashlib
  self.assertEqual(len(m.EXPLICITDEPS),4)
  for case in list(m.EXPLICITCASES.values())+list(m.EXPLICITDEPS.values()):
   for name,digest in case['documents'].items():
    with self.subTest(file=name):self.assertEqual(hashlib.sha256((m.ROOT/'tests/corpora/xsd/upstream'/name).read_bytes()).hexdigest(),digest)
  for case in m.EXPLICITDEPS.values():
   x=m.view({'id':case['id'],'passed':False,'actual':'schema-unavailable'});self.assertIsNone(x['profilePassed']);self.assertTrue(x['notExecutable']);self.assertTrue(x['invalidSchemaDependency'])
 def test_supplementary_and_lexical_profiles_preserve_original_mismatches(self):
  self.assertEqual(len(m.INSTANCEPROFILES),22)
  for case in m.INSTANCEPROFILES.values():
   with self.subTest(id=case['id']):
    x=m.view({'id':case['id'],'passed':False,'actual':'instance-'+case['profile_expected']});self.assertFalse(x['originalPassed']);self.assertTrue(x['profilePassed']);self.assertFalse(x['approvedException'])
    for refused in ['incomplete-refusal','schema-unavailable','timeout','instance-syntax-error']:
     self.assertFalse(m.view({'id':case['id'],'passed':False,'actual':refused})['profilePassed'])
 def test_substitution_agreement_never_changes_original_failure(self):
  x=m.view({'id':m.SUBSTITUTION['id'],'passed':False,'actual':'schema-valid'});self.assertFalse(x['originalPassed']);self.assertTrue(x['profilePassed']);self.assertFalse(x['approvedException'])
  self.assertFalse(m.view({'id':m.SUBSTITUTION['id'],'passed':False,'actual':'incomplete-refusal'})['profilePassed'])
 def test_xml_standalone_never_credits_explicit_supply_result(self):
  self.assertEqual(len(m.XMLSUPPLYORIGINALS),4)
  for id in m.XMLSUPPLYORIGINALS:
   x=m.view({'id':id,'passed':False,'actual':'schema-invalid' if '/s:' in id else 'schema-unavailable'});self.assertFalse(x['originalPassed']);self.assertIsNone(x['profilePassed']);self.assertTrue(x['applicationInputUnassessed'])
 def test_late_hints_and_namespace_guard_remain_adapter_unassessed(self):
  self.assertEqual(len(m.LATEHINTS),7)
  for id in m.LATEHINTS|{m.ADAPTER['namespace_guard']['id']}:
   x=m.view({'id':id,'passed':False,'actual':'instance-hint-ordering-limitation'});self.assertFalse(x['originalPassed']);self.assertIsNone(x['profilePassed']);self.assertTrue(x['adapterUnassessed']);self.assertFalse(x['approvedException'])
 def test_fatal_syntax_never_becomes_complete_invalid_profile_pass(self):
  self.assertEqual(len(m.FATALXML),3)
  for id in m.FATALXML:
   self.assertIsNone(m.view({'id':id,'passed':False,'actual':'incomplete-refusal'})['profilePassed'])
   self.assertFalse(m.view({'id':id,'passed':True,'actual':'instance-invalid'})['profilePassed'])
 def test_verified_product_hint_profile_requires_complete_ordering_invalidity(self):
  for id in m.LATEHINTS:
   row={'id':id,'engine':'product','hintOrderingProfile':True,'passed':True,'actual':'instance-invalid','codes':['XSD.HintOrder']}
   x=m.view(row);self.assertTrue(x['originalPassed']);self.assertTrue(x['profilePassed']);self.assertFalse(x['adapterUnassessed']);self.assertFalse(x['approvedException'])
   for outcome in ['incomplete-refusal','schema-unavailable','instance-valid','instance-hint-ordering-limitation']:
    self.assertFalse(m.view({**row,'actual':outcome,'passed':False})['profilePassed'])
   self.assertFalse(m.view({**row,'codes':['XSD.Content']})['profilePassed'])
 def test_verified_hint_profile_does_not_promote_bcl_or_historical_rows(self):
  for id in m.LATEHINTS:
   row={'id':id,'engine':'bcl','hintOrderingProfile':True,'passed':False,'actual':'instance-hint-ordering-limitation','codes':[]}
   self.assertIsNone(m.view(row)['profilePassed']);self.assertTrue(m.view(row)['adapterUnassessed'])
   self.assertIsNone(m.view({**row,'engine':'product','hintOrderingProfile':False})['profilePassed'])
 def test_second_edition_namechar_requires_actual_complete_invalidity(self):
  x=m.view({'id':m.NAMECHAR['id'],'passed':False,'actual':'instance-invalid'});self.assertFalse(x['originalPassed']);self.assertTrue(x['profilePassed'])
  self.assertFalse(m.view({'id':m.NAMECHAR['id'],'passed':False,'actual':'incomplete-refusal'})['profilePassed'])
if __name__=='__main__':unittest.main()
