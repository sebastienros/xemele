"""Run clean bootstrap, workflow-equivalent checks, and actual test dependency inventory."""
import json
from pathlib import Path
import shlex
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / "artifacts/M0-tests-mtp/verification.txt"
LOG = []


def run(args):
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    entry = "$ " + shlex.join(args) + "\n" + result.stdout + f"Exit: {result.returncode}\n"
    LOG.append(entry)
    print(entry, flush=True)
    if result.returncode:
        raise RuntimeError(f"Command failed: {args}")
    return result.stdout


try:
    run(["git", "rev-parse", "HEAD"])
    run(["dotnet", "--info"])
    # Only generated output adjacent to actual projects in this isolated checkout.
    for project in ROOT.glob("**/*.csproj"):
        if "artifacts" not in project.relative_to(ROOT).parts:
            for directory in (project.parent / "bin", project.parent / "obj"):
                if directory.exists():
                    shutil.rmtree(directory)
    run([sys.executable, "-B", "-m", "unittest", "discover", "-s",
         "tests/Xemele.TestUtilities/Build", "-p", "test_*.py", "-v"])
    run([sys.executable, "-B", "tools/ApiBaseline/discover.py", "--bootstrap-empty-api"])
    # Includes clean restore, Release build, discovery/execution/failure checks,
    # bootstrap command behavior, net10.0 managed-only pack, API count, task validation.
    run([sys.executable, "-B", "docs/reports/M0-build/verify.py"])
    run(["dotnet", "pack", "src/Xemele/Xemele.csproj", "-c", "Release", "--no-build",
         "--no-restore", "--disable-build-servers", "-m:1", "-p:UseSharedCompilation=false",
         "-o", "artifacts/packages"])
    run(["dotnet", "list", "Xemele.slnx", "package", "--include-transitive", "--no-restore"])
    inventories = {}
    for name in ("Xemele.Tests", "Xemele.Conformance.Tests", "Xemele.Differential.Tests"):
        assets = json.loads((ROOT / f"tests/{name}/obj/project.assets.json").read_text())
        inventory = sorted(key for key, value in assets["libraries"].items() if value["type"] == "package")
        if "xunit.v3.mtp-v2/4.0.1" not in inventory:
            raise RuntimeError(f"Requested version missing: {name}")
        forbidden = {"xunit", "xunit.core", "xunit.assert", "xunit.runner.visualstudio",
                     "Microsoft.NET.Test.Sdk", "Microsoft.TestPlatform.TestHost",
                     "Microsoft.TestPlatform.ObjectModel"}
        if any(package.split("/")[0] in forbidden for package in inventory):
            raise RuntimeError(f"Superseded VSTest/xUnit v2 dependency: {name}")
        inventories[name] = inventory
    LOG.append("Actual test package inventories:\n" + json.dumps(inventories, indent=2) + "\n")
    print(LOG[-1], flush=True)
    run(["dnx", "dotnet-inspect", "-y", "--", "package", "xunit.v3.mtp-v2@4.0.1"])
    run(["dnx", "dotnet-inspect", "-y", "--", "package", "xunit.v3.core.mtp-v2@4.0.1", "-v:d"])
    run([sys.executable, "tools/planning/tasks.py", "validate"])
finally:
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text("\n".join(LOG))
    print(f"Verification transcript: {REPORT}", flush=True)
