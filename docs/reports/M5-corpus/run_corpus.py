"""Bounded ordinary correctness execution; no allocation/timing measurements."""
import json,os,pathlib,signal,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parents[3]
raw=ROOT/'artifacts/M5-corpus';raw.mkdir(parents=True,exist_ok=True)
command=['/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet','--fx-version','10.0.11',str(ROOT/'docs/reports/M5-corpus/bin/Release/net10.0/Xemele.Tests.dll'),str(raw/'corpus.json')]
process=None
try:
    with (raw/'corpus.log').open('w') as log:
        process=subprocess.Popen(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
        code=process.wait(timeout=180)
    print(json.dumps({'exit':code,'processExited':process.returncode is not None}));sys.exit(code)
finally:
    if process is not None:
        try:os.killpg(process.pid,signal.SIGKILL)
        except ProcessLookupError:pass
        process.wait(timeout=5)
