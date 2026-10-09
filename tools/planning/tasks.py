#!/usr/bin/env python3
"""Read-only task reports and consistency checks. Uses only Python's standard library."""

import argparse
from collections import Counter
import json
from pathlib import Path, PurePosixPath
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
STATES = {"planned", "in_progress", "blocked", "implemented", "verified"}
KINDS = {"infrastructure", "contract", "feature", "verification", "gate"}
REVISION = re.compile(r"[0-9a-f]{40}\Z")


def load_tasks(root=ROOT):
    tasks = {}
    errors = []
    for path in sorted((root / "docs/tasks").glob("*.json")):
        try:
            task = json.loads(path.read_text())
            if not isinstance(task, dict) or task.get("id") != path.stem:
                raise ValueError("task id must match the filename")
            tasks[path.stem] = task
        except (ValueError, OSError) as error:
            errors.append(f"{path.name}: {error}")
    if not tasks:
        errors.append("No task records found")
    return tasks, errors


def safe_path(value):
    return (isinstance(value, str) and bool(value) and "\\" not in value
            and not PurePosixPath(value).is_absolute()
            and not any(p in {".", ".."} for p in value.split("/"))
            and not any(c in value for c in "*?[]"))


def overlaps(first, second):
    # Normalize case as worktrees may be on case-insensitive filesystems.
    first, second = first.casefold(), second.casefold()
    return (first.rstrip("/") == second.rstrip("/")
            or first.endswith("/") and second.startswith(first)
            or second.endswith("/") and first.startswith(second))


