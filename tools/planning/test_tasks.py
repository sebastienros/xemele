"""Regression checks for task dependency, ownership, and evidence guarantees."""

from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

import tasks as tracker


class TaskTrackingTests(unittest.TestCase):
    def setUp(self):
        # Own the whole scenario, including specification existence. Repository
        # task splits, dependency repairs and progress must not change its graph.
        directory = TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        (self.root / "spec.md").write_text("Synthetic planning specification.\n")
        self.tasks = {}

        def add(task_id, dependencies, owns, kind="infrastructure"):
            self.tasks[task_id] = {
                "schema_version": 1, "id": task_id,
                "milestone": task_id.split("-")[0], "title": task_id,
                "kind": kind, "status": "planned", "owner": None,
                "branch": None, "base_revision": None, "integration_revision": None,
                "depends_on": dependencies, "goals": ["G18"], "specs": ["spec.md"],
                "owns": owns, "api_or_output": "Synthetic graph scenario",
                "acceptance": [{"id": task_id + "-A1", "description": "Synthetic criterion",
                                "status": "pending", "evidence": []}],
                "implementation": [], "evidence": [], "blockers": [],
                "handoff": {"summary": "Synthetic fixture", "checks": [],
                            "remaining": [], "requests": []},
            }

        add("M0-build", [], ["build/"])
        lanes = {
            "M0-ci": [".github/workflows/"],
            "M0-corpora": ["fixtures/corpora/"],
            "M0-references": ["tools/references/"],
            "M0-tree-spike": ["experiments/tree/"],
            "M0-input-spike": ["experiments/input/"],
        }
        for task_id, owns in lanes.items():
            add(task_id, ["M0-build"], owns)
        add("M0-gate", list(lanes), [], kind="gate")
        add("M1-gate", ["M0-gate"], [], kind="gate")
        # Every negative test starts from a known valid, isolated graph.
        self.assertEqual([], tracker.validate(self.tasks, self.root))

    def assertInvalid(self, fragment):
        errors = tracker.validate(self.tasks, self.root)
        self.assertTrue(any(fragment in error for error in errors), errors)

    def verify(self, task_id):
        task = self.tasks[task_id]
        task.update(status="verified", owner="test-session", branch="test-branch",
                    base_revision="a" * 40, integration_revision="b" * 40,
                    implementation=["test-only fixture"])
        task["evidence"] = [{"id": "integrated", "kind": "integration", "revision": "b" * 40,
                             "command": "test-only check", "result": "passed",
                             "summary": "Synthetic evidence in an in-memory test only.", "artifact": None}]
        for criterion in task["acceptance"]:
            criterion.update(status="passed", evidence=["integrated"])

    def test_repository_graph_valid(self):
        actual, errors = tracker.load_tasks()
        self.assertEqual([], errors)
        self.assertEqual([], tracker.validate(actual))

    def test_initial_readiness(self):
        self.assertEqual(["M0-build"], [key for key, task in self.tasks.items() if tracker.ready(task, self.tasks)])

    def test_verified_bootstrap_unlocks_independent_lanes(self):
        self.verify("M0-build")
        self.assertEqual([], tracker.validate(self.tasks, self.root))
        self.assertEqual({"M0-ci", "M0-corpora", "M0-references", "M0-tree-spike", "M0-input-spike"},
                         {key for key, task in self.tasks.items() if tracker.ready(task, self.tasks)})

    def test_branch_implementation_does_not_unlock_dependents(self):
        self.verify("M0-build")
        self.tasks["M0-build"]["status"] = "implemented"
        self.tasks["M0-build"]["integration_revision"] = None
        self.assertFalse(tracker.ready(self.tasks["M0-ci"], self.tasks))

    def test_missing_integration_evidence_rejected(self):
        self.verify("M0-build")
        self.tasks["M0-build"]["integration_revision"] = "c" * 40
        self.assertInvalid("passing integration evidence")

    def test_unproven_acceptance_rejected(self):
        self.tasks["M0-build"]["acceptance"][0]["status"] = "passed"
        self.assertInvalid("without passing evidence")

    def test_later_milestone_dependency_rejected(self):
        self.tasks["M0-build"]["depends_on"] = ["M1-gate"]
        self.assertInvalid("forward milestone")

    def test_cycle_rejected(self):
        self.tasks["M0-build"]["depends_on"] = ["M0-ci"]
        self.assertInvalid("dependency cycle")

    def test_unordered_path_overlap_rejected_case_insensitively(self):
        self.tasks["M0-corpora"]["owns"].append(".GITHUB/workflows/check.yml")
        self.assertInvalid("unordered ownership overlap: M0-ci / M0-corpora")

    def test_similar_prefixes_are_not_ownership_overlap(self):
        self.assertFalse(tracker.overlaps("src/Foo/", "src/Foobar/"))
        self.assertTrue(tracker.overlaps("src/Foo/", "src/Foo/Bar.cs"))

    def test_started_task_with_unverified_dependency_rejected(self):
        self.verify("M0-ci")
        self.assertInvalid("started task has unverified dependency")

    def test_gate_cannot_omit_a_task(self):
        self.tasks["M0-gate"]["depends_on"].remove("M0-ci")
        self.assertInvalid("M0-gate: gate does not cover ['M0-ci']")

    def test_malformed_status_reports_error(self):
        self.tasks["M0-build"]["status"] = []
        self.assertInvalid("must be nonempty strings")

    def test_missing_specification_reports_error(self):
        self.tasks["M0-build"]["specs"] = ["docs/specs/missing-file.md"]
        self.assertInvalid("missing specification")


if __name__ == "__main__":
    unittest.main()
