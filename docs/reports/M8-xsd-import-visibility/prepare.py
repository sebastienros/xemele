"""Root-dispatched preparation only after source review; all counters disabled."""
import sys
sys.dont_write_bytecode = True
import json
import os
import time
import uuid
from pathlib import Path
import bindings as b
from family import Runner, nested, publish_finite


def main():
    started=time.monotonic()
    assert len(sys.argv)==4 and sys.argv[1]=='--released', 'Source review, positive-controls receipt/SHA and launch.py required'
    positive=Path(sys.argv[2]).resolve();positive_sha=sys.argv[3]
    review_sha = os.environ['XEMELE_IMPORT_REVIEW_SHA256']
    directory = Path(os.environ['XEMELE_IMPORT_RUN_DIRECTORY']) / 'preparation'
    source = None
    with Runner(directory, 400,started=started) as runner:
        source = b.review(review_sha)
        result=b.verify_positive_controls(positive,positive_sha,review_sha)
        active=directory/'active'
        probe=active/'bin/Xemele.Benchmarks.dll'
        required = b.build_commands(active)
        nested.durable(directory / 'required-commands.json', {'sourceSha256': b.digest(b.SOURCE), 'commands': required})
        commands_sha = b.digest(directory / 'required-commands.json')
        for command in required:
            assert b.digest(directory / 'required-commands.json') == commands_sha
            b.review(review_sha)
            runner.run(command, 180)
            b.review(review_sha)
        binaries = b.binary_identities(probe)
        tested = dict(identities=binaries, sourceSha256=b.digest(b.SOURCE), commandsSha256=commands_sha)
        nested.durable(directory / 'tested-inputs.json', tested)
        tested_sha = b.digest(directory / 'tested-inputs.json')
        assert not b.mismatches(binaries)
        control = b.commands(directory,probe)[0]
        runner.run(control, 40, environment=b.environment(source['runtimeControlCommit']))
        assert b.binary_identities(probe)==binaries and b.digest(directory / 'tested-inputs.json') == tested_sha
        b.verify_control(directory / 'control.json',probe)
        nested.durable(directory/'control-summary.json',{'completed':True,'routes':24,'consumed':1012,'retainedResults':28,'counterEnabled':False,'rawSha256':b.digest(directory/'control.json')})
        b.review(review_sha)
        assert time.monotonic() < runner.work, 'No late preparation success'
    assert runner.receipt['completed']
    measurement = b.OUT / ('measurement-' + uuid.uuid4().hex)
    identities = dict(source['identities'], **binaries)
    for file in [positive,positive.parent/'controller-release.json',Path(result['semanticReceipt']),Path(result['familyReceipt']),b.REVIEW,directory / 'control-summary.json', directory / 'tested-inputs.json', directory / 'required-commands.json', directory / 'family-cleanup.json']:
        identities[b.key(file)] = b.digest(file)
    assert not b.mismatches(identities) and time.monotonic() < runner.hard - 1
    manifest = dict(sourceRevision=source['runtimeControlCommit'], harnessSourceSha256=b.digest(b.SOURCE),
                    coordinatorReviewSha256=review_sha, commands=b.commands(measurement,probe), probePath=str(probe), activeOutputRoot=str(active), lifecycleState='active-prepared; single use only', directory=str(measurement),
                    maximumSeconds=120, workSeconds=100, cleanupReserveSeconds=20,
                    positiveControlsReceipt=str(positive),positiveControlsSha256=positive_sha,preparationReceipt=str(directory / 'family-cleanup.json'), testedInputsSha256=tested_sha,
                    identities=identities, binaryInputs=binaries, counterEnabled=False, routes=24, warmups=8, operations=32, retainedResults=28)
    publish_finite(directory / 'prepared.json', manifest,runner.hard)
    print('Prepared manifest', directory / 'prepared.json', b.digest(directory / 'prepared.json'), 'COUNTERS DISABLED')


if __name__ == '__main__':
    with nested.interruptions(): main()