def validate(tasks, root=ROOT):
    errors = []
    required = {"schema_version", "id", "milestone", "title", "kind", "status",
                "owner", "branch", "base_revision", "integration_revision",
                "depends_on", "goals", "specs", "owns", "api_or_output", "acceptance",
                "implementation", "evidence", "blockers", "handoff"}
    for key, task in tasks.items():
        def error(message):
            errors.append(f"{key}: {message}")

        missing = required - task.keys()
        if missing:
            error(f"missing fields: {', '.join(sorted(missing))}")
            continue
        if any(not isinstance(task[name], str) or not task[name].strip()
               for name in ("id", "milestone", "title", "kind", "status", "api_or_output")):
            error("id/milestone/title/kind/status/api_or_output must be nonempty strings")
            continue
        if task["schema_version"] != 1:
            error("unsupported schema_version")
        if not re.fullmatch(r"M[0-9]+-[a-z0-9-]+", key):
            error("invalid task id")
            continue
        if task["milestone"] != key.split("-")[0]:
            error("milestone must match task id")
        if task["status"] not in STATES or task["kind"] not in KINDS:
            error("invalid status or kind")
        for name in ("depends_on", "goals", "specs", "owns", "implementation", "blockers"):
            value = task[name]
            if not isinstance(value, list) or not all(isinstance(x, str) and x for x in value):
                error(f"{name} must be a list of nonempty strings")
            elif len(value) != len(set(value)):
                error(f"{name} contains duplicates")
        if any(not isinstance(task[name], list) or not all(isinstance(x, str) and x for x in task[name])
               for name in ("depends_on", "goals", "specs", "owns", "implementation", "blockers")):
            continue
        if any(not isinstance(task[name], list) for name in
               ("depends_on", "goals", "specs", "owns", "implementation", "blockers", "evidence", "acceptance")):
            error("collection fields must be arrays")
            continue
        if not task["goals"] or any(not re.fullmatch(r"G(?:0[1-9]|1[0-8])", str(g)) for g in task["goals"]):
            error("goals must identify roadmap goals G01–G18")
        for dep in task["depends_on"]:
            if not isinstance(dep, str) or dep not in tasks:
                error(f"unknown dependency {dep}")
            elif int(dep.split("-")[0][1:]) > int(key.split("-")[0][1:]):
                error(f"forward milestone dependency: {dep}")
            elif task["status"] != "planned" and tasks[dep].get("status") != "verified":
                error(f"started task has unverified dependency: {dep}")
        for name in ("specs", "owns"):
            for path in task[name]:
                if not safe_path(path):
                    error(f"invalid repository path in {name}: {path}")
                elif name == "specs" and not (root / path).is_file():
                    error(f"missing specification: {path}")
        if not task["specs"]:
            error("at least one specification is required")
        for path in task["owns"]:
            if isinstance(path, str) and path.startswith("docs/tasks/"):
                error("task records are implicitly owned; do not claim other task files")

        claimed = task["status"] != "planned"
        for name in ("owner", "branch"):
            if claimed and not (isinstance(task[name], str) and task[name].strip()):
                error(f"claimed task requires {name}")
            if not claimed and task[name] is not None:
                error(f"planned task must not have {name}")
        for name in ("base_revision", "integration_revision"):
            value = task[name]
            if value is not None and (not isinstance(value, str) or not REVISION.fullmatch(value)):
                error(f"{name} must be a full Git revision")
        if claimed and not task["base_revision"]:
            error("claimed task requires base_revision")
        if task["status"] == "blocked" and not task["blockers"]:
            error("blocked status requires a concrete blocker")
        if task["status"] in {"implemented", "verified"} and task["blockers"]:
            error("completed work cannot have unresolved blockers")

        evidence = {}
        for item in task["evidence"]:
            if not isinstance(item, dict):
                error("evidence must contain objects")
                continue
            fields = {"id", "kind", "revision", "command", "result", "summary", "artifact"}
            if not fields <= item.keys():
                error("evidence requires id/kind/revision/command/result/summary/artifact")
                continue
            if not isinstance(item["id"], str) or not item["id"] or item["id"] in evidence:
                error("evidence IDs must be unique nonempty strings")
                continue
            evidence[item["id"]] = item
            if not isinstance(item["kind"], str) or item["kind"] not in {"check", "review", "integration"}:
                error("invalid evidence kind")
            if not isinstance(item["result"], str) or item["result"] not in {"passed", "failed", "skipped"}:
                error("invalid evidence result")
            if not isinstance(item["revision"], str) or not REVISION.fullmatch(item["revision"]):
                error("evidence requires tested full revision")
            if not isinstance(item["summary"], str) or not item["summary"].strip():
                error("evidence requires a durable outcome summary")
            if item["kind"] != "review" and not item["command"]:
                error("check/integration evidence requires the actual command")
            if item["artifact"] is not None and not safe_path(item["artifact"]):
                error("artifact must be a repository-relative path or null")

        criteria_ids = set()
        if not task["acceptance"]:
            error("acceptance criteria are required")
        for criterion in task["acceptance"]:
            if not isinstance(criterion, dict) or not {"id", "description", "status", "evidence"} <= criterion.keys():
                error("malformed acceptance criterion")
                continue
            cid = criterion["id"]
            if not isinstance(cid, str) or not cid.startswith(key + "-A") or cid in criteria_ids:
                error("acceptance IDs must be unique and task-prefixed")
            else:
                criteria_ids.add(cid)
            if not isinstance(criterion["status"], str) or criterion["status"] not in {"pending", "passed", "failed"}:
                error("invalid acceptance status")
            refs = criterion["evidence"]
            if not isinstance(refs, list) or not all(isinstance(r, str) for r in refs):
                error("criterion evidence must be evidence-ID strings")
                continue
            if any(ref not in evidence for ref in refs):
                error(f"{cid} references nonexistent evidence")
            if criterion["status"] == "passed" and not any(evidence.get(ref, {}).get("result") == "passed" for ref in refs):
                error(f"{cid} marked passed without passing evidence")
        if task["status"] in {"implemented", "verified"}:
            if not task["implementation"]:
                error("implemented work needs file/symbol references")
            if any(not isinstance(c, dict) or c.get("status") != "passed" for c in task["acceptance"]):
                error("implemented work requires all acceptance criteria passed")
            if not any(e.get("result") == "passed" and e.get("kind") in {"check", "integration"} for e in evidence.values()):
                error("implemented work requires passing check evidence")
        if task["status"] == "verified":
            revision = task["integration_revision"]
            if not revision:
                error("verified task requires integration_revision")
            if not any(e.get("kind") == "integration" and e.get("result") == "passed" and e.get("revision") == revision for e in evidence.values()):
                error("verified task requires passing integration evidence at integration_revision")
        handoff = task["handoff"]
        if not isinstance(handoff, dict) or not {"summary", "checks", "remaining", "requests"} <= handoff.keys():
            error("handoff requires summary/checks/remaining/requests")

    # Stop before graph traversal if malformed schema would make it unsafe.
    if errors:
        return errors
    ancestors = {}
    visiting = set()

    def visit(key):
        if key in visiting:
            raise ValueError(f"dependency cycle at {key}")
        if key not in ancestors:
            visiting.add(key)
            all_deps = set()
            for dep in tasks[key]["depends_on"]:
                all_deps.add(dep)
                all_deps.update(visit(dep))
            visiting.remove(key)
            ancestors[key] = all_deps
        return ancestors[key]

    try:
        for key in tasks:
            visit(key)
    except ValueError as error:
        return [str(error)]
    keys = sorted(tasks)
    for i, first in enumerate(keys):
        for second in keys[i + 1:]:
            if first in ancestors[second] or second in ancestors[first]:
                continue
            collisions = [(a, b) for a in tasks[first]["owns"] for b in tasks[second]["owns"] if overlaps(a, b)]
            if collisions:
                errors.append(f"unordered ownership overlap: {first} / {second}: {collisions[0]}")
    for milestone in sorted({t["milestone"] for t in tasks.values()}):
        members = [k for k in keys if tasks[k]["milestone"] == milestone]
        gates = [k for k in members if tasks[k]["kind"] == "gate"]
        if len(gates) != 1:
            errors.append(f"{milestone}: expected exactly one integration gate")
            continue
        gate = gates[0]
        omitted = set(members) - ancestors[gate] - {gate}
        if omitted:
            errors.append(f"{gate}: gate does not cover {sorted(omitted)}")
        number = int(milestone[1:])
        if number and f"M{number - 1}-gate" not in ancestors[gate]:
            errors.append(f"{gate}: missing previous milestone gate")
    return errors


