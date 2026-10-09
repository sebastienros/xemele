"""Run M0 bootstrap checks in the current checkout; preserve a durable transcript."""
import json
from pathlib import Path
import shlex
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parents[3]
log = []


def run(args, expected=0):
    result = subprocess.run(args, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    log.append("$ " + shlex.join(args) + "\n" + result.stdout + f"Exit: {result.returncode}\n")
    print(log[-1], flush=True)
    if result.returncode != expected:
        raise RuntimeError(f"Expected exit {expected}: {args}")
    return result.stdout


try:
    revision = run(["git", "rev-parse", "HEAD"]).strip()
    assert run(["dotnet", "--version"]).strip() == "10.0.400"
    run(["dotnet", "restore", "Xemele.slnx"])
    run(["dotnet", "build", "Xemele.slnx", "-c", "Release", "--no-restore"])
    run(["dotnet", "test", "Xemele.slnx", "-c", "Release", "--no-build", "--list-tests"])
    run(["dotnet", "test", "Xemele.slnx", "-c", "Release", "--no-build"])
    for project in ["src/Xemele.Tool", "tests/Xemele.Fuzz", "benchmarks/Xemele.Benchmarks", "tools/CorpusManager"]:
        for arg in ["--help", "-h", "help", None]:
            command = ["dotnet", "run", "-c", "Release", "--no-build", "--project", project, "--"]
            output = run(command + ([arg] if arg else []))
            assert "Xemele" in output
    for project in ["src/Xemele.Tool", "tests/Xemele.Fuzz", "tools/CorpusManager"]:
        run(["dotnet", "run", "-c", "Release", "--no-build", "--project", project, "--", "unknown"], expected=2)
    run(["dotnet", "pack", "src/Xemele/Xemele.csproj", "-c", "Release", "--no-build", "-o", "artifacts/M0-build/packages"])
    package = root / "artifacts/M0-build/packages/Xemele.1.0.0.nupkg"
    with zipfile.ZipFile(package) as archive:
        library_files = [name for name in archive.namelist() if name.startswith("lib/")]
        assert library_files == ["lib/net10.0/Xemele.dll"], library_files
        assert not any(name.startswith("runtimes/") for name in archive.namelist())
        log.append("Package library files: " + json.dumps(library_files) + "; no native runtime assets.\n")
    count = run(["dnx", "dotnet-inspect", "-y", "--", "type", "--library", "src/Xemele/bin/Release/net10.0/Xemele.dll", "--count"])
    assert count.strip() == "0", count
    run(["python3", "tools/planning/tasks.py", "validate"])
    log.append(f"Passed bootstrap checks at {revision}. Test shells contain zero tests; no runtime APIs or benchmarks exist yet.\n")
finally:
    report = root / "artifacts/M0-build/verification.txt"
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text("\n".join(log))
