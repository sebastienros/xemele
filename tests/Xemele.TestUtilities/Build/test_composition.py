import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'tools/ApiBaseline'))
from compare import compose
from discover import DiscoveryError


class CompositionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'docs/api').mkdir(parents=True)

    def fragment(self, name, identity, member, declaration='public class Example'):
        path = self.root / 'docs/api' / (name + '.txt')
        path.write_text('xemele-api-v1\n' + json.dumps(dict(id=identity, member=member,
                        declaration=declaration), sort_keys=True, separators=(',', ':')) + '\n')
        return path

    def test_member_order_and_distinct_id_provenance_without_rewrites(self):
        a = self.fragment('a', 'original-a', 'T:Z')
        z = self.fragment('z', 'original-z', 'T:A')
        before = (a.read_bytes(), z.read_bytes())
        canonical, report = compose(self.root)
        entries = [json.loads(row) for row in canonical.splitlines()[1:]]
        self.assertEqual([row['id'] for row in entries], ['T:A', 'T:Z'])
        self.assertEqual([row['id'] for row in report['provenance']], ['original-z', 'original-a'])
        self.assertEqual(report['provenance'][0]['source'], 'docs/api/z.txt')
        self.assertEqual(report['provenance'][0]['line'], 2)
        self.assertEqual(before, (a.read_bytes(), z.read_bytes()))

    def test_duplicates_and_conflicts_under_distinct_ids(self):
        self.fragment('a', 'first', 'T:A')
        for declaration, reason in [('public class Example', 'duplicate'), ('changed', 'conflicting')]:
            self.fragment('z', 'second', 'T:A', declaration)
            with self.assertRaisesRegex(DiscoveryError, reason + ' API member'):
                compose(self.root)

    def test_join_fragment_keeps_provenance_and_nonempty_comparison(self):
        self.fragment('member', 'original', 'T:A')
        join = self.root / 'docs/api/join.txt'
        join.write_text('xemele-api-v1\n')
        canonical, report = compose(self.root)
        self.assertEqual(len(canonical.splitlines()), 2)
        self.assertEqual(report['inventory']['apiDeclarations'], 1)
        self.assertEqual(report['provenance'][0]['source'], 'docs/api/member.txt')
        self.assertIn('docs/api/join.txt', report['inventory']['apiFragments'])

    def test_empty_requires_explicit_inventory_bootstrap(self):
        project = self.root / 'src/Xemele/Xemele.csproj'
        project.parent.mkdir(parents=True)
        project.write_text('<Project />')
        with self.assertRaisesRegex(DiscoveryError, 'missing API baselines'):
            compose(self.root)
        canonical, report = compose(self.root, True)
        self.assertEqual(canonical, 'xemele-api-v1\n')
        self.assertTrue(report['inventory']['bootstrapEmptyApi'])
        (project.parent / 'Runtime.cs').write_text('internal class Runtime {}')
        with self.assertRaisesRegex(DiscoveryError, 'empty runtime'):
            compose(self.root, True)
