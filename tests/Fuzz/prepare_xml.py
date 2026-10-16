#!/usr/bin/env python3
"""Prepare the disposable local bundle used by local_xml.py.

This builds, instruments and hashes the XML driver, the multi-target driver and the
crash-positive control, and builds the pinned AFL engine. It never starts a fuzzing process and never touches the
production build output: instrumentation is confined to copies under artifacts/.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
CAMPAIGN = ROOT / "tests/Fuzz"
BUNDLE = ROOT / "artifacts/M9-robustness/manual-xml"
RUNTIME = "10.0.11"
# XML uses XmlDriver; every other target is served by TargetDriver (see its Program.cs).
TARGETS = ["xml", "html5", "html-legacy", "stream", "xpath", "xsd", "rng", "schematron", "xinclude", "serialize", "writer"]
AFL_REVISION = "61037103ae3722c8060ff7082994836a794f978e"
AFL_SHA256 = "7c6a30d26a1362aa04899915e5a642d1f5f8dec7cad1e5d33debaefe0d57933c"
AFL_URL = "https://codeload.github.com/google/AFL/tar.gz/" + AFL_REVISION
DOTNETUP = Path.home() / "Library/Application Support/dotnetup/dotnet"
DOTNET_ROOT = Path(os.environ.get("XEMELE_DOTNET_ROOT", DOTNETUP))
DOTNET = DOTNET_ROOT / "dotnet"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(argv, cwd=ROOT, **kwargs):
    print("+", " ".join(str(a) for a in argv), flush=True)
    subprocess.run([str(a) for a in argv], cwd=cwd, check=True, **kwargs)


def build(project, output):
    run([DOTNET, "build", project, "-c", "Release", "--disable-build-servers", "-m:1",
         "-p:UseSharedCompilation=false", "-p:NuGetAudit=false", "-o", output])


def instrument(directory):
    # The instrumenter prints every instrumented type name; keep only its summary.
    print("+ instrument", directory / "Xemele.dll", flush=True)
    result = subprocess.run([str(DOTNET), "--fx-version", RUNTIME,
                             str(BUNDLE / "build/instrumenter/Instrumenter.dll"),
                             str(directory / "Xemele.dll")],
                            cwd=ROOT, check=True, capture_output=True, text=True)
    summary = json.loads(result.stdout)
    print("  runtime", summary["runtime"], "instrumented types", summary["types"], flush=True)


def wrapper(name, directory, assembly, *extra):
    path = BUNDLE / name
    path.write_text('#!/bin/sh\nexec "%s" "%s"%s\n' % (DOTNET, directory / assembly, "".join(" " + e for e in extra)))
    path.chmod(0o755)


def build_engine():
    work = BUNDLE / "afl-source"
    work.mkdir()
    archive = work / "afl.tar.gz"
    print("Downloading", AFL_URL, flush=True)
    urllib.request.urlretrieve(AFL_URL, archive)
    if digest(archive) != AFL_SHA256:
        raise ValueError("The downloaded AFL archive hash differs from the pinned value.")
    with tarfile.open(archive) as tar:
        tar.extractall(work, filter="data")
    source = work / ("AFL-" + AFL_REVISION)
    # AFL's x86 assembly instrumentation is irrelevant: SharpFuzz instruments managed code.
    run(["make", "AFL_NO_X86=1", "afl-fuzz"], cwd=source)
    shutil.copy2(source / "afl-fuzz", BUNDLE / "afl-fuzz")


# Everything a previous preparation generated and that depends on the source or the seeds.
# Campaign output (campaign*), session records, control runs and the AFL engine are kept.
GENERATED = ["driver-xml", "driver-targets", "driver-control", "seeds", "control-seeds", "build",
             "afl-source", "manifest.json", "qualification.json"] + ["run-" + t for t in TARGETS] + ["run-crash-control"]


def refresh():
    BUNDLE.mkdir(parents=True, exist_ok=True)
    busy = subprocess.run(["pgrep", "-f", str(BUNDLE / "afl-fuzz")], capture_output=True, text=True)
    if busy.returncode == 0:
        raise ValueError("A campaign from this bundle is still running; stop it before preparing again.")
    for name in GENERATED:
        path = BUNDLE / name
        if path.is_dir():
            shutil.rmtree(path)
        elif path.exists():
            path.unlink()


def main():
    if not DOTNET.is_file():
        raise ValueError("Pinned .NET SDK not found: " + str(DOTNET))
    refresh()
    try:
        build(CAMPAIGN / "Instrumenter/Instrumenter.csproj", BUNDLE / "build/instrumenter")
        build(CAMPAIGN / "XmlDriver/XmlDriver.csproj", BUNDLE / "driver-xml")
        build(CAMPAIGN / "TargetDriver/TargetDriver.csproj", BUNDLE / "driver-targets")
        build(CAMPAIGN / "CrashControl/CrashControl.csproj", BUNDLE / "driver-control")
        production = digest(ROOT / "src/Xemele/bin/Release/net10.0/Xemele.dll") \
            if (ROOT / "src/Xemele/bin/Release/net10.0/Xemele.dll").exists() else None
        for directory in ("driver-xml", "driver-targets", "driver-control"):
            instrument(BUNDLE / directory)
        wrapper("run-xml", BUNDLE / "driver-xml", "XmlDriver.dll")
        for target in TARGETS[1:]:
            wrapper("run-" + target, BUNDLE / "driver-targets", "TargetDriver.dll", target)
        wrapper("run-crash-control", BUNDLE / "driver-control", "CrashControl.dll")
        for target in TARGETS:
            source = CAMPAIGN / "seeds" / target
            if not any(source.iterdir()):
                raise ValueError("No seeds for target " + target)
            shutil.copytree(source, BUNDLE / "seeds" / target)
        (BUNDLE / "control-seeds").mkdir()
        (BUNDLE / "control-seeds/seed.xml").write_bytes(b"<r/>")
        if not (BUNDLE / "afl-fuzz").exists():
            build_engine()
        if (BUNDLE / "afl-source").exists():
            shutil.rmtree(BUNDLE / "afl-source")
        shutil.rmtree(BUNDLE / "build")

        files = {}
        for name in ["afl-fuzz", "run-crash-control"] + ["run-" + t for t in TARGETS]:
            files[name] = digest(BUNDLE / name)
        for directory in ("driver-xml", "driver-targets", "driver-control", "seeds", "control-seeds"):
            for path in sorted((BUNDLE / directory).rglob("*")):
                if path.is_file():
                    files[path.relative_to(BUNDLE).as_posix()] = digest(path)
        framework = DOTNET_ROOT / "shared/Microsoft.NETCore.App" / RUNTIME
        runtime_files = {}
        for path in (DOTNET, framework / "libcoreclr.dylib", framework / "System.Private.CoreLib.dll"):
            runtime_files[str(path)] = digest(path)
        manifest = {
            "scope": "One campaign per target: " + ", ".join(TARGETS) + ". The full M9 robustness gate "
                     "also needs the evidence report and the other gates in docs/testing.md.",
            "targets": TARGETS,
            "engine": "afl-fuzz", "aflRevision": AFL_REVISION, "runtime": RUNTIME,
            "productionXemeleSha256": production,
            "files": files, "runtimeFiles": runtime_files,
        }
        (BUNDLE / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    except BaseException:
        print("Preparation failed; the partial bundle is retained for inspection: " + str(BUNDLE), file=sys.stderr)
        raise
    print("Prepared", BUNDLE)
    print("Next: python3 tests/Fuzz/local_xml.py qualify")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
