"""Run the actual managed client against privately built references; no provisioning."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=ROOT / 'docs/reports/M0-references')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
dotnet = shutil.which('dotnet')
assert dotnet, 'Pinned dotnet must be on PATH'
assert subprocess.check_output([dotnet, '--version'], cwd=ROOT, text=True).strip() == '10.0.400'
libxml = ROOT / 'artifacts/M0-references-libxml2/native-build'
rust = ROOT / 'artifacts/M0-references-xmloxide'
for path in [libxml / 'libxemele_libxml2.dylib', libxml / 'provenance.json',
             rust / 'target/release/xemele-xmloxide-reference', rust / 'provenance.json']:
    assert path.is_file(), f'Explicit native provisioning required: {path}'
provenance = json.loads((rust / 'provenance.json').read_text())
assert provenance['runnerRevision'] == revision, 'Rebuild Rust runner at the tested source revision'
assert provenance['engineCommit'] == '555055f1ea6bf9944488fd96415b6f03b5b43696'
assert provenance['lockSha256'] == hashlib.sha256((ROOT / 'tools/ReferenceRunners/Xmloxide/Cargo.lock').read_bytes()).hexdigest()
for source, target in [(libxml / 'provenance.json', 'libxml2-provenance.json'), (rust / 'provenance.json', 'xmloxide-provenance.json')]:
    shutil.copyfile(source, args.output / target)
command = [dotnet, str(ROOT / 'tools/ReferenceRunners/Integration/bin/Release/net10.0/ReferenceIntegration.dll'),
           str(ROOT), dotnet, str(args.output.resolve()), revision]
result = subprocess.run(command, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                        stderr=subprocess.STDOUT, timeout=180)
(args.output / 'verification.txt').write_text('$ ' + repr(command) + '\n' + result.stdout + f'Exit: {result.returncode}\n')
print(result.stdout, end='')
raise SystemExit(result.returncode)
