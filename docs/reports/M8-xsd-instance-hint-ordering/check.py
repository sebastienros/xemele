"""Focused bounded correctness/API/consumer checks. No counters or measurements."""
from collections import Counter
from pathlib import Path
import json
import os
import subprocess
import xml.etree.ElementTree as ET

D = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
FLAGS = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
OUT = Path('artifacts/M8-xsd-instance-hint-ordering')
OWN = 'Xemele.Tests.M8XsdInstanceHintOrdering.'
EXPECTED = {
 'OriginalTests.SevenUnchangedOriginalInvalidExpectations': 7,
 'OrderingTests.PhysicalStartTagsDetermineOrdering': 18,
 'OrderingTests.EmptyNamespaceIsARealPhysicalName': 6,
 'OrderingTests.SkippedLexicalAndSemanticAssessmentStaysSkipped': 4,
 'OrderingTests.ChosenSkipPolicyChecksOnlyGlobalOrdering': 4,
 'OrderingTests.NormalLexicalErrorsRemainAssessed': 2,
 'OrderingTests.ContentFailureStillTracksLaterGlobalNamesAndHints': 1,
 'OrderingTests.OrderingDiagnosticsHaveOriginalAttributeLocationsAndHooksStaySingle': 1,
 'BoundaryTests.ExactSharedWorkSucceedsAndOneShortRefuses': 3,
 'BoundaryTests.ExactDiagnosticLimitAndOneShort': 2,
 'BoundaryTests.OrderingScanDoesNotDoubleChargeDocumentCharacters': 1,
 'BoundaryTests.CancellationInsideLongBorrowedTokenWithNullCaps': 3,
 'BoundaryTests.CancellationInsideLongOrdinalComparisonWithNullCaps': 1,
 'BoundaryTests.PrimitiveExactWorkBoundaryAndOneShort': 2,
 'BoundaryTests.BorrowedTableGrowthAndEntryReservationsPrecedeAllocation': 1,
 'BoundaryTests.CancellationAndDocumentMutationStillPropagateAroundNewHook': 1,
 'BoundaryTests.RepeatedAndConcurrentRunsHaveIndependentSetsAndDoNotMutateDocuments': 1,
}
def run(args, **kwargs):
 return subprocess.run(args, check=True, timeout=120, **kwargs)
def assert_tests(path, owned=False):
 root = ET.parse(path).getroot(); assembly = root.find('assembly'); rows = list(root.iter('test'))
 assert rows and len(rows) == int(assembly.attrib['passed']), assembly.attrib
 assert all(int(assembly.attrib[k]) == 0 for k in ['errors', 'failed', 'skipped', 'not-run']), assembly.attrib
 assert '10.0.11' in assembly.attrib['environment']
 assert all(r.attrib['result'] == 'Pass' and 'WarmBorrowed' not in r.attrib['name'] for r in rows)
 assert len({r.attrib['id'] for r in rows}) == len(rows)
 if owned:
  assert all(r.attrib['name'].startswith(OWN) for r in rows)
  assert Counter(r.attrib['name'][len(OWN):].split('(', 1)[0] for r in rows) == Counter(EXPECTED)
  assert len({r.attrib['name'] for r in rows}) == sum(EXPECTED.values())
 return rows
if __name__ == '__main__':
 OUT.mkdir(parents=True, exist_ok=True)
 for project in ['tests/Xemele.Tests/Xemele.Tests.csproj', 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', 'docs/reports/M8-model/ApiCheck/ApiCheck.csproj']:
  run([D, 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *FLAGS])
  run([D, 'build', project, '-c', 'Release', '--no-restore', *FLAGS])
 namespaces = ['M8XsdInstanceHintOrdering', 'M8XsdSchemaSyntax', 'M8XsdAttributeFixed', 'M8Datatypes', 'M8DatatypesTextBinary', 'M8XsdContentFailureAssessment', 'M8XsdInstanceAssessment', 'M8XsdFacade', 'M8XsdPublicationCheckpoints', 'M8XsdFixedRestriction', 'M8SchemaResourceBase', 'M8XsdValidate', 'M8ConstraintLexical', 'M8XsdContent', 'M8XsdCompile', 'M8XsdIdentity', 'M8Facets', 'M8Model', 'M8SourceLocations', 'M8XsdWildcardComposition', 'M8XsdEffectiveContent']
 args = [D, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
 for ns in namespaces: args += ['-namespace', 'Xemele.Tests.' + ns]
 args += ['-method-', '*WarmBorrowed*', '-parallelMode', 'none', '-noLogo', '-noColor', '-result-xml', str(OUT / 'correctness.xml')]
 # On a focused followup --checks-only avoids repeating previously passing tests.
 if '--checks-only' not in __import__('sys').argv: run(args)
 rows = assert_tests(OUT / 'correctness.xml')
 own_rows = [r for r in rows if r.attrib['name'].startswith(OWN)]
 assert Counter(r.attrib['name'][len(OWN):].split('(', 1)[0] for r in own_rows) == Counter(EXPECTED)
 assert len({r.attrib['name'] for r in own_rows}) == sum(EXPECTED.values())
 resolver = next(r['args'] for r in json.loads(Path('docs/reports/M8-resource-kind/test-commands.json').read_text()) if r['label'] == 'resolver')
 resolver = resolver[:resolver.index('-result-xml')] + ['-result-xml', str(OUT / 'resolver.xml')]
 if '--checks-only' not in __import__('sys').argv: run(resolver)
 assert len(assert_tests(OUT / 'resolver.xml')) == 42
 run(['python3', 'docs/reports/M5-validation-model/check_api.py'])
 # All eight frozen declaration lanes, plus emitted implementation baselines.
 baselines = sorted(Path('tests/Contracts/Fixtures/M8').glob('*.golden.txt'))
 assert len(baselines) == 8, baselines
 baselines += [Path(p) for p in ['docs/api/M8-model.txt', 'docs/api/M8-xsd-facade.txt', 'docs/reports/M8-xsd-compile/compiler.golden.txt', 'docs/reports/M8-xsd-identity/identity.golden.txt', 'docs/reports/M8-xsd-content/content.golden.txt', 'docs/reports/M8-xsd-validate/evaluation.golden.txt']]
 for baseline in baselines:
  run([D, '--fx-version', '10.0.11', 'docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll', 'compare', 'src/Xemele/bin/Release/net10.0/Xemele.dll', str(baseline)])
 project = 'samples/M8-xsd-facade/Facade.Consumer.csproj'
 for xmlonly in [False, True]:
  flags = [*FLAGS, *(['-p:FacadeXmlOnly=true'] if xmlonly else [])]
  run([D, 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *flags])
  run([D, 'build', project, '-c', 'Release', '--no-restore', *flags])
  run([D, '--fx-version', '10.0.11', 'samples/M8-xsd-facade/bin/Release/net10.0/Xemele.Benchmarks.dll'], env=dict(os.environ, FACADE_XML_ONLY='1' if xmlonly else '0'))
 run(['python3', 'tools/planning/tasks.py', 'validate']); run(['git', 'diff', '--check'])
 print(f'PASS: {len(rows)} affected including {len(own_rows)} owned; resolver42; public/all8 frozen/implementation APIs; normal+true XML-only consumers; planning/diff.')
