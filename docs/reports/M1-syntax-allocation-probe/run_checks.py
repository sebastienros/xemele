"""Bounded, sequential source-revision checks; every invocation retains its output."""
import gzip
import json
import os
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[3]
report = Path(__file__).resolve().parent
env = dict(os.environ)
env["DOTNET_ROOT"] = "/Users/sebastienros/Library/Application Support/dotnetup/dotnet"
env["PATH"] = env["DOTNET_ROOT"] + os.pathsep + env["PATH"]
env["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
commands = {
    "build": ["dotnet", "build", "Xemele.slnx", "-c", "Release", "--disable-build-servers", "-m:1", "-p:UseSharedCompilation=false", "-p:NuGetAudit=false"],
    "focused": ["dotnet", "test", "--project", "tests/Xemele.Tests", "-c", "Release", "--no-build", "--no-restore", "--filter-method", "*WarmRepeatedTagsAndStreamingUnicodeDispatchAllocateZero*"],
    "full": ["dotnet", "test", "--solution", "Xemele.slnx", "-c", "Release", "--no-build", "--no-restore"],
    "diagnostic-build": ["dotnet", "build", "docs/reports/M1-syntax-allocation-probe/Diagnostic", "-c", "Release", "--disable-build-servers", "-m:1", "-p:UseSharedCompilation=false", "-p:NuGetAudit=false"],
    "diagnostic-original": ["dotnet", "docs/reports/M1-syntax-allocation-probe/Diagnostic/bin/Release/net10.0/Xemele.Tests.dll", "original"],
    "diagnostic-helper": ["dotnet", "docs/reports/M1-syntax-allocation-probe/Diagnostic/bin/Release/net10.0/Xemele.Tests.dll", "helper"],
    "api-build": ["dotnet", "build", "docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj", "-c", "Release", "--disable-build-servers", "-m:1", "-p:UseSharedCompilation=false", "-p:NuGetAudit=false"],
    "api": ["python3", "-B", "tools/ApiBaseline/compare.py", "--report", "docs/reports/M1-syntax-allocation-probe/runtime-api.json"],
    "provision": ["python3", "-B", "docs/reports/M1-corpus-import/provision.py"],
    "discovery": ["python3", "-B", "tools/ApiBaseline/discover.py"],
    "planning": ["python3", "-B", "tools/planning/tasks.py", "validate"],
    "regressions": ["python3", "-B", "-m", "unittest", "discover", "-s", "tools/planning", "-p", "test_*.py", "-v"],
    "runtime": ["dotnet", "--info"],
}
commands["diagnostic-build-fixed"] = commands["diagnostic-build"]
commands["api-provisioned"] = commands["api"]
for name in sys.argv[1:]:
    destination = report / (name + ".txt.gz")
    if destination.exists():
        raise SystemExit("Refusing to overwrite retained check: " + name)
    result = subprocess.run(commands[name], cwd=root, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    output = "revision=" + revision + "\ncommand=" + repr(commands[name]) + "\nexit=" + str(result.returncode) + "\n" + result.stdout
    with gzip.open(destination, "wt") as stream:
        stream.write(output)
    print(name + ": exit=" + str(result.returncode), flush=True)
    print(result.stdout[-5000:], flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)
