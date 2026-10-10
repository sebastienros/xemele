"""Build/discover/validate scoped common-only benchmark shapes, without measurement."""
import argparse, gzip, hashlib, json, os, platform, shlex, shutil, subprocess
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
FLAGS = ['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
p = argparse.ArgumentParser(description=__doc__); p.add_argument('--report-prefix', required=True); a = p.parse_args()
report = ROOT/'docs/reports/M2-dtd-composition'/a.report_prefix; report.mkdir()
logs = []; summary = {'result':'failed','host':platform.platform(),'architecture':platform.machine(),'measurement':False}
def run(*command):
    command = list(map(str, command)); result = subprocess.run(command,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=600)
    logs.append('$ '+shlex.join(command)+'\n'+result.stdout+'\nexit='+str(result.returncode)+'\n'); print(result.stdout[-1200:],flush=True)
    assert result.returncode == 0, logs[-1]
    return result.stdout
try:
    summary['sourceRevision'] = run('git','rev-parse','HEAD').strip()
    sources = ['src/Xemele/Parsing/Dtd/Grammar/Parser','benchmarks/Xemele.Benchmarks/M2-dtd-composition','docs/reports/M2-dtd-composition/BenchmarkDriver.cs','docs/reports/M2-dtd-composition/Xemele.DtdCompositionBenchmarks.csproj','docs/reports/M2-dtd-composition/verify_benchmarks.py']
    assert not run('git','diff','--name-only','HEAD','--',*sources).strip()
    assert not run('git','ls-files','--others','--exclude-standard','--',*sources).strip()
    dotnet = shutil.which('dotnet'); assert dotnet
    assert run(dotnet,'--version').strip() == '10.0.400'
    run(dotnet,'build','docs/reports/M2-dtd-composition/Xemele.DtdCompositionBenchmarks.csproj',*FLAGS)
    run(dotnet,'build','benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj',*FLAGS)
    dll = ROOT/'docs/reports/M2-dtd-composition/bin/Release/net10.0/Xemele.DtdCompositionBenchmarks.dll'
    correctness = run(dotnet,dll,'--correctness')
    assert correctness == 'runtime=10.0.11; two lengths, 20 composition/seed outputs and 2048 borrowed scan calls passed\n'
    discovery = run(dotnet,dll,'--list','flat')
    assert discovery.splitlines() == ['Xemele.Benchmarks.DtdCompositionBenchmarks.SeedAndFramedComposition','Xemele.Benchmarks.DtdCompositionBenchmarks.SeedAndModelSnapshot','Xemele.Benchmarks.DtdFramedScanBenchmarks.BorrowedCommentScan']
    summary.update(correctness=correctness,discovery=discovery.splitlines(),dllSha256=hashlib.sha256(dll.read_bytes()).hexdigest(),result='passed')
finally:
    (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0)); (report/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
