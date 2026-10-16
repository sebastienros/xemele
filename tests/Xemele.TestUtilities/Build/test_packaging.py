import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('tool_packaging', ROOT / 'tools/Packaging/tool.py')
tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tool)


class PackagingTests(unittest.TestCase):
    def test_release_tag_version_and_prerelease_flag(self):
        self.assertEqual(tool.release_plan('refs/tags/v1.2.3-preview.4'), ('1.2.3-preview.4', True))
        self.assertEqual(tool.release_plan('refs/tags/v1.2.3'), ('1.2.3', False))
        for ref in ['refs/heads/main', 'refs/heads/release/1.2', 'refs/tags/1.2.3',
                    'refs/tags/v1.2', 'refs/tags/v01.2.3', 'refs/tags/v1.2.3-rc.01',
                    'refs/tags/v1.2.3\nchannel=preview']:
            with self.subTest(ref=ref), self.assertRaises(ValueError):
                tool.release_plan(ref)

    def test_plan_cli_writes_tag_version_after_validating_source(self):
        with tempfile.TemporaryDirectory() as temporary:
            for version, flag in [('1.2.3', 'false'), ('1.2.3-rc.1', 'true')]:
                output = Path(temporary) / version
                ref = f'refs/tags/v{version}'
                with self.subTest(version=version), \
                     patch.object(sys, 'argv', ['tool.py', 'plan', '--ref', ref, '--github-output', str(output)]), \
                     patch.object(tool, 'validate_tag_source') as validate:
                    tool.main()
                    validate.assert_called_once_with(ref)
                    self.assertEqual(output.read_text(), f'version={version}\nprerelease={flag}\n')

    def test_tag_source_rejects_nonancestor_and_git_failures(self):
        for code, exception in [(1, ValueError), (128, subprocess.CalledProcessError)]:
            with self.subTest(code=code), patch.object(tool, 'run') as fetch, \
                 patch.object(tool.subprocess, 'check_output', return_value='abc123\n') as resolve, \
                 patch.object(tool.subprocess, 'run', return_value=subprocess.CompletedProcess(['git'], code)) as ancestry:
                with self.assertRaises(exception):
                    tool.validate_tag_source('refs/tags/v1.2.3')
                fetch.assert_called_once_with(['git', 'fetch', '--no-tags', 'origin', 'main'])
                self.assertEqual(resolve.call_args.args[0], ['git', 'rev-parse', '--verify', 'refs/tags/v1.2.3^{commit}'])
                self.assertEqual(ancestry.call_args.args[0], ['git', 'merge-base', '--is-ancestor', 'abc123', 'FETCH_HEAD'])

    def test_invalid_versions(self):
        for version in ['v1.2.3', '1.2', '01.2.3', '1.2.3\nchannel=stable', '1.2.3-rc.01', '1.2.3-']:
            with self.subTest(version=version), self.assertRaises(ValueError):
                tool.validate_version(version)

    def test_native_targets_and_portable_fallback(self):
        project = ET.parse(ROOT / 'src/Xemele.Tool/Xemele.Tool.csproj')
        self.assertEqual(project.find('.//ToolCommandName').text, 'xemele')
        self.assertEqual(project.find('.//ToolPackageRuntimeIdentifiers').text.split(';'), list(tool.RIDS))
        self.assertEqual(project.find('.//PublishAot').get('Condition'), "'$(RuntimeIdentifier)' != 'any'")
        self.assertEqual(project.find('.//UseAppHost').get('Condition'), "'$(RuntimeIdentifier)' == 'any'")

    def test_feed_requires_all_implementations_and_native_runners(self):
        with tempfile.TemporaryDirectory() as temporary:
            feed = Path(temporary)
            version = '1.2.3-preview.4'

            def write_package(rid=None, runner=None, runtime_version='10.0.11'):
                name = f'Xemele.Tool{("." + rid) if rid else ""}.{version}.nupkg'
                settings = ET.Element('DotNetCliTool')
                if rid:
                    commands = ET.SubElement(settings, 'Commands')
                    entry = 'Xemele.Tool.dll' if rid == 'any' else 'Xemele.Tool'
                    ET.SubElement(commands, 'Command', Name='xemele', EntryPoint=entry,
                                  Runner=runner or ('dotnet' if rid == 'any' else 'executable'))
                else:
                    identifiers = ET.SubElement(settings, 'RuntimeIdentifierPackages')
                    for identifier in tool.RIDS:
                        ET.SubElement(identifiers, 'RuntimeIdentifierPackage',
                                      RuntimeIdentifier=identifier, Id=f'Xemele.Tool.{identifier}')
                with zipfile.ZipFile(feed / name, 'w') as archive:
                    for doc in ('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'icon.png'):
                        archive.writestr(doc, b'document')
                    archive.writestr(f'tools/net10.0/{rid or "any"}/DotnetToolSettings.xml', ET.tostring(settings))
                    if rid:
                        archive.writestr(f'tools/net10.0/{rid}/{entry}', b'implementation')
                        if rid == 'any':
                            archive.writestr('tools/net10.0/any/Xemele.Tool.runtimeconfig.json',
                                             '{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"' + runtime_version + '"}}}')

            write_package()
            for rid in tool.RIDS:
                write_package(rid)
            (feed / f'Xemele.{version}.nupkg').touch()
            tool.check_feed(feed, version)
            write_package('linux-x64', 'dotnet')
            with self.assertRaisesRegex(RuntimeError, 'incorrect tool runner'):
                tool.check_feed(feed, version)
            write_package('linux-x64')
            write_package('any', runtime_version='11.0.0')
            with self.assertRaisesRegex(RuntimeError, 'depend on the .NET 10 runtime'):
                tool.check_feed(feed, version)
            (feed / f'Xemele.Tool.linux-x64.{version}.nupkg').unlink()
            with self.assertRaisesRegex(RuntimeError, 'exactly'):
                tool.check_feed(feed, version)


if __name__ == '__main__':
    unittest.main()
