"""Exact nonempty owned inventory; never accepts skipped/failed or allocation cases."""
from collections import Counter
import xml.etree.ElementTree as ET
OWNED='Xemele.Tests.M8XsdCountedMatcher.'
EXPECTED={'LanguageTests.IndependentFiniteLanguageEnumeration':6,'LanguageTests.NestedMinimumAndMaximumBoundaries':5,'LanguageTests.PartitionGrowthAndExactWorkRefusal':1,'LanguageTests.DominanceCancellationFaultsCursorAndPreservesCallerException':1}
def assert_owned(path):
 root=ET.parse(path).getroot();assembly=root.find('assembly');tests=list(root.iter('test'))
 assert '10.0.11' in assembly.attrib['environment']
 assert all(int(assembly.attrib[k])==0 for k in ['errors','failed','skipped','not-run'])
 assert all(t.attrib['result']=='Pass' and 'WarmBorrowed' not in t.attrib['name'] for t in tests)
 names=[t.attrib['name'] for t in tests if t.attrib['name'].startswith(OWNED)]
 assert Counter(n[len(OWNED):].split('(',1)[0] for n in names)==Counter(EXPECTED)
 assert len(names)==len(set(names))==13
 return len(tests)
if __name__=='__main__':
 import sys
 print(assert_owned(sys.argv[1]))
