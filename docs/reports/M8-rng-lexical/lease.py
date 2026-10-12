"""Run exactly the two frozen prebuilt children only with a fresh coordinator grant."""
import argparse, datetime, hashlib, json, os, signal, subprocess, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
MANIFEST = HERE / 'preparation.json'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def utc(text): return datetime.datetime.fromisoformat(text.replace('Z', '+00:00'))
def now(): return datetime.datetime.now(datetime.timezone.utc)

parser = argparse.ArgumentParser()
parser.add_argument('--lease', required=True)
parser.add_argument('--manifest-sha256', required=True)
parser.add_argument('--latest-start', required=True)
parser.add_argument('--hard-stop', required=True)
args = parser.parse_args()
if sha(MANIFEST) != args.manifest_sha256: raise RuntimeError('Manifest changed')
plan = json.loads(MANIFEST.read_text())
if args.lease != plan['lease_name']: raise RuntimeError('Wrong grant')
if now() > utc(args.latest_start): raise RuntimeError('Latest start passed')
for path, digest in plan['inputs'].items():
    if sha(ROOT / path) != digest: raise RuntimeError('Frozen input changed: ' + path)
if sha(Path(__file__)) != plan['supervisor_sha256']: raise RuntimeError('Supervisor changed')
work_stop = min(time.monotonic() + plan['maximum_seconds'] - plan['cleanup_seconds'],
                time.monotonic() + (utc(args.hard_stop) - now()).total_seconds() - plan['cleanup_seconds'])
if work_stop <= time.monotonic(): raise RuntimeError('No work interval')
out = ROOT / 'artifacts/M8-rng-lexical/lease'
out.mkdir(parents=True, exist_ok=True)
children = []
current = None
failure = None
started = now().isoformat()
try:
    for child in plan['children']:
        remaining = work_stop - time.monotonic()
        if remaining <= 0: raise TimeoutError('Work interval exhausted')
        with (out / (child['name'] + '.log')).open('wb') as log:
            current = subprocess.Popen(child['command'], cwd=ROOT, env=os.environ | plan['environment'],
                                       stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            children.append({'name': child['name'], 'pid': current.pid})
            try:
                code = current.wait(timeout=remaining)
            except BaseException:
                os.killpg(current.pid, signal.SIGKILL)
                current.wait(timeout=plan['cleanup_seconds'])
                raise
            children[-1]['exit_code'] = code
            # No child may leave a live descendant group behind.
            try: os.killpg(current.pid, 0)
            except ProcessLookupError: pass
            else:
                os.killpg(current.pid, signal.SIGKILL)
                raise RuntimeError('Child left descendants')
            current = None
            if code != 0: raise RuntimeError('Child failure: ' + child['name'])
except BaseException as error:
    failure = type(error).__name__ + ': ' + str(error)
finally:
    if current is not None and current.poll() is None:
        os.killpg(current.pid, signal.SIGKILL)
        current.wait(timeout=plan['cleanup_seconds'])
    release = {'lease': args.lease, 'started_utc': started, 'ended_utc': now().isoformat(),
               'children': children, 'owned_jobs': 0, 'logs_closed': True, 'failure': failure}
    (out / 'release.json').write_text(json.dumps(release, indent=2) + '\n')
    print('CLEANUP RELEASE: children reaped, groups absent, logs closed, zero owned jobs.', flush=True)
if failure: raise RuntimeError(failure)
