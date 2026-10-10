"""Discover and execute MTP tests, reporting infrastructure separately from product tests."""
from pathlib import Path
import shlex
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
PROJECTS = ("Xemele.Tests", "Xemele.Conformance.Tests", "Xemele.Differential.Tests")
METHOD = "RunnerExecutesAndPropagatesControlledFailure"
REPORT_ROOT = ROOT / "artifacts/test-results"


def main():
    REPORT_ROOT.mkdir(parents=True, exist_ok=True)
    results = Path(tempfile.mkdtemp(prefix="mtp-", dir=REPORT_ROOT))
    log = []

    def run(args, expected=0):
        result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT)
        entry = "$ " + shlex.join(args) + "\n" + result.stdout + f"Exit: {result.returncode}\n"
        log.append(entry)
        print(entry, flush=True)
        if result.returncode != expected:
            raise RuntimeError(f"Expected exit {expected}, got {result.returncode}: {args}")
        return result.stdout

    def command(selector, path):
        # MTP native CLI has no VSTest logger or MSBuild switch forwarding.
        return ["dotnet", "test", selector, path, "-c", "Release", "--no-build",
                "--no-restore", "--no-ansi", "--no-progress"]

    def tests(directory):
        return [test for report in directory.glob("*.xunit.xml")
                for test in ET.parse(report).iter("test")]

    try:
        run(["git", "rev-parse", "HEAD"])
        for project in PROJECTS:
            output = run(command("--project", f"tests/{project}/{project}.csproj") + ["--list-tests"])
            if f"{project}.Infrastructure.MtpRunnerSmokeTests.{METHOD}" not in output:
                raise RuntimeError(f"Smoke test missing from discovery: {project}")
        success = results / "success"
        run(command("--solution", "Xemele.slnx") + ["--minimum-expected-tests", "1",
            "--report-xunit-trx", "--report-xunit-xml", "--results-directory", str(success)])
        executed = tests(success)
        for project in PROJECTS:
            smoke = [test for test in executed if test.get("name") ==
                     f"{project}.Infrastructure.MtpRunnerSmokeTests.{METHOD}"]
            if len(smoke) != 1 or smoke[0].get("result") != "Pass":
                raise RuntimeError(f"Smoke test not executed successfully: {project}")
        infrastructure = [test for test in executed if any(
            trait.get("name") == "Category" and trait.get("value") == "Infrastructure"
            for trait in test.iter("trait"))]
        summary = (f"Infrastructure tests: {len(infrastructure)}; "
                   f"product tests: {len(executed) - len(infrastructure)}. "
                   "Infrastructure success is not product conformance evidence.\n")
        log.append(summary)
        print(summary, flush=True)
        for project in PROJECTS:
            failure = results / f"controlled-failure-{project}"
            run(command("--project", f"tests/{project}/{project}.csproj") + [
                "--filter-class", f"{project}.Infrastructure.MtpRunnerSmokeTests",
                "--environment", "XEMELE_MTP_SMOKE_FAIL=1", "--report-xunit-xml",
                "--results-directory", str(failure)], expected=2)
            failed = tests(failure)
            if len(failed) != 1 or failed[0].get("result") != "Fail" or \
                    "Intentional infrastructure failure" not in "".join(failed[0].itertext()):
                raise RuntimeError(f"Expected controlled assertion failure absent: {project}")
        log.append("All three controlled assertion failures propagated exit 2.\n")
    finally:
        (results / "runner-verification.txt").write_text("\n".join(log))
        print(f"Runner transcript: {results / 'runner-verification.txt'}", flush=True)


if __name__ == "__main__":
    main()
