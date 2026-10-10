"""Failure preservation regression: malformed first report cannot erase later raw data."""
import gzip
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import subprocess

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

class NegativeTimeoutArchiveTests(unittest.TestCase):
    def test_mocked_timeout_preserves_current_case_outputs_and_request(self):
        spec=importlib.util.spec_from_file_location('framing_negatives',Path(__file__).with_name('protocol_negative.py'))
        negative=importlib.util.module_from_spec(spec);spec.loader.exec_module(negative)
        with tempfile.TemporaryDirectory(prefix='xemele-negative-preservation-') as temporary:
            root=Path(temporary);case=root/'current';case.mkdir();destination=root/'retained'
            (case/'real.request.json').write_bytes(b'guard request before launch')
            logs=[]
            error=subprocess.TimeoutExpired(['known-child'],40,output=b'partial stdout\n',stderr=b'partial stderr\n')
            with patch.object(negative.subprocess,'run',side_effect=error):
                with self.assertRaises(subprocess.TimeoutExpired):negative.run_case(['known-child'],{},case,destination,logs,'current')
            for name,expected in [('stdout.txt',b'partial stdout\n'),('stderr.txt',b'partial stderr\n'),('real.request.json',b'guard request before launch')]:
                self.assertEqual(gzip.decompress((destination/(name+'.gz')).read_bytes()),expected)
            status=json.loads(gzip.decompress((destination/'exit.json.gz').read_bytes()))
            self.assertTrue(status['timedOut']);self.assertIsNone(status['exitCode'])
            self.assertIn('partial stdout',logs[0]);self.assertIn('timedOut=True',logs[0])

if __name__=='__main__' :unittest.main()
