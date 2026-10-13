"""Finite affected conformance, differential and compiled API verification."""
import sys
sys.dont_write_bytecode = True
import argparse, hashlib, importlib.util, json, os, subprocess, time, xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
parser=argparse.ArgumentParser(); parser.add_argument('--phase',required=True); args=parser.parse_args()
host=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
out=ROOT/'artifacts/M9-dom-memory'/('compatibility-'+args.phase); out.mkdir(parents=True,exist_ok=False)
spec=importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
supervisor=importlib.util.module_from_spec(spec); spec.loader.exec_module(supervisor)
supervisor.ROOT,supervisor.ARTIFACTS=ROOT,out
env=dict(os.environ,PATH=str(host.parent)+os.pathsep+os.environ.get('PATH',''),DOTNET_ROOT=str(host.parent),
         DOTNET_ROLL_FORWARD='Disable',DOTNET_TieredCompilation='0',DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',
         DOTNET_gcServer='0',DOTNET_GCConcurrent='0',XEMELE_DOTNET=str(host),
         XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources')
report=dict(revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
            result='failed',commands=[],suites={})
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
deadline=time.monotonic()+1500
def run(argv,cap):
    index=len(report['commands']); end=min(deadline,time.monotonic()+cap)
    argv=list(map(str,argv)); receipt=supervisor.supervise_command(argv,index,env,end,end+10)
    log=out/f'command-{index}.log'
    row=dict(argv=argv,receipt=receipt,outputSha256=hashlib.sha256(log.read_bytes()).hexdigest(),outputTail=log.read_text(errors='replace')[-2500:])
    report['commands'].append(row); print(row['outputTail'],flush=True)
    if not supervisor.workload_succeeded(receipt): raise RuntimeError('Compatibility check failed')
try:
    for project in ['tests/Xemele.Conformance.Tests/Xemele.Conformance.Tests.csproj',
                    'tests/Xemele.Differential.Tests/Xemele.Differential.Tests.csproj',
                    'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj']:
        run([host,'restore',project,'--source','/private/tmp',*flags],200)
        run([host,'build',project,'-c','Release','--no-restore','--no-incremental',*flags],200)
    for name in ['Xemele.Conformance.Tests','Xemele.Differential.Tests']:
        result=out/(name+'.xml')
        try:
            run([host,'--fx-version','10.0.11',f'tests/{name}/bin/Release/net10.0/{name}.dll',
                 '-parallelMode','none','-culture','invariant','-noColor','-result-xml',result],450)
        finally:
            if result.is_file():
                tree=ET.parse(result).getroot(); assemblies=tree.findall('assembly')
                report['suites'][name]=dict(counts={k:sum(int(a.get(k,'0')) for a in assemblies) for k in ['total','passed','failed','errors','skipped','not-run']},
                    failures=[dict(name=t.get('name'),message=t.findtext('failure/message')) for t in tree.findall('.//test') if t.get('result')!='Pass'])
    api=out/'api.json'; run(['python3','-B','tools/ApiBaseline/compare.py','--report',api],100)
    data=json.loads(api.read_bytes()); report['api']=dict(matched=data['matched'],members=len(data['provenance']),
            fragments=len(data['inventory']['apiFragments']),assemblySha256=data['assemblySha256'],checkerOutput=data['checkerOutput'])
    assert report['api']['matched'] and report['api']['members']==1430
    report['result']='passed'
finally:
    (HERE/('compatibility-'+args.phase+'.json')).write_text(json.dumps(report,indent=2)+'\n')
