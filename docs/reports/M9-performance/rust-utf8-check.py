"""Requalify changed UTF-8 serialization on every frozen Rust-reference fixture."""
import sys
sys.dont_write_bytecode=True
import hashlib,importlib.util,json,os,subprocess,time,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent
out=ROOT/'artifacts/M9-performance/rust-utf8';out.mkdir(parents=True,exist_ok=False)
spec=importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
supervisor=importlib.util.module_from_spec(spec);spec.loader.exec_module(supervisor)
supervisor.ROOT,supervisor.ARTIFACTS=ROOT,out
env=dict(os.environ,CARGO_TARGET_DIR=str(out/'target'),PYTHONDONTWRITEBYTECODE='1')
manifest=ROOT/'benchmarks/baselines/release/fixtures.json';fixtures=json.loads(manifest.read_bytes())['fixtures']
report=dict(result='failed',revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
            fixtureManifestSha256=hashlib.sha256(manifest.read_bytes()).hexdigest(),commands=[],rows=[],controlledTimingCredit=False,
            encoding='UTF-8',rustToolchain='1.95.0',xmloxideRevision='555055f1ea6bf9944488fd96415b6f03b5b43696')
# Reuse the frozen independent ElementTree framing oracle, not the reference's own parser.
oracleSpec=importlib.util.spec_from_file_location('fixture_oracle',ROOT/'benchmarks/baselines/release/freeze_fixtures.py')
oracle=importlib.util.module_from_spec(oracleSpec);oracleSpec.loader.exec_module(oracle)
deadline=time.monotonic()+1200
def run(argv,cap):
    index=len(report['commands']);end=min(deadline,time.monotonic()+cap);argv=list(map(str,argv))
    receipt=supervisor.supervise_command(argv,index,env,end,end+10);log=out/f'command-{index}.log';text=log.read_text(errors='replace')
    row=dict(argv=argv,receipt=receipt,outputSha256=hashlib.sha256(log.read_bytes()).hexdigest());report['commands'].append(row)
    if not supervisor.workload_succeeded(receipt):row['failureTail']=text[-4000:];raise RuntimeError(row['failureTail'])
    return text
try:
    project=HERE/'RustWorker/Cargo.toml'
    run(['/Users/sebastienros/.cargo/bin/rustup','run','1.95.0','cargo','build','--release','--offline','--locked','--manifest-path',project],500)
    binary=out/'target/release/xemele-release-reference';report['binarySha256']=hashlib.sha256(binary.read_bytes()).hexdigest()
    for f in fixtures:
        prefix=out/f['id'];text=run([binary,manifest.parent/f['path'],f['xpath'],'qualify',1,prefix],120)
        actual=json.loads(text);expected=f['semantic']['sha256']
        events=Path(str(prefix)+'.events').read_bytes();xml=Path(str(prefix)+'.xml').read_bytes()
        assert hashlib.sha256(events).hexdigest()==expected and actual['xpath']==f['xpathExpected']
        # The same independent frozen oracle verifies emitted UTF-8 roundtrip semantics.
        digest=oracle.semantic(xml)['sha256']
        assert digest==expected,(f['id'],digest,expected)
        assert b'encoding="UTF-8"' in xml
        report['rows'].append(dict(fixture=f['id'],inputSha256=f['sha256'],actual=actual,
                                  semanticSha256=expected,serializationRoundtripSha256=digest,serializedUtf8Bytes=len(xml)))
        print('PASS UTF8',f['id'],len(xml),flush=True)
    report.update(result='passed',qualifiedFixtures=10)
finally:
    (HERE/'rust-utf8-qualification.json').write_text(json.dumps(report,indent=2)+'\n')
