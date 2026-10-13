"""Fresh single-use preparation binds released controls/correctness; counters off."""
import sys
sys.dont_write_bytecode=True
import os
import time
import uuid
from pathlib import Path
import bindings as b
from family import Runner,nested,publish_finite


def main():
    assert len(sys.argv)==6 and sys.argv[1]=='--root','--root positive.json SHA checks.json SHA'
    started=time.monotonic();review_sha=os.environ['XEMELE_MISSING_REVIEW_SHA256'];source=b.review(review_sha)
    positive,positive_sha,checks,checks_sha=sys.argv[2:]
    b.positive_controls(positive,positive_sha,review_sha)
    b.verify_release(checks,checks_sha,'check',review_sha)
    controller=Path(os.environ['XEMELE_MISSING_RUN_DIRECTORY']);directory=controller/'preparation'
    with Runner(directory,580,started=started,cleanup_reserve=40) as runner:
        active=directory/'active';probe=active/'bin/Xemele.Benchmarks.dll'
        commands=b.build_commands('benchmarks/Xemele.Benchmarks/M8-xsd-resolved-availability/Resolved.Probe.csproj',active)
        nested.durable(directory/'required-commands.json',dict(commands=commands,sourceSha256=b.digest(b.SOURCE)))
        commands_sha=b.digest(directory/'required-commands.json')
        for argv in commands:
            b.positive_controls(positive,positive_sha,review_sha);b.verify_release(checks,checks_sha,'check',review_sha)
            assert b.digest(directory/'required-commands.json')==commands_sha
            runner.run(argv,180)
        binary_inputs,folders=b.binary_identities(probe)
        runner.run(b.probe_commands(directory,probe)[0],45,environment=b.environment(source['runtimeSourceCommit']))
        disabled=b.verify_resolved_probe(directory/'disabled.json',probe,False,source['runtimeSourceCommit'])
        assert b.binary_identities(probe)==(binary_inputs,folders)
        nested.durable(directory/'disabled-summary.json',dict(completed=True,countersEnabled=False,routes=6,retainedResults=192,
                       retainedSchemas=8,consumed=disabled['consumed'],rawSha256=b.digest(directory/'disabled.json')))
        b.review(review_sha)
    assert runner.receipt['completed']
    measurement=b.OUT/('measurement-'+uuid.uuid4().hex)
    identities=dict(source['identities']);identities.update(binary_inputs)
    receipts=[b.REVIEW,Path(positive),Path(checks),directory/'disabled-summary.json',directory/'family-cleanup.json',directory/'required-commands.json']
    for path in [Path(positive),Path(checks)]:
        data=__import__('json').loads(path.read_text());parent=Path(data['controller'])
        receipts += [parent/'controller-release.json',parent/'family-cleanup.json',parent/'terminal-cleanup.json']
    identities.update(b.identities(receipts))
    manifest=dict(completed=True,controller=str(controller),sourceSha256=b.digest(b.SOURCE),reviewSha256=review_sha,
        runtimeSourceCommit=source['runtimeSourceCommit'],positiveReceipt=positive,positiveSha256=positive_sha,
        checksReceipt=checks,checksSha256=checks_sha,disabledReceipt=str(directory/'disabled-summary.json'),disabledReceiptSha256=b.digest(directory/'disabled-summary.json'),
        preparationFamilyReceipt=str(directory/'family-cleanup.json'),preparationFamilySha256=b.digest(directory/'family-cleanup.json'),
        commands=b.probe_commands(measurement,probe),probePath=str(probe),activeOutputRoot=str(active),measurementDirectory=str(measurement),
        binaryInputs=binary_inputs,binaryFolders=folders,activeOutputIdentities=b.directory_inputs([active]),identities=identities,maximumSeconds=120,workSeconds=90,cleanupReserveSeconds=30,
        counterEnabled=False,state='active prepared; exclusive consumed marker required; no reuse')
    assert not b.mismatches(identities)
    publish_finite(directory/'prepared.json',manifest,runner.hard)
    print('Prepared source/correctness/disabled/binary bindings; counters disabled',flush=True)


if __name__=='__main__':
    with nested.interruptions():main()
