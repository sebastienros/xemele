"""Reuse the verified facade check, then require this repair's nonempty owned inventory."""
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

D = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
subprocess.run(['python3', 'docs/reports/M8-xsd-facade/check.py'], check=True)
out = Path('artifacts/M8-xsd-attribute-fixed'); out.mkdir(parents=True, exist_ok=True)
subprocess.run([D, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll',
                '-namespace', 'Xemele.Tests.M8XsdAttributeFixed', '-parallelMode', 'none', '-noLogo', '-noColor',
                '-result-xml', str(out / 'owned.xml')], check=True)
tree = ET.parse(out / 'owned.xml').getroot(); assembly = tree.find('assembly')
rows = list(tree.iter('test'))
assert len(rows) == 45 and int(assembly.attrib['passed']) == 45, assembly.attrib
assert all(int(assembly.attrib[k]) == 0 for k in ['errors', 'failed', 'skipped', 'not-run']), assembly.attrib
assert '10.0.11' in assembly.attrib['environment'] and all(t.attrib['result'] == 'Pass' for t in rows)
required = ['OriginalWitnessTests', 'RestrictionUsesActualValuesWithIndependentOracle', 'QNameContextBelongsToEachLocalUse',
            'QNameListsRetainAuthoredContexts', 'BothDerivationKindsCheckInheritedFixed', 'FixedDefaultAndAbsentRemainDistinct',
            'ReferencedDeclarationContextSurvivesDifferentlyBoundUse', 'InvalidLocalOverrideCannotBorrowDeclarationContext',
            'FacetsReceiveAuthoredLexicalAndSharedOperation', 'FrozenStructuralLanePreservesLexicalComparison',
            'ExactSharedBudgetAndCancellationBoundaries', 'SemanticMismatchAndOperationRefusalHaveDifferentDiagnostics']
for name in required: assert any(name in t.attrib['name'] for t in rows), name
subprocess.run([D, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll',
                '-namespace', 'Xemele.Tests.M8Datatypes', '-method-', '*WarmBorrowed*', '-parallelMode', 'none', '-noLogo', '-noColor',
                '-result-xml', str(out / 'datatypes.xml')], check=True)
dt = ET.parse(out / 'datatypes.xml').getroot(); da = dt.find('assembly'); dr = list(dt.iter('test'))
assert len(dr) > 45 and int(da.attrib['passed']) == len(dr), da.attrib
assert all(int(da.attrib[k]) == 0 for k in ['errors', 'failed', 'skipped', 'not-run']) and all(t.attrib['result'] == 'Pass' for t in dr)
assert not any('WarmBorrowed' in t.attrib['name'] for t in dr)
for baseline in ['tests/Contracts/Fixtures/M8/Model.golden.txt', 'docs/api/M8-model.txt']:
    subprocess.run([D, '--fx-version', '10.0.11', 'docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll',
                    'compare', 'src/Xemele/bin/Release/net10.0/Xemele.dll', baseline], check=True)
affected = ET.parse('artifacts/M8-xsd-facade/correctness.xml').getroot().find('assembly')
assert int(affected.attrib['passed']) == 1110, affected.attrib
subprocess.run(['python3', 'tools/planning/tasks.py', 'validate'], check=True)
subprocess.run(['git', 'diff', '--check'], check=True)
print('Owned45, affected1110, resolver42, public1417, model316+139, compiler257/identity24/content9/evaluation2/facade7, normal/XML-only consumers, planning passed.')
