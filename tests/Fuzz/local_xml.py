#!/usr/bin/env python3
"""User-invoked local fuzz campaigns (one per target); no campaign starts during preparation."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import signal
import subprocess
import sys
import time
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
BUNDLE = ROOT / "artifacts/M9-robustness/manual-xml"
REQUIRED_HOURS = 24  # docs/testing.md: at least 24 hours per major parser/validator target


def manifest_targets():
    path = BUNDLE / "manifest.json"
    return json.loads(path.read_text())["targets"] if path.exists() else ["xml"]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def utc():
    return datetime.now(timezone.utc).isoformat()


def save(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n")
    temporary.replace(path)


def stats(output):
    path = output / "fuzzer_stats"
    if not path.exists():
        return {}
    result = {}
    for line in path.read_text().splitlines():
        if ":" in line:
            key, value = line.split(":", 1)
            result[key.strip()] = value.strip()
    return result


def verify():
    path = BUNDLE / "manifest.json"
    if not path.exists():
        raise ValueError("The prepared local bundle is missing: " + str(path))
    manifest = json.loads(path.read_text())
    for name, expected in manifest["files"].items():
        candidate = BUNDLE / name
        if not candidate.is_file() or digest(candidate) != expected:
            raise ValueError("Prepared input changed or is missing: " + str(candidate))
    for name, expected in manifest["runtimeFiles"].items():
        if not Path(name).is_file() or digest(Path(name)) != expected:
            raise ValueError("Pinned runtime changed or is missing: " + name)
    return manifest, digest(path)


def crash_files(output):
    return sorted(p for p in (output / "crashes").glob("id:*") if p.is_file())


def stop_group(child):
    for sig, wait in [(signal.SIGINT, 5), (signal.SIGTERM, 2), (signal.SIGKILL, 2)]:
        try:
            os.killpg(child.pid, sig)
        except ProcessLookupError:
            pass
        try:
            child.wait(timeout=wait)
            # Terminate any surviving owned descendants as well.
            try:
                os.killpg(child.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            return
        except subprocess.TimeoutExpired:
            pass
    raise RuntimeError("The AFL process did not stop within the cleanup bound.")


def execute(manifest, binding, output, seeds, target, seconds, control=False):
    session = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ")
    logs = BUNDLE / "sessions"
    logs.mkdir(exist_ok=True)
    log_path = logs / (session + ".log")
    report_path = logs / (session + ".json")
    argv = [str(BUNDLE / manifest["engine"]), "-i", str(seeds), "-o", str(output),
            "-m", "none", "-t", "10000", "--", str(BUNDLE / target)]
    environment = dict(os.environ)
    # Managed targets use a wrapper and report crashes through SharpFuzz's protocol.
    # These compatibility options affect this child only, never system settings.
    for key in ("AFL_BENCH_JUST_ONE", "AFL_EXIT_WHEN_DONE", "AFL_SKIP_CRASHES",
                "AFL_NO_FORKSRV", "AFL_DUMB_FORKSRV", "__AFL_SHM_ID",
                "SHARPFUZZ_SHARED_MEMORY", "SHARPFUZZ_PIPE"):
        environment.pop(key, None)
    environment.update(AFL_SKIP_BIN_CHECK="1", AFL_NO_UI="1",
                       AFL_I_DONT_CARE_ABOUT_MISSING_CRASHES="1",
                       XEMELE_FUZZ_ENGINE="afl")
    before = stats(output)
    if before.get("fuzzer_pid"):
        try:
            os.kill(int(before["fuzzer_pid"]), 0)
        except ProcessLookupError:
            pass
        else:
            raise ValueError("The recorded AFL PID is still alive; stop it before resuming.")
    report = {"manifestSha256": binding, "argv": argv, "startUtc": utc(),
              "requestedSeconds": seconds, "control": control, "statsBefore": before,
              "log": str(log_path), "scope": manifest["scope"]}
    child = None
    start = time.monotonic()
    reason = "engine-exit"
    old_handlers = {}

    def interrupted(signum, frame):
        raise KeyboardInterrupt

    print("Log:", log_path, flush=True)
    print("Stats:", output / "fuzzer_stats", flush=True)
    print("Stop with Ctrl+C. Queue, crashes, hangs and session logs are retained.", flush=True)
    try:
        for sig in (signal.SIGTERM, signal.SIGHUP):
            old_handlers[sig] = signal.signal(sig, interrupted)
        with log_path.open("xb") as log:
            child = subprocess.Popen(argv, cwd=BUNDLE, env=environment, stdout=log,
                                     stderr=subprocess.STDOUT, start_new_session=True)
            report["pid"] = child.pid
            try:
                while child.poll() is None:
                    if control and any(p.read_bytes()[:1] != b"<" for p in crash_files(output)):
                        reason = "control-crash-captured"
                        break
                    if time.monotonic() - start >= seconds:
                        reason = "time-limit"
                        break
                    time.sleep(1)
            except KeyboardInterrupt:
                reason = "user-stop"
            finally:
                stop_group(child)
    finally:
        for sig, handler in old_handlers.items():
            signal.signal(sig, handler)
        report.update(endUtc=utc(), wallSeconds=time.monotonic() - start, reason=reason,
                      exitCode=None if child is None else child.returncode,
                      directChildReaped=child is not None and child.returncode is not None,
                      statsAfter=stats(output), output=str(output))
        save(report_path, report)
        print("Session record:", report_path, flush=True)
    return report


def target_output(target):
    # The original XML campaign keeps its historical folder name.
    return BUNDLE / ("campaign" if target == "xml" else "campaign-" + target)


def recorded_hours(target):
    """Wall-clock hours of finished sessions for one target, from the retained session records."""
    seconds = 0.0
    for path in (BUNDLE / "sessions").glob("*.json"):
        record = json.loads(path.read_text())
        if not record.get("control") and Path(record["argv"][-1]).name == "run-" + target:
            seconds += record.get("wallSeconds", 0.0)
    return seconds / 3600


def summary(target):
    current = stats(target_output(target))
    hours = recorded_hours(target)
    return {"target": target, "hours": round(hours, 2), "meets24h": hours >= REQUIRED_HOURS,
            "execs": current.get("execs_done"), "paths": current.get("paths_total"),
            "crashes": current.get("unique_crashes"), "hangs": current.get("unique_hangs")}


def run_all(command, targets, hours, jobs):
    """Runs one child of this script per target, at most `jobs` at a time."""
    pending = list(targets)
    running = {}
    failed = []
    try:
        while pending or running:
            while pending and len(running) < jobs:
                target = pending.pop(0)
                if command == "resume" and not (target_output(target) / "fuzzer_stats").exists():
                    command_for = "run"
                elif command == "run" and target_output(target).exists():
                    command_for = "resume"
                else:
                    command_for = command
                argv = [sys.executable, str(Path(__file__).resolve()), command_for, "--target", target, "--hours", str(hours)]
                print("Starting", command_for, target, flush=True)
                running[target] = subprocess.Popen(argv)
            for target, child in list(running.items()):
                if child.poll() is not None:
                    del running[target]
                    print("Finished", target, "exit", child.returncode, flush=True)
                    if child.returncode != 0:
                        failed.append(target)
            time.sleep(1)
    except KeyboardInterrupt:
        # The terminal already sent SIGINT to every child; wait for them to retain their findings.
        for child in running.values():
            child.wait()
    return 1 if failed else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("qualify", help="Run the separate crash-positive control for at most 90 seconds")
    choices = ["all"] + manifest_targets()
    for name in ("run", "resume"):
        p = sub.add_parser(name, help="Start a bounded campaign" if name == "run" else "Resume the retained queue")
        p.add_argument("--target", choices=choices, default="xml", help="Fuzz target, or all of them (default: xml)")
        p.add_argument("--hours", type=float, required=True, help="Additional wall-clock limit, greater than zero and at most 24")
        p.add_argument("--jobs", type=int, default=0, help="With --target all: parallel campaigns (default: CPUs - 2)")
    p = sub.add_parser("status", help="Display the retained AFL statistics without starting a process")
    p.add_argument("--target", choices=choices, default="all")
    args = parser.parse_args()
    manifest, binding = verify()
    if args.command == "status":
        targets = manifest["targets"] if args.target == "all" else [args.target]
        rows = [summary(t) for t in targets]
        print(json.dumps({"requiredHours": REQUIRED_HOURS, "targets": rows}, indent=2))
        done = all(r["meets24h"] for r in rows)
        print("Duration gate:", "all selected targets reached %g h of recorded wall-clock time" % REQUIRED_HOURS if done
              else "NOT reached for: " + ", ".join(r["target"] for r in rows if not r["meets24h"]))
        return 0
    if args.command == "qualify":
        control_output = BUNDLE / ("control-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ"))
        report = execute(manifest, binding, control_output, BUNDLE / "control-seeds", "run-crash-control", 90, True)
        found = [p for p in crash_files(control_output) if p.read_bytes()[:1] != b"<"]
        passed = bool(found) and int(report["statsAfter"].get("unique_crashes", "0")) > 0
        qualification = {"manifestSha256": binding, "passed": passed, "utc": utc(),
                         "session": report, "crashes": [{"path": str(p), "sha256": digest(p)} for p in found]}
        save(BUNDLE / "qualification.json", qualification)
        print("Crash-positive qualification:", "PASS" if passed else "FAIL; inspect the log before running a campaign")
        return 0 if passed else 1
    if not math.isfinite(args.hours) or not 0 < args.hours <= 24:
        raise ValueError("--hours must be greater than zero and at most 24")
    qualification = BUNDLE / "qualification.json"
    if not qualification.exists():
        raise ValueError("Run qualify first. Crash detection has not yet been demonstrated.")
    gate = json.loads(qualification.read_text())
    if not gate.get("passed") or gate.get("manifestSha256") != binding:
        raise ValueError("Crash-positive qualification must pass for this exact prepared bundle.")
    if args.target == "all":
        jobs = args.jobs or max(1, (os.cpu_count() or 2) - 2)
        return run_all(args.command, manifest["targets"], args.hours, jobs)
    output = target_output(args.target)
    if args.command == "run" and output.exists():
        raise ValueError("A retained campaign already exists. Use resume to preserve its findings.")
    if args.command == "resume" and not (output / "fuzzer_stats").exists():
        raise ValueError("There is no campaign to resume. Use run first.")
    seeds = "-" if args.command == "resume" else BUNDLE / "seeds" / args.target
    report = execute(manifest, binding, output, seeds, "run-" + args.target, args.hours * 3600)
    print("This session covers only the", args.target, "target; the full M9 robustness gate also needs every other target"
          " and the evidence report. See `status`.")
    return 0 if report["reason"] in ("time-limit", "user-stop") and int(report["statsAfter"].get("execs_done", "0")) > 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, RuntimeError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