def ready(task, tasks):
    return (task["status"] == "planned" and not task["blockers"]
            and all(tasks[d]["status"] == "verified" for d in task["depends_on"]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("ready", "status", "show", "validate"))
    parser.add_argument("id", nargs="?")
    parser.add_argument("--milestone")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    tasks, errors = load_tasks()
    if not errors:
        errors = validate(tasks)
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    if args.command == "validate":
        print(f"Validated {len(tasks)} tasks: schema, evidence, acyclic dependencies, additive milestones, and ordered ownership.")
        return 0
    if args.command == "show":
        if args.id not in tasks:
            parser.error("show requires a known task ID")
        print(json.dumps(tasks[args.id], indent=2))
        return 0
    selected = [t for t in tasks.values() if args.milestone is None or t["milestone"] == args.milestone]
    if args.milestone and not selected:
        parser.error("unknown milestone")
    if args.command == "ready":
        rows = [{"id": t["id"], "title": t["title"]} for t in selected if ready(t, tasks)]
        if args.json:
            print(json.dumps(rows, indent=2))
        else:
            print("\n".join(f"{r['id']}: {r['title']}" for r in rows) or "No ready tasks.")
        return 0
    rows = []
    for milestone in sorted({t["milestone"] for t in selected}, key=lambda s: int(s[1:])):
        group = [t for t in selected if t["milestone"] == milestone]
        counts = Counter(t["status"] for t in group)
        criteria = Counter(c["status"] for t in group for c in t["acceptance"])
        rows.append({"milestone": milestone, "total": len(group), **{s: counts[s] for s in sorted(STATES)},
                     "ready": sum(ready(t, tasks) for t in group), "criteria": dict(criteria)})
    if args.json:
        print(json.dumps(rows, indent=2))
    else:
        for row in rows:
            print(f"{row['milestone']}: {row['verified']}/{row['total']} verified; "
                  f"{row['implemented']} implemented; {row['in_progress']} in progress; "
                  f"{row['blocked']} blocked; {row['ready']} ready; "
                  f"{row['criteria'].get('passed', 0)} acceptance criteria passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
