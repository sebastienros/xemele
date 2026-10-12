"""Independently bounded owned test inventory; pure output validation."""
from collections import Counter
import xml.etree.ElementTree as ET
OWNED = "Xemele.Tests.M8XsdSchemaSyntax."
EXPECTED = {
 "OriginalWitnessTests.OriginalOfficialInvalidSchemaRejectsThroughPublicFacade": 3,
 "NeighborTests.DocumentationLanguageUsesCollapsedXsLanguage": 4,
 "NeighborTests.InvalidDocumentationLanguageIsSyntaxRejection": 10,
 "NeighborTests.AbsentLanguageAndOpaqueAppinfoAreLegal": 1,
 "NeighborTests.GlobalGroupRejectsLocalAttributes": 4,
 "NeighborTests.GlobalDefinitionAndLocalOccurrencesRemainLegal": 1,
 "NeighborTests.LegalRestrictedPathsPublishThroughFacade": 3,
 "NeighborTests.ExplicitSelfAxesRejectThroughFacade": 5,
 "NeighborTests.LanguageWhitespaceScanHasExactWorkAndCancellation": 2,
}
AFFECTED = ["M8XsdFacade", "M8XsdPublicationCheckpoints", "M8XsdFixedRestriction", "M8SchemaResourceBase", "M8XsdValidate", "M8ConstraintLexical", "M8XsdContent", "M8XsdCompile", "M8XsdIdentity", "M8Facets", "M8Model", "M8SourceLocations", "M8XsdWildcardComposition", "M8XsdEffectiveContent"]
def assert_tests(path, count=None, correctness=False):
 r = ET.parse(path).getroot(); a = r.find("assembly"); tests = list(r.iter("test"))
 assert all(int(a.attrib[k]) == 0 for k in ["errors", "failed", "skipped", "not-run"]), a.attrib
 assert "10.0.11" in a.attrib["environment"]
 assert len(tests) == int(a.attrib["passed"]) and all(t.attrib["result"] == "Pass" for t in tests)
 if count is not None: assert len(tests) == count
 assert all("WarmBorrowed" not in t.attrib["name"] for t in tests)
 if correctness:
  names = [t.attrib["name"] for t in tests]
  actual = Counter(n[len(OWNED):].split("(", 1)[0] for n in names if n.startswith(OWNED))
  assert actual == Counter(EXPECTED) and sum(actual.values()) == 33, actual
  assert len({n for n in names if n.startswith(OWNED)}) == 33, "Duplicate owned case identity"
  for ns in AFFECTED: assert any(n.startswith("Xemele.Tests." + ns + ".") for n in names), ns
 return len(tests)
if __name__ == "__main__":
 import sys
 print(assert_tests(sys.argv[1], correctness=True))
