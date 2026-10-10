"""Failure preservation regression: malformed first report cannot erase later raw data."""
import gzip
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('pi_verifier', Path(__file__).with_name('verify.py'))
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)

class ChildArchiveTests(unittest.TestCase):
    def test_malformed_first_report_preserves_all_later_raw_files(self):
        with tempfile.TemporaryDirectory(prefix='xemele-child-archive-') as temporary:
            root=Path(temporary); first=root/'first';later=root/'later';first.mkdir();later.mkdir()
            files={first/'real.report.json':b'{invalid',first/'real.stderr.txt':b'first failure\n',
                   later/'positive.report.json':b'{"AllocatedBytes":6400000}',later/'positive.stdout.txt':b'later output\n',
                   later/'positive.xml':b'<assemblies/>',later/'positive.exit.json':b'{"exitCode":0}'}
            for path,data in files.items():path.write_bytes(data)
            verifier.archive_child_files([first,later],root/'retained')
            with self.assertRaises(json.JSONDecodeError):json.loads((first/'real.report.json').read_text())
            for path,data in files.items():
                retained=root/'retained/child-runs'/path.parent.name/(path.name+'.gz')
                self.assertEqual(data,gzip.decompress(retained.read_bytes()))

if __name__=='__main__':unittest.main()
