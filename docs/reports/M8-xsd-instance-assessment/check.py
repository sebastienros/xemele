"""Bounded correctness checks; no allocation counters, measurements or broad discovery."""
from collections import Counter
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

DOTNET = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
OWNED = 'Xemele.Tests.M8XsdInstanceAssessment.'
EXPECTED = {
 'OriginalWitnessTests.UnchangedOriginalHasIndependentBclAndPublicOutcome': 4,
 'NeighborTests.ProhibitionRequiresMatchingWildcardAndStrictDeclaration': 5,
 'NeighborTests.WildcardUsesGlobalDatatypeAndTypedFixed': 8,
 'NeighborTests.PermittedLocalUsePrecedesMatchingGlobal': 4,
 'NeighborTests.ProhibitedReferenceDoesNotInjectGlobalDefault': 1,
 'NeighborTests.NilledUniqueFieldStillRequiresSimpleEffectiveType': 7,
 'NeighborTests.NilledFieldUsesEffectiveXsiType': 3,
 'NeighborTests.TwoNilledStringFieldsDoNotQualifyDuplicateTuples': 1,
 'NeighborTests.ExistingKeyNillableAndNilLexicalRulesRemain': 5,
 'NeighborTests.SecondEditionKeyRejectsNillableDeclarationEvenWithoutNil': 1,
 'NeighborTests.NilAssessmentSharesExactPrechargedWorkAndMemory': 4,
 'NeighborTests.ComplexNilTypeLookupPollsInsideChargedGraphTraversal': 1,
}
def assert_result(path, count):
 tree = ET.parse(path).getroot(); assembly = tree.find('assembly'); rows = list(tree.iter('test'))
 assert len(rows) == int(assembly.attrib['passed']) == count, assembly.attrib
 assert all(int(assembly.attrib[k]) == 0 for k in ['errors', 'failed', 'skipped', 'not-run']), assembly.attrib
 assert '10.0.11' in assembly.attrib['environment'] and all(r.attrib['result'] == 'Pass' for r in rows)
 assert not any('WarmBorrowed' in r.attrib['name'] for r in rows)
 return rows

def assert_owned(path):
 rows = assert_result(path, 44); names = [r.attrib['name'] for r in rows]
 assert all(n.startswith(OWNED) for n in names)
 actual = Counter(n[len(OWNED):].split('(', 1)[0] for n in names)
 assert actual == Counter(EXPECTED), actual
 assert len(set(names)) == 44, 'Duplicate owned case identity'
 return len(rows)

if __name__ == '__main__':
 subprocess.run(['python3', 'docs/reports/M8-xsd-schema-syntax/check.py'], check=True)
 # This integrated predecessor verifies syntax33+affected1113, safe42,
 # public1417 and all frozen API fragments, normal+true XML-only consumers.
 assert_result('artifacts/M8-xsd-schema-syntax/correctness.xml', 1146)
 out = Path('artifacts/M8-xsd-instance-assessment'); out.mkdir(parents=True, exist_ok=True)
 for namespace, label in [('M8XsdInstanceAssessment', 'owned'), ('M8XsdAttributeFixed', 'fixed-affected')]:
  path = out / (label + '.xml')
  subprocess.run([DOTNET, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll',
    '-namespace', 'Xemele.Tests.' + namespace, '-parallelMode', 'none', '-noLogo', '-noColor', '-result-xml', str(path)], check=True)
  if label == 'owned': assert_owned(path)
  else: assert_result(path, 45)
 subprocess.run(['python3', 'tools/planning/tasks.py', 'validate'], check=True)
 subprocess.run(['git', 'diff', '--check'], check=True)
 print('PASS: 44 owned + 1146 syntax/affected + 45 fixed regression; safe42, public1417/frozen APIs, normal/true XML-only, planning/diff.')
