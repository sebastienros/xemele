#!/usr/bin/env python3
"""Check additive selector signatures against actual verified models and task ownership."""
import argparse
import json
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--dotnet", required=True)
parser.add_argument("--ownership-base", required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]

def run(command):
    result = subprocess.run(command, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    assert result.returncode == 0, result.stdout
    return result.stdout

assert run([args.dotnet, "--version"]).strip() == "10.0.400"
assert "Microsoft.NETCore.App 10.0.11 " in run([args.dotnet, "--list-runtimes"])
project = here / "ActualModels/ActualModels.csproj"
assert "Model.cs.fixture" not in project.read_text()
assert "<IsPackable>false</IsPackable>" in project.read_text()
run([args.dotnet, "restore", str(project), "--source", str(root / "tests/Contracts/Fixtures/M7"), "-p:NuGetAudit=false", "--verbosity", "quiet"])
print(run([args.dotnet, "build", str(project), "-c", "Release", "--no-restore", "-p:RuntimeFrameworkVersion=10.0.11", "-p:UseSharedCompilation=false", "--disable-build-servers", "--verbosity", "quiet"]).strip())
output = run([args.dotnet, "--fx-version", "10.0.11", str(project.parent / "bin/Release/net10.0/ActualModels.dll")])
assert "actual HTML and XML model assembly identity" in output
assert "runtime 10.0.11" in output
print(output.strip())

def rows(content):
    return {row["id"]: row for row in map(json.loads, content.splitlines()[1:])}

selector = "tests/Contracts/Fixtures/M7/Selector.golden.txt"
before = rows(run(["git", "show", args.ownership_base + ":" + selector]))
after = rows((root / selector).read_text())
assert set(after) - set(before) == {"M:Xemele.Css.CssSelector.Matches(Xemele.XmlNode)", "M:Xemele.Css.CssSelector.Select(Xemele.XmlNode)"}
assert all(after[key] == row for key, row in before.items())
for family in ["Engine", "Tokenizer"]:
    path = "tests/Contracts/Fixtures/M7/" + family + ".golden.txt"
    assert (root / path).read_text() == run(["git", "show", args.ownership_base + ":" + path])
print("PASS: exactly two additive Selector members; all previous Selector/Engine/Tokenizer declarations unchanged")

task = json.loads((root / "docs/tasks/M7-css-xml-contract.json").read_text())
def owned(path):
    return path == "docs/tasks/M7-css-xml-contract.json" or any(path.startswith(entry) if entry.endswith("/") else path == entry for entry in task["owns"])
for command in [["git", "diff", "--name-only", args.ownership_base], ["git", "ls-files", "--others", "--exclude-standard"]]:
    paths = run(command).splitlines()
    assert all(owned(path) for path in paths), paths
run(["git", "diff", "--check", args.ownership_base])
print("PASS: strict task-owned claim-to-current diff including task JSON/report; diff whitespace check")
