import hashlib
import json
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]
REFERENCES = Path('tools/ReferenceRunners/RelaxNg')


class CiPrerequisiteTests(unittest.TestCase):
    def test_frozen_reference_hashes_survive_windows_checkout(self):
        identity = json.loads((ROOT / REFERENCES / 'reference-build.json').read_text(encoding='utf-8'))
        for engine, name in [('jing', 'RngReference.java'), ('libxml2', 'libxml2_reference.c')]:
            with self.subTest(engine=engine):
                path = REFERENCES / name
                expected = identity[engine]['sourceSha256']
                self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), expected)
                checkout = subprocess.check_output(
                    ['git', '-c', 'core.autocrlf=true', 'cat-file', '--filters', 'HEAD:' + path.as_posix()],
                    cwd=ROOT)
                self.assertEqual(hashlib.sha256(checkout).hexdigest(), expected)


if __name__ == '__main__':
    unittest.main()
