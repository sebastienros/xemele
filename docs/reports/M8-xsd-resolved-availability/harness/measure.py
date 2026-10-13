"""Coordinator-dispatched prebuilt allocation commands; no builds/restores."""
import sys
sys.dont_write_bytecode=True
import os
import time
from pathlib import Path
import bindings as b
from family import Runner,nested,publish_finite


def main():
    assert len(sys.argv)==4 and sys.argv[1]=='--root'
    started=time.monotonic();review_sha=os.environ['XEMELE_MISSING_REVIEW_SHA256']
    path=Path(sys.argv[2]);expected=sys.argv[3]
    row,source=b.claim_prepared(path,expected,review_sha)
    with Runner(row['measurementDirectory'],110,started=started,cleanup_reserve=20,counter_launcher=True) as runner:
        for index,command in enumerate(row['commands']):
            b.verify_prepared(path,expected,review_sha,allow_claimed=True)
            runner.run(command,45,environment=b.environment(source['runtimeSourceCommit']))
            b.verify_prepared(path,expected,review_sha,allow_claimed=True)
            b.verify_resolved_probe(Path(row['measurementDirectory'])/('disabled.json' if index==0 else 'allocations.json'),row['probePath'],index==1,source['runtimeSourceCommit'])
        assert time.monotonic()<runner.work
    assert runner.receipt['completed']
    publish_finite(Path(row['measurementDirectory'])/'measurement-semantic.json',dict(completed=True,controller=os.environ['XEMELE_MISSING_RUN_DIRECTORY'],
                   preparationSha256=expected,sourceSha256=b.digest(b.SOURCE),reviewSha256=review_sha,countersEnabled=True),runner.hard)


if __name__=='__main__':
    with nested.interruptions():main()
