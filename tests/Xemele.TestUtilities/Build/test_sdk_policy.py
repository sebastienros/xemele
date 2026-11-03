import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'tools/ApiBaseline'))
from sdk_policy import validate_sdk

spec = importlib.util.spec_from_file_location('api_sdk_verifier', ROOT / 'tools/ApiBaseline/verify.py')
api = importlib.util.module_from_spec(spec)
spec.loader.exec_module(api)
spec = importlib.util.spec_from_file_location('corpus_sdk_verifier', ROOT / 'tools/Validation/check_corpus_cli.py')
corpus = importlib.util.module_from_spec(spec)
spec.loader.exec_module(corpus)


class ReachedChecks(Exception):
    pass


class SdkPolicyTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.policy = dict(version='10.0.100', rollForward='latestFeature', allowPrerelease=False)
        self.write_policy()

    def write_policy(self):
        (self.root / 'global.json').write_text(json.dumps(dict(sdk=self.policy)), encoding='utf-8')

    def test_exact_patch_and_feature_roll_forward(self):
        for selected in ['10.0.100', '10.0.101', '10.0.200', '10.0.400', '10.0.403']:
            with self.subTest(selected=selected):
                validate_sdk(self.root, selected)

    def test_older_minor_major_and_prerelease_versions_fail(self):
        for selected in ['10.0.99', '9.0.500', '10.1.100', '11.0.100',
                         '10.0.403-preview.1', '10.0.100-rc.1']:
            with self.subTest(selected=selected), self.assertRaises(ValueError):
                validate_sdk(self.root, selected)

    def test_malformed_versions_fail_explicitly(self):
        for selected in ['', '10.0', '10.0.403.1', 'v10.0.403', '10.0.0403',
                         '10.0.403\n11.0.100', '10.0.403+metadata', None]:
            with self.subTest(selected=selected), self.assertRaisesRegex(ValueError, 'stable SDK version'):
                validate_sdk(self.root, selected)

    def test_declared_minimum_is_read_instead_of_hard_coded(self):
        self.policy['version'] = '10.0.400'
        self.write_policy()
        with self.assertRaisesRegex(ValueError, '>= 10.0.400'):
            validate_sdk(self.root, '10.0.399')
        validate_sdk(self.root, '10.0.403')

    def test_policy_changes_are_not_silently_accepted(self):
        for field, value in [('rollForward', 'latestMajor'), ('rollForward', 'disable'),
                             ('allowPrerelease', True), ('allowPrerelease', 'false')]:
            with self.subTest(field=field, value=value):
                original = self.policy[field]
                self.policy[field] = value
                self.write_policy()
                with self.assertRaisesRegex(ValueError, 'latestFeature with allowPrerelease=false'):
                    validate_sdk(self.root, '10.0.403')
                self.policy[field] = original

    def test_missing_corrupt_and_invalid_policy_fail(self):
        path = self.root / 'global.json'
        path.unlink()
        with self.assertRaises(FileNotFoundError):
            validate_sdk(self.root, '10.0.403')
        path.write_text('{invalid', encoding='utf-8')
        with self.assertRaises(json.JSONDecodeError):
            validate_sdk(self.root, '10.0.403')
        self.policy['version'] = '10.0'
        self.write_policy()
        with self.assertRaisesRegex(ValueError, 'stable SDK version'):
            validate_sdk(self.root, '10.0.403')

    def test_api_entrypoint_validates_before_running_checks(self):
        for selected in ['10.0.403', '11.0.100']:
            def run(*args):
                if args == ('git', 'rev-parse', 'HEAD'):
                    return 'source\n'
                if args == ('dotnet', '--version'):
                    return selected + '\n'
                raise ReachedChecks(args)

            with self.subTest(selected=selected), patch.object(api, 'ROOT', self.root), \
                 patch.object(api, 'LOG', []), patch.object(api, 'run', side_effect=run) as command:
                if selected == '10.0.403':
                    with self.assertRaises(ReachedChecks):
                        api.main()
                    self.assertEqual(command.call_args.args, ('dotnet', '--info'))
                else:
                    with self.assertRaisesRegex(ValueError, 'does not satisfy global.json'):
                        api.main()
                    self.assertEqual(command.call_count, 2)

    def test_corpus_entrypoint_validates_before_running_checks(self):
        for selected in ['10.0.403', '11.0.100']:
            def run(args):
                if args[0] == 'git':
                    return subprocess.CompletedProcess(args, 0, stdout='source\n')
                if args == ['dotnet', '--version']:
                    return subprocess.CompletedProcess(args, 0, stdout=selected + '\n')
                raise ReachedChecks(args)

            with self.subTest(selected=selected), patch.object(corpus, 'ROOT', self.root), \
                 patch.object(corpus, 'ARTIFACTS', self.root / 'artifacts'), \
                 patch.object(corpus, 'LOG', []), patch.object(corpus, 'run', side_effect=run) as command:
                if selected == '10.0.403':
                    with self.assertRaises(ReachedChecks):
                        corpus.main()
                    self.assertEqual(command.call_args.args[0][:2], ['dotnet', 'restore'])
                else:
                    with self.assertRaisesRegex(ValueError, 'does not satisfy global.json'):
                        corpus.main()
                    self.assertEqual(command.call_count, 2)

    def test_failed_or_missing_sdk_command_cannot_pass(self):
        for module, args in [(api, ('dotnet', '--version')), (corpus, (['dotnet', '--version'],))]:
            with self.subTest(module=module.__name__), patch.object(module, 'LOG', []), \
                 patch.object(module.subprocess, 'run', return_value=subprocess.CompletedProcess(
                     ['dotnet', '--version'], 1, stdout='10.0.403\n', stderr='resolution failed')):
                with self.assertRaises(AssertionError):
                    module.run(*args)
            for failure in [FileNotFoundError('dotnet'), subprocess.TimeoutExpired('dotnet', 120)]:
                with self.subTest(module=module.__name__, failure=type(failure).__name__), \
                     patch.object(module.subprocess, 'run', side_effect=failure):
                    with self.assertRaises(type(failure)):
                        module.run(*args)


if __name__ == '__main__':
    unittest.main()
