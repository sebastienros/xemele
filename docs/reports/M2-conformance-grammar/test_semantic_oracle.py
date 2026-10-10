"""Authored normative controls, independent of all product/reference responses."""
import unittest
from semantic_oracle import Expected

BASE = 'https://offline.invalid/main.xml'


def start(name='r', namespace='', attributes=None):
    return {'kind': 'start', 'namespaceUri': namespace, 'localName': name, 'attributes': attributes or []}


def end(name='r', namespace=''):
    return {'kind': 'end', 'namespaceUri': namespace, 'localName': name}


def attribute(value, name='a', namespace=''):
    return {'namespaceUri': namespace, 'localName': name, 'value': value}


class AuthoredOracleTests(unittest.TestCase):
    def expect(self, xml, events, resources=None):
        model = Expected(xml.encode('utf-8'), BASE, resources or {})
        self.assertEqual(events, model.events)
        return model

    def test_reference_and_literal_whitespace_are_different(self):
        self.expect("<r a='\t\r\n&#9;&#10;&#13;'/>", [start(attributes=[attribute('  \t\n\r')]), end()])

    def test_entity_declaration_character_reference_is_reprocessed_on_use(self):
        self.expect("<!DOCTYPE r [<!ENTITY e '&#9;'><!ATTLIST r a CDATA '&e;&#9;'>]><r/>",
                    [start(attributes=[attribute(' \t')]), end()])

    def test_tokenized_only_collapses_spaces(self):
        self.expect("<!DOCTYPE r [<!ATTLIST r a NMTOKENS ' x  &#9;y  '>]><r/>",
                    [start(attributes=[attribute('x \ty')]), end()])

    def test_first_declaration_and_internal_subset_precedence(self):
        self.expect("<!DOCTYPE r SYSTEM 'd.dtd' [<!ENTITY e 'first'><!ATTLIST r a CDATA 'internal'>]><r>&e;</r>",
                    [start(attributes=[attribute('internal')]), {'kind': 'text', 'value': 'first'}, end()],
                    {'https://offline.invalid/d.dtd': b"<!ENTITY e 'second'><!ATTLIST r a CDATA 'external'>"})

    def test_inclusion_supplies_spaces_and_conditional_status_bracket(self):
        self.expect("<!DOCTYPE r SYSTEM 'd.dtd'><r/>", [start(attributes=[attribute('yes')]), end()],
                    {'https://offline.invalid/d.dtd': b'<!ENTITY % n "r"><!ENTITY % status "INCLUDE["><![%status;<!ATTLIST%n;a CDATA "yes">]]>'})

    def test_default_namespace_does_not_apply_to_attributes(self):
        self.expect("<r xmlns='urn:r' xmlns:p='urn:p' p:a='v' a='u'><p:c/></r>",
                    [start(namespace='urn:r', attributes=[attribute('u'), attribute('v', namespace='urn:p')]),
                     start('c', 'urn:p'), end('c', 'urn:p'), end(namespace='urn:r')])

    def test_cdata_and_character_references_are_text_but_general_entities_are_markup(self):
        self.expect("<!DOCTYPE r [<!ENTITY e '<c/>'>]><r>a<![CDATA[&x;]]>&lt;&e;z</r>",
                    [start(), {'kind': 'text', 'value': 'a&x;<'}, start('c'), end('c'), {'kind': 'text', 'value': 'z'}, end()])

    def test_internal_reference_cr_is_not_literal_source_cr(self):
        self.expect("<!DOCTYPE r [<!ENTITY e '&#13;'>]><r>\r\n&e;&#13;</r>",
                    [start(), {'kind': 'text', 'value': '\n\r\r'}, end()])

    def test_effective10_preserves_nel_lsep_and_exact_version(self):
        model = self.expect("<?xml version='1.10' encoding='UTF-8' standalone='no'?><r>\u0085\u2028</r>",
                            [start(), {'kind': 'text', 'value': '\u0085\u2028'}, end()])
        self.assertEqual(('1.10', 'UTF-8', 'no'), (model.version, model.encoding, model.standalone))

    def test_bom_consumes_exactly_one_signature(self):
        model = Expected(b'\xef\xbb\xbf<r/>', BASE, {})
        self.assertEqual([start(), end()], model.events)
        model = self.expect("<!DOCTYPE r [<!ENTITY e SYSTEM 'e'>]><r>&e;</r>",
                            [start(), {'kind': 'text', 'value': '\ufeffdata'}, end()],
                            {'https://offline.invalid/e': b'\xff\xfe\xff\xfed\x00a\x00t\x00a\x00'})
        self.assertEqual(['https://offline.invalid/e'], model.opens)

    def test_unread_parameter_stops_later_declarations(self):
        model = self.expect("<!DOCTYPE r SYSTEM 'd.dtd'><r/>", [start(attributes=[attribute('before')]), end()],
                            {'https://offline.invalid/d.dtd': b"<!ATTLIST r a CDATA 'before'>%missing;<!ATTLIST r b CDATA 'after'>"})
        self.assertFalse(model.reading_declarations)

    def test_unknown_content_has_identity_provenance(self):
        model = self.expect("<!DOCTYPE r [<!ENTITY % p ''>%p;]><r>a&missing;b</r>",
                            [start(), {'kind': 'text', 'value': 'ab'}, end()])
        self.assertEqual([{'name': 'missing', 'lexicalReference': '&missing;', 'parentNamespace': '',
                           'parentName': 'r', 'sourceUri': BASE, 'sequence': 0}], model.unresolved)

    def test_unsupported_or_ungranted_model_is_a_preparation_failure(self):
        with self.assertRaisesRegex(ValueError, 'ungranted'):
            Expected(b"<!DOCTYPE r SYSTEM 'missing'><r/>", BASE, {})
        with self.assertRaisesRegex(ValueError, 'symbolic model'):
            Expected(b"<r a='&missing;'/>", BASE, {})
        with self.assertRaises(UnicodeError):
            Expected(b'<r>\xff</r>', BASE, {})


if __name__ == '__main__':
    unittest.main()
