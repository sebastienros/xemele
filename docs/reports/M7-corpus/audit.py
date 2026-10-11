"""Read-only exact-claim ancestry and task ownership audit."""
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[3]
claim = "5e5f7572ed4ad3071d48f74cba9ec741ab5a78fc"
task = json.loads((root / "docs/tasks/M7-corpus.json").read_text())

def git(*args):
    return subprocess.check_output(["git", *args], cwd=root, text=True).strip()

assert git("branch", "--show-current") == "codex/m7-corpus"
assert task["base_revision"] == "09f43321521ee2a8d31cf3b88787b62cda6899fe"
for ancestor in [claim, task["base_revision"]]:
    subprocess.run(["git", "merge-base", "--is-ancestor", ancestor, "HEAD"], cwd=root, check=True)
paths = set(git("diff", "--name-only", claim).splitlines())
paths.update(git("ls-files", "--others", "--exclude-standard").splitlines())
allowed = task["owns"] + ["docs/tasks/M7-corpus.json"]
for path in paths:
    assert any(path.startswith(p) if p.endswith("/") else path == p for p in allowed), path
subprocess.run(["git", "diff", "--check", claim], cwd=root, check=True)
print(json.dumps({"branch": task["branch"], "worktree": str(root), "claim": claim,
                  "revision": git("rev-parse", "HEAD"), "owned_changes": len(paths),
                  "paths": sorted(paths), "outside_ownership": []}, indent=2))
