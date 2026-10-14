"""Run before/after DOM BDN suites in separate default assembly contexts.

The supplied baseline DLL must be built and qualified at the intended revision.
Save it before building current source; this script records its hash and creates
an artifact-only project with a binary reference, never a source-project reference.
"""
import argparse, csv, hashlib, json, os, re, subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HOST = Path("/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet")
parser = argparse.ArgumentParser()
parser.add_argument("--before-dll", type=Path, required=True)
parser.add_argument("--out", type=Path, default=ROOT / "artifacts/M9-xml-performance")
args = parser.parse_args()
baseline = args.before_dll.resolve(strict=True)
out = args.out.resolve()
suffix = hashlib.sha256((str(out) + str(baseline)).encode()).hexdigest()[:12]
project_name = "BeforeRunner" + suffix
project = out / ("before-" + suffix)
project.mkdir(parents=True, exist_ok=True)
original = ROOT / "docs/reports/M9-xml-performance/Runner"
(project / "Program.cs").write_bytes((original / "Program.cs").read_bytes())
source = (original / "XmlPerformanceRunner.csproj").read_text()
source = re.sub(r'<ProjectReference Include="[^"]+"/>',
    '<Reference Include="Xemele"><HintPath>' + str(baseline) + '</HintPath></Reference>', source)
source = re.sub(r'Compile Include="([^"]+)"',
    lambda m: 'Compile Include="' + str((original / m.group(1)).resolve()) + '"', source)
(project / (project_name + ".csproj")).write_text(source)
env = dict(os.environ, DOTNET_ROOT=str(HOST.parent),
    PATH=str(HOST.parent) + ":" + os.environ["PATH"], DOTNET_TieredCompilation="0",
    DOTNET_TieredPGO="0", DOTNET_ReadyToRun="0", XEMELE_PERF_ROOT=str(ROOT))

def run(command, log):
    with log.open("w") as output:
        subprocess.run([str(v) for v in command], cwd=ROOT, env=env,
            stdout=output, stderr=subprocess.STDOUT, check=True)

build = [HOST, "build", "-c", "Release", "--disable-build-servers", "-m:1",
    "-p:UseSharedCompilation=false", "-p:RuntimeFrameworkVersion=10.0.11",
    "-p:NuGetAudit=false", "--ignore-failed-sources"]
run(build + [project / (project_name + ".csproj")], out / "before-affine-runner-build.log")
run(build + [original / "XmlPerformanceRunner.csproj"], out / "after-affine-runner-build.log")
(out / "default-context-metadata.json").write_text(json.dumps({
    "beforeDll": str(baseline), "beforeSha256": hashlib.sha256(baseline.read_bytes()).hexdigest(),
    "revision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
    "settings": {k: env[k] for k in ("DOTNET_TieredCompilation", "DOTNET_TieredPGO", "DOTNET_ReadyToRun")}
}, indent=2))
for label, runner in [("affine-before-default", project / ("bin/Release/net10.0/" + project_name + ".dll")),
        ("affine-after-default", original / "bin/Release/net10.0/XmlPerformanceRunner.dll")]:
    run([HOST, "--fx-version", "10.0.11", runner, "bdn", "--iterationTime", "250",
        "--artifacts", out / label], out / (label + ".log"))
    result = out / label / "results/DomComparison-report.csv"
    with result.open() as file:
        cases = list(csv.DictReader(file))
    if len(cases) != 20 or any(not case.get("Mean") or case["Mean"] == "NA" for case in cases):
        raise RuntimeError("Incomplete benchmark suite: " + label)
    print("PASS", label, "20 measured cases", flush=True)
