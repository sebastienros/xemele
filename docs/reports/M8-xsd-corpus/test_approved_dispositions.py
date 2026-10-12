"""Exact approval/report controls, SOURCE only until coordinator acceptance."""
import copy
import importlib.util
import unittest
from pathlib import Path

s = importlib.util.spec_from_file_location('dispositions', Path(__file__).with_name('dispositions.py'))
m = importlib.util.module_from_spec(s)
s.loader.exec_module(m)


class ExactApproval(unittest.TestCase):
    def row(self, name):
        record = m.approved_sources()[name]
        return dict(id=name, engine='product', classification='applicable', kind=record['kind'],
                    expected=record['expected'], actual=record['actual'], passed=False, codes=record['codes'])

    def test_exact_scope_and_original_nonpasses_are_preserved(self):
        records = m.approved_sources()
        self.assertEqual(len(records), 36)
        for name, record in records.items():
            with self.subTest(id=name):
                row = self.row(name)
                prior = copy.deepcopy(row)
                view = m.view(row)
                self.assertEqual(row, prior)
                self.assertIs(view['originalPassed'], False)
                self.assertEqual(view['approvedGateDisposition'], record['category'])
                self.assertNotEqual(view['profilePassed'], True)

    def test_real_schema_and_dependent_diagnostic_are_approved_without_pass_credit(self):
        for name in ['msMeta/Particles_w3c.xml#g248/s:particlesHa070',
                     'msMeta/Particles_w3c.xml#g705/i0:particlesR013.v']:
            row = self.row(name)
            self.assertEqual(row['codes'], ['XSD.SchemaRestriction'])
            self.assertIsNotNone(m.view(row)['approvedGateDisposition'])
            self.assertFalse(m.view(row)['originalPassed'])

    def test_quotas_denial_syntax_unknown_and_extra_diagnostics_are_not_approved(self):
        for name in m.approved_sources():
            row = self.row(name)
            for codes in [['SCHEMA.Limit'], ['SCHEMA.ResourceDenied'], ['CALLER.Cancel'],
                          ['harness-exception'], ['XSD.Content'], row['codes'] + ['SCHEMA.Limit']]:
                with self.subTest(id=name, codes=codes):
                    self.assertIsNone(m.view({**row, 'codes': codes})['approvedGateDisposition'])
            if row['codes'] != ['SCHEMA.Syntax']:
                self.assertIsNone(m.view({**row, 'codes': ['SCHEMA.Syntax']})['approvedGateDisposition'])

    def test_altered_id_engine_expected_kind_scope_actual_or_original_result_are_not_approved(self):
        for name in m.approved_sources():
            row = self.row(name)
            for changes in [dict(id=name+'-neighbor'), dict(engine='bcl'), dict(engine=None),
                            dict(expected='different'), dict(kind='different'), dict(classification='excluded'),
                            dict(actual='harness-exception'), dict(actual='timeout'), dict(actual='cancellation'),
                            dict(passed=True), dict(passed=None)]:
                with self.subTest(id=name, changes=changes):
                    self.assertIsNone(m.view({**row, **changes})['approvedGateDisposition'])

    def test_syntax_and_cycle_approval_never_means_complete_invalidity(self):
        for name, record in m.approved_sources().items():
            if record['category'] in ['fatal-XML-unassessed', 'malformed-hint-prerequisite-unassessed',
                                      'indeterminate-resource-cycle', 'cycle-dependent-unassessed']:
                row = self.row(name)
                self.assertIn(row['actual'], ['incomplete-refusal', 'schema-unavailable'])
                self.assertFalse(m.view(row)['originalPassed'])
                self.assertNotEqual(m.view(row)['profilePassed'], True)
                self.assertIsNone(m.view({**row, 'actual': 'instance-invalid'})['approvedGateDisposition'])

    def test_profile_agreement_outside_exact_human_approval_is_not_gate_exception(self):
        row = dict(id=m.ERR, engine='product', classification='applicable', expected='valid', kind='schema',
                   actual='schema-invalid', passed=False, codes=['XSD.SchemaRestriction'])
        view = m.view(row)
        self.assertFalse(view['originalPassed'])
        self.assertTrue(view['profilePassed'])
        self.assertIsNone(view['approvedGateDisposition'])

    def test_optional_a004_derived_valid_never_changes_original_or_grants_exception(self):
        row = dict(id=m.OPTIONAL_A004, engine='product', classification='applicable', expected='invalid', kind='instance',
                   actual='instance-valid', passed=False, codes=[])
        view = m.view(row)
        self.assertFalse(view['originalPassed'])
        self.assertTrue(view['profilePassed'])
        self.assertIsNone(view['approvedGateDisposition'])
        for actual in ['incomplete-refusal', 'schema-unavailable', 'instance-invalid', 'harness-exception']:
            self.assertFalse(m.view({**row, 'actual': actual})['profilePassed'])


if __name__ == '__main__':
    unittest.main()
