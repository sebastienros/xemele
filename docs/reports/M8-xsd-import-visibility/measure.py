"""Root-only reviewed prebuilt allocation launcher. No builds, isolation or leases."""
import sys
sys.dont_write_bytecode = True
import json
import os
import time
from pathlib import Path
import bindings as b
from family import Runner, nested, publish_finite


def main():
    started=time.monotonic()
    assert len(sys.argv) == 4 and sys.argv[1] == '--root', 'Root-only --root prepared.json reviewedSHA256'
    path = Path(sys.argv[2]).resolve(); expected = sys.argv[3]
    review_sha = os.environ['XEMELE_IMPORT_REVIEW_SHA256']
    manifest, source = b.claim_prepared(path, expected, review_sha)
    directory = Path(manifest['directory']).resolve()
    # Directory is exclusive; prior attempts and raw results are never replaced.
    runner = Runner(directory, 120, counter_launcher=True,started=started)
    error = None
    try:
        with runner:
            env = b.environment(source['runtimeControlCommit'])
            for index, command in enumerate(manifest['commands']):
                assert b.digest(path)==expected
                b.verify_active_prepared(manifest)
                runner.run(command, 50, environment=env)
                assert b.digest(path)==expected
                b.verify_active_prepared(manifest)
                if index == 0: b.verify_control(directory / 'control.json',manifest['probePath'])
            assert time.monotonic() < runner.work, 'No late measurement success'
    except BaseException as caught:
        error = type(caught).__name__
    after = b.digest(path)==expected and not b.mismatches(manifest['identities']) and b.binary_identities(manifest['probePath'])==manifest['binaryInputs']
    release = dict(completed=error is None and after and runner.receipt is not None and runner.receipt['completed'] and time.monotonic() < runner.hard - 1,
                   error=error, afterIdentities=after, preparationSha256=expected,
                   rawAllocationsInspected=False,sequence=runner.sequence,controllerReceipt=str(Path(os.environ['XEMELE_IMPORT_RUN_DIRECTORY'])/'controller-release.json'))
    publish_finite(directory / 'release.json', release,runner.hard)
    return 0 if release['completed'] else 1


if __name__ == '__main__':
    with nested.interruptions(): raise SystemExit(main())
