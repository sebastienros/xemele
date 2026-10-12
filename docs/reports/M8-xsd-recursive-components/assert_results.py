"""Counter-free result checks; independent authored inventory, including missing-row rejection."""
import sys
sys.dont_write_bytecode = True
from collections import Counter
from pathlib import Path
import importlib.util
import xml.etree.ElementTree as ET
spec = importlib.util.spec_from_file_location('mapping', Path(__file__).resolve().parents[1] / 'M8-xsd-component-mapping/assert_results.py')
mapping = importlib.util.module_from_spec(spec); spec.loader.exec_module(mapping)
OWNED = 'Xemele.Tests.M8XsdRecursiveComponents.'
EXPECTED = {
    'OriginalTests.UnchangedOriginalSchemaPublishes': 3,
    'OriginalTests.UnchangedOriginalInstanceValidates': 1,
    'NeighborTests.RecursiveGroupAcrossElementHasOrderIndependentFiniteValidation': 2,
    'NeighborTests.MutualGroupsAcrossElementsTerminateAtOptionalContent': 2,
    'NeighborTests.InlineDerivationMayUseEnclosingCompletedType': 4,
    'NeighborTests.MutualGlobalInlineElementsUseFiniteDeclarationIds': 2,
    'NeighborTests.CompletionDrainsNewlyRegisteredNestedInlineTypes': 1,
    'NeighborTests.ForbiddenCyclesStillReject': 8,
    'NeighborTests.UnusedDeferredBodiesMustPassPublicationChecks': 4,
    'NeighborTests.CompiledSnapshotDoesNotRetainMutableSource': 1,
    'BoundaryTests.CompletionHasExactAggregateWorkAndComponents': 2,
    'BoundaryTests.CancellationAndWorkRefusalOccurInsideDeferredBodyWithNullCaps': 1,
    'BoundaryTests.DeferredFailurePreservesCharacterOrByteProvenance': 4,
    'BoundaryTests.DeferredImportedFailureUsesFinalResourceUriAndDisposesOnce': 1,
    'BoundaryTests.LateSourceMutationRefusesPublication': 1,
}
def assert_tests(path, count=None, correctness=False):
    total = mapping.assert_tests(path, count=count)
    if correctness:
        names = [t.attrib['name'] for t in ET.parse(path).getroot().iter('test')]
        owned = [n for n in names if n.startswith(OWNED)]
        assert Counter(n[len(OWNED):].split('(', 1)[0] for n in owned) == Counter(EXPECTED)
        assert len(set(owned)) == sum(EXPECTED.values()) == 37
        # Remove only the independently inventoried new rows before asking the
        # established checker to validate its frozen 1263-case predecessor set.
        root = ET.parse(path).getroot()
        for collection in root.iter('collection'):
            for test in list(collection):
                if test.tag == 'test' and test.attrib['name'].startswith(OWNED): collection.remove(test)
        assembly = root.find('assembly')
        assembly.attrib['passed'] = assembly.attrib['total'] = str(total - 37)
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            predecessor = Path(tmp) / 'predecessor.xml'; ET.ElementTree(root).write(predecessor)
            mapping.assert_tests(predecessor, correctness=True)
        assert total == 1300
    return total
