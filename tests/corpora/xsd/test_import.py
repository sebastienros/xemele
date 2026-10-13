"""Independent metadata controls, including the status-retention regression."""
import importlib.util, unittest, xml.etree.ElementTree as E
from pathlib import Path
s=importlib.util.spec_from_file_location('xsd_import',Path(__file__).with_name('import.py'));m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
def node(tag,**attrs):return E.Element(m.NS+tag,attrs)
class MetadataRules(unittest.TestCase):
 def test_enclosing_exclusion_survives_absent_lower_version(self):
  self.assertEqual('excluded',m.classify([node('testSet',version='1.1'),node('testGroup'),node('instanceTest')])[0])
 def test_any_tokens_and_all_enclosing_scopes(self):
  self.assertEqual('applicable',m.classify([node('testSet',version='1.0 1.1'),node('testGroup',version='1.0')])[0])
  self.assertEqual('excluded',m.classify([node('testSet',version='1.0 1.1'),node('testGroup',version='1.1')])[0])
 def test_all_expected_tokens_and_exactly_one_match(self):
  t=node('schemaTest',name='probe');E.SubElement(t,m.NS+'expected',version='1.0 1.1',validity='valid');E.SubElement(t,m.NS+'expected',version='1.0',validity='invalid')
  r=m.record(t,[],'probe','schema','s','https://w3c-xsd.test/source/probe.xml');self.assertEqual('invalid',r['expected'])
  E.SubElement(t,m.NS+'expected',validity='valid');self.assertEqual('unresolved',m.record(t,[],'probe','schema','s','https://w3c-xsd.test/source/probe.xml')['classification'])
 def test_schema_and_instance_are_siblings(self):
  group=node('testGroup');schema=node('schemaTest',version='1.1');instance=node('instanceTest',name='probe');E.SubElement(instance,m.NS+'expected',validity='valid');group.extend([schema,instance])
  self.assertEqual('applicable',m.record(instance,[group],'probe','instance','i','https://w3c-xsd.test/source/probe.xml')['classification'])
 def test_current_prior_history_attributes_are_preserved(self):
  t=node('schemaTest',name='probe');E.SubElement(t,m.NS+'expected',validity='valid');E.SubElement(t,m.NS+'current',status='disputed-test',date='2000-01-01',bugzilla='https://example.test/issue');E.SubElement(t,m.NS+'prior',status='queried',date='1999-01-01')
  r=m.record(t,[],'probe','schema','s','https://w3c-xsd.test/source/probe.xml');self.assertEqual('disputed-test',r['currentStatus'][0]['status']);self.assertEqual('https://example.test/issue',r['currentStatus'][0]['bugzilla']);self.assertEqual('queried',r['priorStatus'][0]['status'])
 def test_unknown_tokens_remain_unresolved(self):self.assertEqual('unresolved',m.classify([node('testGroup',version='future-token')])[0])
if __name__=='__main__':unittest.main()
