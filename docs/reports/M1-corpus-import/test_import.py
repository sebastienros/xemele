import copy
import json
from pathlib import Path
import unittest
import import_suite as suite

class ImportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = (suite.CORPUS / 'xmlts20130923.tar.gz').read_bytes()
        cls.files, cls.members = suite.archive_files(cls.data)
        cls.manifest, cls.inventory, cls.bundle = suite.generate(cls.data)

    def test_exact_authoritative_counts_and_identities(self):
        i = self.inventory
        self.assertEqual(2585, len(i['cases']))
        self.assertEqual(2585, len({c['id'] for c in i['cases']}))
        self.assertEqual({'valid':812, 'not-wf':1498, 'invalid':242, 'error':33}, i['summary']['sourceTypes'])
        self.assertEqual(323, i['summary']['externalEntityCases'])
        self.assertEqual(3697, len(self.members))
        self.assertEqual(3386, len(self.files))

    def test_every_archive_file_is_in_bundle_and_manifest(self):
        import base64
        bundle = json.loads(self.bundle)
        entries = {f['path']: base64.b64decode(f['base64'], validate=True) for f in bundle['files']}
        self.assertEqual(self.files, entries)
        self.assertEqual(set(entries), {f['path'] for f in self.manifest['fixtures']})
        for fixture in self.manifest['fixtures']:
            self.assertEqual(suite.sha(entries[fixture['path']]), fixture['sha256'])

    def test_primary_output_metadata_containment(self):
        for c in self.inventory['cases']:
            self.assertIn(c['inputPath'], self.files)
            self.assertIn(c['metadataPath'], self.files)
            for p in c['outputPaths'].values(): self.assertIn(p, self.files)
            self.assertFalse(c['missingOutputs'])

    def test_corrupt_archive_fails(self):
        with self.assertRaisesRegex(ValueError, 'SHA-256'): suite.archive_files(self.data + b'changed')

    def test_missing_metadata_entity_fails(self):
        files = dict(self.files)
        del files['xmlconf/testcases.dtd']
        with self.assertRaisesRegex(ValueError, 'Missing metadata resource'): suite.discover(files)

    def test_missing_primary_fixture_fails(self):
        files = dict(self.files)
        del files['xmlconf/xmltest/not-wf/sa/001.xml']
        with self.assertRaisesRegex(ValueError, 'Missing primary'): suite.discover(files)

    def test_bad_metadata_type_fails(self):
        files = dict(self.files)
        key = 'xmlconf/xmltest/xmltest.xml'
        files[key] = files[key].replace(b'TYPE="not-wf"', b'TYPE="mystery"', 1)
        with self.assertRaisesRegex(ValueError, 'Unknown source TYPE'): suite.discover(files)

    def test_escaped_metadata_uri_fails(self):
        with self.assertRaises(ValueError): suite.contained('https://outside.example/test.xml')
        with self.assertRaises(ValueError): suite.contained(suite.HOST + 'outside.xml')

    def classify(self, data, **attrs):
        attributes = {'TYPE':'valid', 'ENTITIES':'none', 'RECOMMENDATION':'XML1.0', **attrs}
        return suite.classify({'attributes': attributes}, data)

    def test_supported_acceptance_including_xmlns_local_names(self):
        for data in [b'<a/>', b'<xmlns/>', b'<a xmlns:p="urn:x" p:xmlns="v"/>', b'<a>&amp;&#65;</a>']:
            self.assertEqual('accepted', self.classify(data)['outcome'])

    def test_pure_malformed_and_unknown_entities_remain_rejections(self):
        for data in [b'<a>', b'<a>&unknown;</a>', b'<?xml version="1.0" encoding=" UTF-8"?><a/>', b'<?xml version="1.0" encoding="8-UTF"?><a/>', b'<a x="<"/>']:
            self.assertEqual('rejected', self.classify(data, TYPE='not-wf')['outcome'])

    def test_no_feature_strings_from_comments_cdata_or_pi(self):
        self.assertEqual('accepted', self.classify(b'<a><!-- <!DOCTYPE --> <![CDATA[<!DOCTYPE a>]]><?p <!DOCTYPE ?></a>')['outcome'])

    def test_utf16_feature_inspection(self):
        result = self.classify('<?xml version="1.0"?><!DOCTYPE a><a/>'.encode('utf-16'))
        self.assertIn('dtd-prohibited-in-m1', result['reasons'])

    def test_all_later_capabilities_remain_visible(self):
        cases = [(b'<!DOCTYPE a><a/>', {}, 'dtd-prohibited-in-m1'),
                 (b'<a/>', {'VERSION':'1.1'}, 'xml-1.1-or-other-version'),
                 (b'<a/>', {'ENTITIES':'general'}, 'external-general-entities'),
                 (b'<a/>', {'TYPE':'invalid'}, 'dtd-validity-oracle'),
                 (b'<a/>', {'NAMESPACES':'no'}, 'namespace-processing-disabled'),
                 (b'<a/>', {'EDITION':'1 2 3 4'}, 'historical-xml-edition'),
                 (b'<?xml version="1.0" encoding="iso-8859-1"?><a/>', {}, 'encoding-outside-m1:iso-8859-1')]
        for data, attrs, reason in cases:
            result = self.classify(data, **attrs)
            self.assertEqual('unsupported-profile', result['outcome'])
            self.assertIn(reason, result['reasons'])

    def test_zero_product_or_standards_passes(self):
        for c in self.inventory['cases']:
            self.assertFalse(c['m1']['productExecuted'])
            self.assertFalse(c['m1']['standardsPassed'])
        self.assertEqual(0, self.inventory['summary']['parserCasesExecuted'])
        self.assertEqual(0, self.inventory['summary']['standardsPassed'])

if __name__ == '__main__': unittest.main()
