"""Prepared allocation pair. --measure requires a durable coordinator grant.

Run --preflight under the same execution permissions before requesting a lease.
No result file is read by this supervisor.
"""
from pathlib import Path
import hashlib
import json
import os
import signal
import subprocess
import time

import sys
mode = sys.argv[1] if len(sys.argv) == 2 else None
if mode not in ("--preflight", "--measure"):
    raise SystemExit("Use --preflight or --measure")
manifest = json.loads(Path("docs/reports/M8-source-locations/preparation.json").read_text())

def identities():
    for name, digest in manifest["files"].items():
        if hashlib.sha256(Path(name).read_bytes()).hexdigest() != digest:
            raise RuntimeError(f"Prepared identity changed: {name}")

def members(group):
    rows = subprocess.run(["ps", "-axo", "pid=,pgid=,stat="], check=True,
                          text=True, capture_output=True, timeout=2).stdout.splitlines()
    return [row for row in rows if len(row.split()) >= 3
            and row.split()[1] == str(group) and not row.split()[2].startswith("Z")]

identities()
members(-1)  # Fail before a child starts if process inspection is unavailable.
if mode == "--preflight":
    print(json.dumps({"identities": len(manifest["files"]), "processInspection": "passed",
                      "measurementStarted": False}))
    raise SystemExit(0)

started = time.monotonic()
deadline = started + manifest["maximumSeconds"] - 5
records = []
error = None
try:
    for command in manifest["commands"]:
        env = dict(os.environ, **manifest["environment"],
                   XEMELE_SOURCE_REVISION=command["revision"], DOTNET_ROLL_FORWARD="Disable")
        process = None
        record = {"label": command["label"]}
        records.append(record)
        with open(f'artifacts/M8-source-locations/{command["label"]}-measurement.log', "wb") as log:
            try:
                process = subprocess.Popen(command["argv"], env=env, stdout=log,
                                           stderr=subprocess.STDOUT, start_new_session=True)
                record["pid"] = process.pid
                record["exitCode"] = process.wait(timeout=max(0.01, deadline-time.monotonic()))
            finally:
                if process is not None:
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.wait(timeout=2)
                    record["reaped"] = True
                    record["remainingGroup"] = members(process.pid)
        record["logClosed"] = True
        if record["remainingGroup"]:
            raise RuntimeError("Process group remains after cleanup")
        if record["exitCode"]:
            raise RuntimeError(f'{command["label"]} exited {record["exitCode"]}')
except BaseException as failure:
    error = f"{type(failure).__name__}: {failure}"
finally:
    try:
        identities()
        final_identity = "passed"
    except BaseException as failure:
        final_identity = str(failure)
        error = error or final_identity
    lifecycle = {"elapsedSeconds": time.monotonic()-started, "commands": records,
                 "finalIdentities": final_identity, "error": error,
                 "rawResultsInspected": False}
    Path("artifacts/M8-source-locations/lifecycle.json").write_text(json.dumps(lifecycle, indent=2)+"\n")
    print(json.dumps(lifecycle))
if error:
    raise SystemExit(1)
