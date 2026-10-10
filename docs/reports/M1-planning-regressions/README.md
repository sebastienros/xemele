# Planning regression isolation

Source revision: `d8e2e3648344e5c607044e8dc2b13ba140088cd7`.

At claim revision `4cd62f4d04afdbf46ab713fa22bd5d1b180b0afe`, the original 14 tests ran with three failures: removing a gate edge no longer removed transitive reachability, the selected workflow path no longer collided with CI ownership, and two bootstrap lanes acquired additional dependencies. These were changes to the live graph, rather than tracker defects.

The test fixture now owns eight explicit task records: one bootstrap, five independent lanes, their M0 gate, and the M1 gate. It creates its specification in a temporary root and validates the baseline before each mutation. The CI lane owns the workflow subtree; the corpus lane's mixed-case workflow file collides with it only in the negative case. The gate has direct edges to each otherwise independent lane, so omitting CI truly loses reachability. All 14 original test methods remain; the live graph test still loads the actual task records. `tasks.py` is unchanged.

`checks.txt` contains the fixed-source suite and 107-task validation results, all nine actual rejection diagnostics, and an additional audit: every synthetic test passes with repository loading forced to fail. This establishes independence from future live graph edits, including readiness scenarios. Runtime build, allocation and timing checks do not apply to this Python test-only change.

Reproduce the normal checks:

```sh
python3 -B -m unittest discover -s tools/planning -p test_tasks.py
python3 tools/planning/tasks.py validate
git diff --check
```

Reproduce the independence and diagnostic audit from the repository root:

```sh
python3 -B - <<'PY'
import sys
import unittest
from unittest.mock import patch
sys.path.insert(0, 'tools/planning')
import tasks as tracker
import test_tasks
names = unittest.defaultTestLoader.getTestCaseNames(test_tasks.TaskTrackingTests)
names.remove('test_repository_graph_valid')
suite = unittest.TestSuite(test_tasks.TaskTrackingTests(name) for name in names)
original = test_tasks.TaskTrackingTests.assertInvalid
def logged_rejection(case, fragment):
    original(case, fragment)
    print(f'Rejected mutation ({fragment}): {tracker.validate(case.tasks, case.root)}')
with patch.object(tracker, 'load_tasks', side_effect=AssertionError('Repository loading forbidden')) as loader, \
     patch.object(test_tasks.TaskTrackingTests, 'assertInvalid', logged_rejection):
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    loader.assert_not_called()
raise SystemExit(0 if result.wasSuccessful() else 1)
PY
```

The worker marks branch work `implemented`; coordinator integration and verification remain required.
