"""Independent case counts fixed from authored acceptance coverage, not observed output."""
from collections import Counter
from pathlib import Path
import importlib.util
spec = importlib.util.spec_from_file_location("syntax_assert", Path(__file__).resolve().parents[1] / "M8-xsd-schema-syntax/assert_results.py")
syntax = importlib.util.module_from_spec(spec); spec.loader.exec_module(syntax)
OWNED = "Xemele.Tests.M8XsdComponentMapping."
EXPECTED = {
 "OriginalWitnessTests.OriginalGroupN007AcceptsMixedCharacters": 1,
 "OriginalWitnessTests.OriginalGroupN008RejectsForbiddenChildren": 1,
 "OriginalWitnessTests.OriginalGroupH007RejectsMixedRestrictionOfElementOnlyBase": 1,
 "WildcardWitnessTests.OriginalEmptyNamespaceSchemasPublish": 3,
 "WildcardNeighborTests.ExplicitEmptyListsMapToPositiveEmptySet": 6,
 "WildcardNeighborTests.EmptyListIsDistinctFromExistingNamespacePolicies": 10,
 "WildcardNeighborTests.RequiredEmptySetParticleRemainsUnsatisfiable": 2,
 "WildcardNeighborTests.EmptyMappingDoesNotRelaxInvalidNamespaceTokens": 6,
 "WildcardNeighborTests.EmptyMappingDoesNotRelaxProcessContents": 2,
 "WildcardNeighborTests.WhitespaceMappingChargesWorkAtSourceAndPollsCancellation": 2,
 "OriginalWitnessTests.OriginalSaxonComponentCompilerRejectsNames": 2,
 "OriginalWitnessTests.OriginalSaxonSchemasRejectUnderApprovedSecondEditionProfile": 2,
 "NeighborTests.ValidatedZeroOccurrenceMapsAndValidatesAccordingToMixed": 6,
 "NeighborTests.ZeroOccurrenceUsesInnerEffectiveMixedForRestriction": 6,
 "NeighborTests.EmptyExtensionInheritsWholeBaseAndMixedExtensionRetainsBase": 6,
 "NeighborTests.MixedZeroMappingDoesNotHideInvalidAuthoredParticles": 6,
 "NeighborTests.SynthesizedZeroParticlePreservesScalarAndByteLocations": 6,
 "NeighborTests.ZeroSynthesisHasExactWorkComponentsAndCancellation": 3,
 "NeighborTests.LegalSecondEditionNamesPublish": 6,
 "NeighborTests.AllComponentNameKindsUseSecondEdition": 7,
 "NeighborTests.SchemaIdsUseSecondEdition": 3,
 "NeighborTests.QNamePrefixAndLocalUseSecondEdition": 7,
 "NeighborTests.LegalMultilingualDeclarationIdAndQNameComponentsPublish": 1,
 "NeighborTests.ParserAndTreeKeepFifthEditionNameGrammar": 5,
 "NeighborTests.LongNamesScanAndRejectAtTheirOriginalLocation": 2,
 "NeighborTests.BorrowedScanMatchesIndependentBoundaryOracle": 10,
 "NeighborTests.NameScanChargesExactSharedBudgetAndCancelsInsideLongName": 1,
}
def assert_tests(path, count=None, correctness=False):
 import xml.etree.ElementTree as ET
 total=syntax.assert_tests(path,count=count,correctness=correctness)
 if correctness:
  names=[t.attrib["name"] for t in ET.parse(path).getroot().iter("test") if t.attrib["name"].startswith(OWNED)]
  actual=Counter(n[len(OWNED):].split("(",1)[0] for n in names)
  assert actual == Counter(EXPECTED) and sum(actual.values()) == 113, actual
  assert len(set(names)) == 113, "Duplicate owned case identity"
  assert total == 1113 + 33 + 113, ("Verified syntax33 + affected1113 plus owned113 expected",total)
 return total
