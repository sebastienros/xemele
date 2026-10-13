"""Run unchanged historical lifecycle/binding controls on their exact binding fixtures.

Only the temporary fixture builder restores the four historically pinned source
files. Production current source is untouched and checked independently by the
M9 reviewed-current-inputs manifest; the old positive freeze is not relabeled.
"""
import sys
sys.dont_write_bytecode=True
import hashlib,importlib.util,subprocess,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
path=ROOT/'docs/reports/M8-xsd-corpus/test_supervisor.py'
spec=importlib.util.spec_from_file_location('historical_supervisor',path);controls=importlib.util.module_from_spec(spec);spec.loader.exec_module(controls)
original=controls.Supervisor.runtime_binding_fixture
changed=['src/Xemele/Tree/Document.cs','src/Xemele/Tree/SchemaSourceLocations/Document.SourceLocations.cs',
         'src/Xemele/XPath/Navigation/XPathNavigator.cs','src/Xemele/XPath/Evaluation/Evaluator.cs']
def historical_fixture(self):
    root,here,file,binding=original(self)
    for name in changed:
        payload=subprocess.check_output(['git','show','6657e8e0ab0f34602a5040a04ee0d8fa197f70b1:'+name],cwd=ROOT,timeout=10)
        assert hashlib.sha256(payload).hexdigest()==binding['runtime_production_inputs'][name]
        (root/name).write_bytes(payload)
    return root,here,file,binding
controls.Supervisor.runtime_binding_fixture=historical_fixture
suite=unittest.defaultTestLoader.loadTestsFromTestCase(controls.Supervisor)
assert suite.countTestCases()==39
result=unittest.TextTestRunner(verbosity=1).run(suite)
if not result.wasSuccessful():raise SystemExit(1)
print('PASS39 unchanged historical controls; four temporary fixture bytes restored exactly; current production separately reviewed')
