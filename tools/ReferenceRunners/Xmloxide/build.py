"""Pinned private reference build. Dependencies/caches stay outside tracked source."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CACHE = ROOT / 'artifacts/M0-references-xmloxide'
COMMIT = '555055f1ea6bf9944488fd96415b6f03b5b43696'

def run(args, **kw):
    return subprocess.check_output(args, cwd=HERE, text=True, **kw).strip()

if __name__ == '__main__':
    CACHE.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, CARGO_TARGET_DIR=str(CACHE / 'target'),
               XEMELE_BUILD_REVISION=run(['git', 'rev-parse', 'HEAD']))
    # Fixed release profile in Cargo.toml; refuse ambient compilation overrides.
    for key in list(env):
        if key in ('RUSTFLAGS', 'CARGO_ENCODED_RUSTFLAGS', 'RUSTC_WRAPPER', 'RUSTC_WORKSPACE_WRAPPER') or key.startswith('CARGO_PROFILE_'):
            raise SystemExit(f'Unset ambient build override {key}.')
    cmd = ['cargo', '+1.95.0', 'build', '--release', '--locked', '--manifest-path', str(HERE / 'Cargo.toml')]
    if '--offline' in sys.argv: cmd.append('--offline')
    subprocess.run(cmd, cwd=HERE, env=env, check=True)
    metadata_cmd = ['cargo', '+1.95.0', 'metadata', '--locked', '--format-version', '1']
    if '--offline' in sys.argv: metadata_cmd.append('--offline')
    metadata = json.loads(run(metadata_cmd, env=env))
    upstream = next(p for p in metadata['packages'] if p['name'] == 'xmloxide')
    source = Path(upstream['manifest_path']).parent
    assert upstream['source'].endswith('#' + COMMIT), upstream['source']
    assert run(['git', '-C', str(source), 'rev-parse', 'HEAD']) == COMMIT
    assert run(['git', '-C', str(source), 'status', '--porcelain']) in ('', '?? .cargo-ok')
    provenance = {
        'runnerRevision': env['XEMELE_BUILD_REVISION'], 'engineCommit': COMMIT,
        'engineTree': run(['git', '-C', str(source), 'rev-parse', 'HEAD^{tree}']),
        'source': upstream['source'], 'toolchain': run(['rustc', '+1.95.0', '-vV']),
        'cargo': run(['cargo', '+1.95.0', '--version']), 'command': cmd,
        'features': [], 'defaultFeatures': False,
        'lockSha256': hashlib.sha256((HERE / 'Cargo.lock').read_bytes()).hexdigest(),
        'upstreamLicenseSha256': hashlib.sha256((source / 'LICENSE').read_bytes()).hexdigest(),
        'dependencies': [{'name': p['name'], 'version': p['version'], 'source': p['source'], 'license': p['license']}
                         for p in metadata['packages']],
        'resolvedFeatures': [{'package': n['id'], 'features': n['features']} for n in metadata['resolve']['nodes']],
    }
    (CACHE / 'provenance.json').write_text(json.dumps(provenance, indent=2) + '\n')
    print(json.dumps(provenance, indent=2))
    print('Runner:', CACHE / 'target/release/xemele-xmloxide-reference')
