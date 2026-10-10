"""Focused negative checks for reference source attribution; no product mocks."""
from pathlib import Path
import unittest
from verify import pinned_environment, validate_reference_provenance


class ReferenceProvenanceTests(unittest.TestCase):
    def test_caller_overrides_are_replaced(self):
        dotnet = Path('/known/sdk/dotnet')
        runner = Path('/known/checkout/BclReference.dll')
        env = pinned_environment({'XEMELE_DOTNET':'/old/dotnet','XEMELE_BCL_RUNNER':'/old/runner','PATH':'/old'},Path('/results'),dotnet,runner)
        self.assertEqual(env['XEMELE_DOTNET'],str(dotnet))
        self.assertEqual(env['XEMELE_BCL_RUNNER'],str(runner))
        self.assertTrue(env['PATH'].startswith(str(dotnet.parent)))

    def test_current_hash_and_build_pass(self):
        validate_reference_provenance([self.record()], 'current', 'revision')

    def test_old_hash_fails_even_with_matching_runtime_and_build(self):
        row = self.record()
        row['runnerSha256'] = 'old'
        with self.assertRaisesRegex(AssertionError, 'hash differs'):
            validate_reference_provenance([row], 'current', 'revision')

    def test_old_build_fails_even_with_current_hash(self):
        row = self.record()
        row['response']['engine']['build'] = '1.0.0+old'
        with self.assertRaisesRegex(AssertionError, 'tested source revision'):
            validate_reference_provenance([row], 'current', 'revision')

    def test_every_record_is_checked(self):
        bad = self.record()
        bad['runnerSha256'] = 'old'
        with self.assertRaises(AssertionError):
            validate_reference_provenance([self.record(),bad], 'current', 'revision')

    @staticmethod
    def record():
        return {'runnerSha256':'current','response':{'engine':{'name':'System.Xml','version':'10.0.0.0','runtime':'.NET 10.0.11','build':'1.0.0+revision'}}}
