#!/usr/bin/env python3
"""Scoped frozen correctness/API/consumer checks; measurement remains in measure.py."""
import argparse, hashlib, json, os, re, shutil, subprocess, sys, tempfile
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[3]
OWN = Path(__file__).resolve().parent
SDK = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
DOTNET = SDK/'dotnet'
FLAGS = ['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:NuGetAudit=false']

def digest(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--mode',choices=['api','correctness'],required=True); parser.add_argument('--report',required=True); args=parser.parse_args()
    assert re.fullmatch('[a-z0-9-]+',args.report)
    report=OWN/(args.report+'.json'); assert not report.exists()
    env=dict(os.environ,DOTNET_ROOT=str(SDK),DOTNET_CLI_USE_MSBUILD_SERVER='0',XEMELE_DOTNET=str(DOTNET),XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources')
    env['PATH']=str(SDK)+os.pathsep+env.get('PATH','')
    summary={'mode':args.mode,'result':'failed','checks':[]}
    def run(*command):
        command=list(map(str,command)); process=subprocess.run(command,cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=300)
        summary['checks'].append({'command':command,'exit':process.returncode})
        if process.returncode:
            print(process.stdout[-7000:],flush=True); raise RuntimeError('Check failed: '+str(command))
        return process.stdout
    def build(project): return run(DOTNET,'build',project,*FLAGS)
    try:
        head=run('git','rev-parse','HEAD').strip(); summary['head']=head
        assert run('git','branch','--show-current').strip()=='codex/m2-undeclared-content'
        inputs=run('git','ls-files','--','src','tests','samples','benchmarks','docs/api','docs/reports/M2-undeclared-content','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json').splitlines()
        sources={p:digest(ROOT/p) for p in inputs if Path(p).suffix in ('.cs','.csproj','.props','.targets','.fixture','.txt','.py','.json') and not p.startswith('docs/reports/M2-undeclared-content/') or p.startswith('docs/reports/M2-undeclared-content/') and Path(p).suffix in ('.cs','.csproj','.py')}
        for path in sources: assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',head+':'+path],cwd=ROOT),path+' differs from Git'
        summary['sourceInputs']=sources
        assert run(DOTNET,'--version').strip()=='10.0.400'
        summary['runtime']=run(DOTNET,'--list-runtimes').strip().splitlines(); summary['dotnetSha256']=digest(DOTNET)
        build('tests/Xemele.Tests/Xemele.Tests.csproj')
        runtime=ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll'
        with tempfile.TemporaryDirectory(prefix='checks-',dir=OWN) as directory:
            temporary=Path(directory)
            if args.mode=='api':
                build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj'); checker=ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
                build('samples/M2-undeclared-contract/Sample.csproj'); model=ROOT/'samples/M2-undeclared-contract/bin/Release/net10.0/UndeclaredContract.dll'
                summary['frozenContractOutput']=run(DOTNET,model,runtime).strip()
                model_api=temporary/'model.txt'; run(DOTNET,checker,'emit',model,model_api)
                rows=[json.loads(v) for v in model_api.read_text().splitlines()[1:]]
                additive=[r for r in rows if 'Xemele.XmlUnresolvedReference' in r['member'] and 'Xemele.XmlDocument' not in r['member'] or r['member']=='P:Xemele.XmlDocument.UnresolvedReferences']
                approved=[json.loads(v) for v in (ROOT/'docs/api/M2-undeclared-content.txt').read_text().splitlines()[1:]]
                assert additive==approved,'Additive fragment differs from independently approved fixture'
                api_root=temporary/'snapshot'; (api_root/'docs/api').mkdir(parents=True)
                for fragment in (ROOT/'docs/api').glob('*.txt'): shutil.copyfile(fragment,api_root/'docs/api'/fragment.name)
                sys.path.insert(0,str(ROOT/'tools/ApiBaseline')); from compare import compare
                api=compare(api_root,runtime,checker); assert api['matched'],api['checkerOutput']
                summary['api']={'additiveMembers':len(additive),'allMembers':len(api['provenance']),'assemblySha256':api['assemblySha256'],'baselineSha256':digest(ROOT/'docs/api/M2-undeclared-content.txt')}
                summary['frozenPublicModels']={}
                for lane in ['Resolver','Dtd','Encoding','Entities','Recovery']:
                    build(f'docs/reports/M2-contracts/Projects/{lane}/{lane}.csproj')
                    assembly=ROOT/f'docs/reports/M2-contracts/Projects/{lane}/bin/Release/net10.0/M2{lane}.dll'
                    golden=ROOT/f'tests/Contracts/Fixtures/M2/{lane}.golden.txt'
                    run(DOTNET,checker,'compare',assembly,golden)
                    summary['frozenPublicModels'][lane]={'assemblySha256':digest(assembly),'baselineSha256':digest(golden)}
                build('docs/reports/M2-contracts/Projects/Checks/Checks.csproj')
                summary['frozenModelAssertions']=run(DOTNET,ROOT/'docs/reports/M2-contracts/Projects/Checks/bin/Release/net10.0/M2Checks.dll').strip()
                build('samples/M2-undeclared-content/Sample.csproj'); sample=ROOT/'samples/M2-undeclared-content/bin/Release/net10.0/Sample.dll'
                source=run(DOTNET,sample).strip(); summary['sourceConsumer']=source
                feed=temporary/'feed'; run(DOTNET,'pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','-o',feed)
                package=next(feed.glob('*.nupkg')); summary['packageSha256']=digest(package)
                package_output=temporary/'package-build'; run(DOTNET,'build','samples/M2-undeclared-content/Sample.csproj',*FLAGS,'--artifacts-path',package_output,'-p:UsePackage=true','-p:ManagePackageVersionsCentrally=false',f'-p:RestoreSources={feed}',f'-p:RestorePackagesPath={temporary}/packages')
                package_sample=package_output/'bin/Sample/release/Sample.dll'; summary['packageConsumer']=run(DOTNET,package_sample).strip(); assert source==summary['packageConsumer']
            else:
                held = {
                    "Xemele.Tests.Syntax.StreamingTests.WarmRepeatedTagsAndStreamingUnicodeDispatchAllocateZero",
                    "Xemele.Tests.PiTargets.PiTargetTests.RepeatedBorrowedPiTargetsAllocateZeroAfterWarmup",
                    "Xemele.Tests.EncodingCompatibility.EncodingTests.WarmLegacyConversionAndDeclarationMatchingAllocateZero",
                    "Xemele.Tests.Resolution.ResolverTests.WarmBorrowedReadsAllocateNothing",
                    "Xemele.Tests.FramingTests.WarmWhitespacePrimitiveHasNoAllocations",
                    "Xemele.Tests.Tree.TreeTests.WarmTraversalAndRepeatedNameInternProbesAllocateNothing"
                }
                filters=[part for method in sorted(held) for part in ['-method-',method]]
                summary['heldAllocationMethods']=sorted(held); summary['heldReason']='No standalone allocation/counters before source review and explicit bounded host lease; prior passing unchanged observations are preserved.'
                tests=ROOT/'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll'; xml=temporary/'correctness.xml'
                before={str(p):digest(p) for p in tests.parent.iterdir() if p.is_file()}
                run(DOTNET,tests,'-noColor',*filters,'-result-xml',xml)
                rows=list(ET.parse(xml).iter('test')); assert rows and all(r.get('result')=='Pass' for r in rows)
                ids=sorted((r.get('type'),r.get('method'),r.get('name')) for r in rows)
                authored=[identity for identity in ids if identity[0].startswith('Xemele.Tests.UndeclaredReferences.')]
                assert len(authored)==len(set(authored)), 'Duplicate newly authored task rows'
                summary['runtimeCorrectness']={'passed':len(rows),'failed':0,'skipped':0,'identitySha256':hashlib.sha256(json.dumps(ids,ensure_ascii=True,separators=(',',':')).encode()).hexdigest(),'owned':sum(r.get('type','').startswith('Xemele.Tests.UndeclaredReferences.') for r in rows),'assemblySha256':digest(tests)}
                assert before=={str(p):digest(p) for p in tests.parent.iterdir() if p.is_file()},'Output closure changed'
                summary['outputClosureUnchanged']=True
        summary['temporaryOutputsRemoved']=True
        assert sources=={p:digest(ROOT/p) for p in sources}; summary['sourceFreezeUnchanged']=True
        run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py')
        summary['planning']=run('python3','-B','tools/planning/tasks.py','validate').strip()
        run('git','diff','--check'); summary['result']='passed'
    except Exception as error:
        summary['failure']=str(error) or type(error).__name__; raise
    finally:
        report.write_text(json.dumps(summary,indent=2)+'\n'); print(json.dumps({k:v for k,v in summary.items() if k in ('head','mode','result','failure','runtimeCorrectness','api')}))
if __name__=='__main__': main()
