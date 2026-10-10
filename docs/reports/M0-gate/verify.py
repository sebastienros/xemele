#!/usr/bin/env python3
"""Reproduce the M0 checkpoint; explicit native provisioning, no new timings."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shlex
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
LOG = []


def run(args):
    args = list(map(str, args))
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT)
    entry = '$ ' + shlex.join(args) + '\n' + result.stdout + f'Exit: {result.returncode}\n'
    LOG.append(entry)
    print(entry, flush=True)
    if result.returncode:
        raise RuntimeError(f'Command failed: {shlex.join(args)}')
    return result.stdout


def copy_report(source, output, name):
    shutil.copyfile(ROOT / source, output / name)


def archive(output, revision):
    """Snapshot approved inputs; index historical evidence without changing it."""
    paths = ['LICENSE', 'THIRD-PARTY-NOTICES.md', 'global.json',
             'Directory.Build.props', 'Directory.Packages.props', 'Xemele.slnx',
             'tools/ReferenceRunners/Xmloxide/Cargo.lock',
             'tools/ReferenceRunners/Xmloxide/Cargo.toml',
             'tools/ReferenceRunners/Xmloxide/rust-toolchain.toml',
             'tools/ReferenceRunners/Libxml2/build.py',
             'tools/ApiBaseline/README.md', 'docs/specs/foundation.md']
    for directory in ['docs/decisions/M0', 'tests/Contracts/Fixtures/foundation',
                      'tests/corpora/bootstrap']:
        paths.extend(run(['git', 'ls-files', directory]).splitlines())
    history = []
    for directory in ['docs/reports/M0-tree-spike', 'docs/reports/M0-input-spike',
                      'docs/reports/M0-bench-shortrun', 'docs/reports/M0-references-libxml2',
                      'docs/reports/M0-references-xmloxide']:
        history.extend(run(['git', 'ls-files', directory]).splitlines())
    def entry(name):
        data = (ROOT / name).read_bytes()
        return dict(path=name, sha256=hashlib.sha256(data).hexdigest(), bytes=len(data),
                    blob=run(['git', 'rev-parse', f'{revision}:{name}']).strip())
    snapshots = []
    for name in sorted(set(paths)):
        item = entry(name)
        target = output / 'archive' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes((ROOT / name).read_bytes())
        snapshots.append(item)
    historical = [entry(name) for name in sorted(set(history))]
    # Historical timing data remains tied to its original revisions/job definitions.
    (output / 'checkpoint.json').write_text(json.dumps(dict(
        sourceRevision=revision, host=platform.platform(), architecture=platform.machine(),
        sdk='10.0.400', snapshots=snapshots, historicalEvidence=historical,
        limitations=['Infrastructure only; public runtime API is empty.',
                    'Hosted Windows/Linux/macOS CI unrun; native build is pinned macOS arm64.',
                    'No new benchmark timing. Archived synthetic timings are provisional.',
                    'Document whitespace loss is explicit unsupported failure.',
                    'Xmloxide external DTD/parameter entities/relative resource context unsupported.',
                    'Xmloxide literal CRLF attribute/CDATA values and libxml2 namespace IRI errors remain observed differences.',
                    'Reference gaps do not narrow Xemele requirements; expectations are independent.']), indent=2) + '\n')


def main(output, fetch):
    revision = run(['git', 'rev-parse', 'HEAD']).strip()
    LOG.append(f'Host: {platform.platform()}; architecture={platform.machine()}; source={revision}')
    assert run(['dotnet', '--version']).strip() == '10.0.400'
    assert not os.environ.get('XEMELE_BCL_RUNNER')
    assert not os.environ.get('XEMELE_MTP_SMOKE_FAIL')
    task = json.loads((ROOT / 'docs/tasks/M0-gate.json').read_text())
    dependencies = []
    for task_id in task['depends_on']:
        dependency = json.loads((ROOT / f'docs/tasks/{task_id}.json').read_text())
        assert dependency['status'] == 'verified', task_id
        run(['git', 'merge-base', '--is-ancestor', dependency['integration_revision'], revision])
        dependencies.append(dict(id=task_id, integrationRevision=dependency['integration_revision']))
    (output / 'dependencies.json').write_text(json.dumps(dependencies, indent=2) + '\n')
    # Remove generated managed outputs only, including all separate fixture/check projects.
    projects = run(['git', 'ls-files', '*.csproj']).splitlines()
    for project in projects:
        for directory in ['bin', 'obj']:
            path = (ROOT / project).parent / directory
            if path.exists():
                shutil.rmtree(path)
    LOG.append(f'Cleaned bin/obj for {len(projects)} tracked managed projects.')
    run([sys.executable, '-B', 'docs/reports/M0-build/verify.py'])
    copy_report('artifacts/M0-tests-mtp/bootstrap-verification.txt', output, 'bootstrap.txt')
    run([sys.executable, '-B', 'docs/reports/M0-api-wiring/verify.py'])
    copy_report('artifacts/M0-api-wiring/checks.txt', output, 'api-contracts.txt')
    copy_report('artifacts/M0-api-wiring/runtime-api.json', output, 'runtime-api.json')
    run([sys.executable, '-B', 'docs/reports/M0-corpora-wiring/check_cli.py'])
    copy_report('artifacts/M0-corpora-wiring/verification.txt', output, 'corpus-cli.txt')
    run([sys.executable, '-B', 'tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py'])
    run([sys.executable, '-B', 'tools/ApiBaseline/discover.py', '--bootstrap-empty-api'])
    # Native builds are explicit. Network fetch happens only with the opt-in flag.
    run([sys.executable, '-B', 'tools/ReferenceRunners/Libxml2/build.py'] + (['--fetch'] if fetch else []))
    run([sys.executable, '-B', 'tools/ReferenceRunners/Xmloxide/build.py', '--offline'])
    run(['dotnet', 'build', 'tools/ReferenceRunners/Integration/ReferenceIntegration.csproj', *FLAGS])
    run([sys.executable, '-B', 'docs/reports/M0-references/verify.py', '--output', output / 'references'])
    for experiment in ['Tree', 'Input']:
        run(['dotnet', 'build', f'experiments/{experiment}/{experiment}.csproj', *FLAGS])
    run(['dotnet', 'experiments/Tree/bin/Release/net10.0/Tree.dll', '--self-check'])
    tree = run(['dotnet', 'experiments/Tree/bin/Release/net10.0/Tree.dll', '--memory'])
    (output / 'tree-memory.json').write_text(tree)
    run(['dotnet', 'experiments/Input/bin/Release/net10.0/Input.dll', 'check'])
    allocations = run(['dotnet', 'experiments/Input/bin/Release/net10.0/Input.dll', 'allocations'])
    (output / 'input-allocations.txt').write_text(allocations)
    run([sys.executable, '-B', 'docs/reports/M0-input-spike/check_escape.py'])
    assert not run(['git', 'ls-files', 'src/Xemele/*.cs']).strip()
    archive(output, revision)
    run([sys.executable, '-B', 'tools/planning/tasks.py', 'validate'])
    run(['git', 'diff', '--check'])
    assert run(['git', 'rev-parse', 'HEAD']).strip() == revision
    LOG.append(f'PASS M0 foundation checkpoint at {revision}; no runtime parser/conformance or new timing claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'docs/reports/M0-gate/checkpoint')
    parser.add_argument('--fetch-libxml2', action='store_true', help='Explicitly fetch the pinned upstream commit')
    args = parser.parse_args()
    args.output = args.output.resolve()
    assert args.output.is_relative_to(ROOT / 'docs/reports/M0-gate') or args.output.is_relative_to(ROOT / 'artifacts')
    args.output.mkdir(parents=True, exist_ok=True)
    try:
        main(args.output, args.fetch_libxml2)
    finally:
        (args.output / 'verification.txt').write_text('\n'.join(LOG) + '\n')
