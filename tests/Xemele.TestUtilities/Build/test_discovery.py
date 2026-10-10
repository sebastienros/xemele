import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

TOOL = Path(__file__).resolve().parents[3] / 'tools/ApiBaseline/discover.py'
spec = importlib.util.spec_from_file_location('discovery', TOOL)
discovery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(discovery)


class DiscoveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.write('src/Xemele/Xemele.csproj', '<Project />')
        self.baseline([{'id': 'a', 'member': 'T:Example.One', 'declaration': 'public class One'}])

    def write(self, name, content):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding='utf-8')
        return path

    def baseline(self, entries, name='one'):
        return self.write(f'docs/api/{name}.txt', 'xemele-api-v1\n' + ''.join(json.dumps(e, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n' for e in entries))

    def corpus(self, **changes):
        self.write('tests/corpora/one/input.xml', '<one/>')
        digest = hashlib.sha256(b'<one/>').hexdigest()
        manifest = dict(schemaVersion=1, id='one', sourceUrl='https://example.org/one', revision='v1.0', sha256=digest, license='MIT', notice='Example notice', adapterVersion='1', profile='xml-1.0', fixtures=[dict(path='input.xml', sha256=digest, classification='accepted')])
        manifest.update(changes)
        return self.write('tests/corpora/one/manifest.json', json.dumps(manifest))

    def rejects(self, message, bootstrap=False):
        with self.assertRaisesRegex(discovery.DiscoveryError, message):
            discovery.discover(self.root, bootstrap)

    def test_valid_fragments_and_fixture(self):
        self.corpus()
        result = discovery.discover(self.root)
        self.assertEqual(result['apiFragments'], ['docs/api/one.txt'])
        self.assertEqual(result['corpusManifests'], ['tests/corpora/one/manifest.json'])
        self.assertEqual(result['fixtures'], 1)

    def test_missing_baseline(self):
        (self.root / 'docs/api/one.txt').unlink()
        self.rejects('missing API baselines')
        self.assertTrue(discovery.discover(self.root, True)['bootstrapEmptyApi'])
        self.write('src/Xemele/One.cs', 'class One {}')
        self.rejects('empty runtime', True)

    def test_generated_sources_do_not_break_empty_bootstrap(self):
        (self.root / 'docs/api/one.txt').unlink()
        self.write('src/Xemele/obj/Generated.cs', '// generated')
        self.assertTrue(discovery.discover(self.root, True)['bootstrapEmptyApi'])

    def test_unknown_and_empty_formats(self):
        self.write('docs/api/one.txt', 'future-format\n')
        self.rejects('unsupported baseline format')
        self.write('docs/api/one.txt', 'xemele-api-v1\n')
        self.rejects('empty API fragment')

    def test_duplicate_and_conflicting_ids(self):
        for declaration, reason in [('public class One', 'duplicate'), ('internal class One', 'conflicting')]:
            self.baseline([dict(id='a', member='T:Example.Other', declaration=declaration)], 'two')
            self.rejects(reason + ' API id')

    def test_distinct_ids_cannot_conflict_on_member(self):
        for declaration, reason in [('public class One', 'duplicate'), ('internal class One', 'conflicting')]:
            self.baseline([dict(id='b', member='T:Example.One', declaration=declaration)], 'two')
            self.rejects(reason + ' API member')

    def test_noncanonical_unknown_fields_and_unsorted(self):
        self.write('docs/api/one.txt', 'xemele-api-v1\n{"id":"a","member":"T:A","declaration":"A"}\n')
        self.rejects('noncanonical')
        self.baseline([dict(id='a', member='T:A', declaration='A', unknown='x')])
        self.rejects('fields only')
        self.baseline([dict(id='b', member='T:B', declaration='B'), dict(id='a', member='T:A', declaration='A')])
        self.rejects('must be sorted')

    def test_duplicate_json_fields(self):
        self.write('docs/api/one.txt', 'xemele-api-v1\n{"id":"a","id":"b"}\n')
        self.rejects('duplicate JSON field')

    def test_missing_fixture_and_checksum(self):
        self.corpus()
        (self.root / 'tests/corpora/one/input.xml').unlink()
        self.rejects('missing fixture')
        self.write('tests/corpora/one/input.xml', 'changed')
        self.rejects('checksum mismatch')

    def test_fixture_traversal(self):
        self.corpus(fixtures=[dict(path='../../../../outside.xml', sha256='0'*64, classification='accepted')])
        self.rejects('escapes repository')

    def test_metadata_schema_and_classification(self):
        for changes, message in [({'schemaVersion': 2}, 'unsupported corpus'), ({'license': ''}, 'metadata license'), ({'revision': 'main'}, 'immutable'), ({'sha256': 'bad'}, 'invalid corpus'), ({'fixtures': [dict(path='input.xml', classification='unknown')]}, 'classification')]:
            self.corpus(**changes)
            self.rejects(message)

    def test_duplicate_corpus_and_fixture(self):
        path = self.corpus()
        self.write('tests/corpora/two/manifest.json', path.read_text())
        self.rejects('duplicate corpus id')
        (self.root / 'tests/corpora/two/manifest.json').unlink()
        entry = dict(path='input.xml', sha256=hashlib.sha256(b'<one/>').hexdigest(), classification='accepted')
        self.corpus(fixtures=[entry, entry])
        self.rejects('duplicate fixture path')

    def test_cli_failure_exit_and_explicit_error(self):
        (self.root / 'docs/api/one.txt').unlink()
        result = subprocess.run([sys.executable, '-B', str(TOOL), '--root', str(self.root)], capture_output=True, text=True)
        self.assertEqual(result.returncode, 1)
        self.assertIn('discovery error: missing API baselines', result.stderr)
        self.assertEqual(result.stdout, '')


if __name__ == '__main__':
    unittest.main()
