"""Qualify cached Coverlet and measure ordinary unit-test branches on disposable copies."""
import sys
sys.dont_write_bytecode=True
import hashlib,importlib.util,json,os,shutil,subprocess,time,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent
out=ROOT/'artifacts/M9-gate/coverage'
out.mkdir(parents=True,exist_ok=False)
package=Path('/Users/sebastienros/.nuget/packages/coverlet.console/6.0.4/coverlet.console.6.0.4.nupkg')
assert hashlib.sha256(package.read_bytes()).hexdigest()=='48ad640bc67eb52e0f95c2f80f6368b24088033c5792e834d7ec4ae2ae543217'
with zipfile.ZipFile(package) as archive:
    for name in archive.namelist():
        if name.startswith('tools/net6.0/any/') and not name.endswith('/'):
            target=out/'tool'/name.removeprefix('tools/net6.0/any/');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(archive.read(name))
(out/'tool-identity.json').write_text(json.dumps(dict(package='coverlet.console',version='6.0.4',packageSha256='48ad640bc67eb52e0f95c2f80f6368b24088033c5792e834d7ec4ae2ae543217',documentation='https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/GlobalTool.md'),indent=2)+'\n')
assert (out/'tool/coverlet.console.dll').is_file()
spec=importlib.util.spec_from_file_location('supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
s=importlib.util.module_from_spec(spec);spec.loader.exec_module(s);s.ROOT,s.ARTIFACTS=ROOT,out
host=Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet')
env=dict(os.environ,DOTNET_ROOT=str(host.parent),DOTNET_ROLL_FORWARD='Disable',DOTNET_TieredCompilation='0',
         DOTNET_TieredPGO='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0',DOTNET_GCConcurrent='0',
         XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources')
report=dict(result='failed',revision=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),commands=[],
            tool=json.loads((out/'tool-identity.json').read_bytes()),fuzzExecuted=False,productionInstrumented=False)
deadline=time.monotonic()+1200
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(argv,cap):
    i=len(report['commands']);end=min(deadline,time.monotonic()+cap);argv=list(map(str,argv))
    receipt=s.supervise_command(argv,i,env,end,end+10);log=out/f'command-{i}.log';text=log.read_text(errors='replace')
    report['commands'].append(dict(argv=argv,receipt=receipt,outputSha256=sha(log),outputTail=text[-4000:]));print(text[-2500:],flush=True)
    assert s.workload_succeeded(receipt),text
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
try:
    control=ROOT/'tests/ReleaseCampaign/CoverageControl/CoverageControl.csproj'
    run([host,'restore',control,'--source','/private/tmp',*flags],120)
    run([host,'build',control,'-c','Release','--no-restore',*flags],180)
    controlDir=out/'control';shutil.copytree(control.parent/'bin/Release/net10.0',controlDir)
    tool=out/'tool/coverlet.console.dll';targetArgs=f'--fx-version 10.0.11 "{controlDir / "CoverageControl.dll"}"'
    run([host,'--fx-version','10.0.11',tool,controlDir/'CoverageControl.dll','--target',host,'--targetargs',targetArgs,
         '--include-test-assembly','--include','[CoverageControl]*','--exclude-assemblies-without-sources','None',
         '--format','json','--output',out/'control-coverage.json'],120)
    data=json.loads((out/'control-coverage.json').read_bytes());branches=[]
    for documents in data.values():
        for classes in documents.values():
            for methods in classes.values():
                for name,method in methods.items():
                    if 'PositiveOrNegative' in name:branches.extend(method['Branches'])
    assert len(branches)==2 and sum(b['Hits']>0 for b in branches)==1,branches
    report['collectorControl']=dict(total=2,covered=1,uncovered=1,realBranchesObserved=True)
    project=ROOT/'tests/Xemele.Tests/Xemele.Tests.csproj'
    run([host,'restore',project,'--source','/private/tmp',*flags],150)
    run([host,'build',project,'-c','Release','--no-restore',*flags],180)
    original=ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll';report['productionSha256Before']=sha(original)
    tests=out/'tests';shutil.copytree(project.parent/'bin/Release/net10.0',tests)
    args=f'--fx-version 10.0.11 "{tests / "Xemele.Tests.dll"}" -parallelMode none -culture invariant -noColor -result-xml "{out / "unit.xml"}"'
    run([host,'--fx-version','10.0.11',tool,tests/'Xemele.Tests.dll','--target',host,'--targetargs',args,
         '--include','[Xemele]Xemele.Parsing.Syntax.XmlParserCore*','--include','[Xemele]Xemele.Parsing.Input.XmlInputDecoder*','--include','[Xemele]Xemele.XmlDocument*','--include','[Xemele]Xemele.Streaming.OptimisticSax*','--include','[Xemele]Xemele.Internal.Operations.FastLineBreaks*','--include','[Xemele]Xemele.Internal.Operations.OptimisticDoctype*','--include','[Xemele]Xemele.Internal.Operations.OptimisticEntity*','--include','[Xemele]Xemele.Streaming.SaxName*','--include','[Xemele]Xemele.Streaming.SaxTapeProbe*','--exclude-assemblies-without-sources','None',
         '--format','json','--output',out/'unit-coverage.json'],500)
    report['productionSha256After']=sha(original);assert report['productionSha256Before']==report['productionSha256After']
    coverage=json.loads((out/'unit-coverage.json').read_bytes());assert coverage
    report['rawCoverageSha256']=sha(out/'unit-coverage.json');report['result']='collection-passed'
finally:
    (HERE/'ordinary-coverage.json').write_text(json.dumps(report,indent=2)+'\n')
