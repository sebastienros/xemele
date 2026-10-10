#!/usr/bin/env python3
"""Bounded hook correctness/API/entrypoint checks; no performance measurements."""
import hashlib, json, os, shutil, subprocess, sys, tempfile, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[3]
OWN = Path(__file__).resolve().parent
SDK = Path('/Users/sebastienros/Library/Application Support/dotnetup/dotnet')
DOTNET = SDK/'dotnet'
FLAGS = ['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def digest(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def main():
    report = OWN/'verification-summary.json'
    assert not report.exists()
    env = dict(os.environ, DOTNET_ROOT=str(SDK), PATH=str(SDK)+os.pathsep+os.environ.get('PATH',''), DOTNET_CLI_USE_MSBUILD_SERVER='0')
    resume = '--remaining' in sys.argv
    summary = json.loads((OWN/'harness-discovery-failure.json').read_text()) if resume else {'checks':[]}
    summary.setdefault('testRevision',summary.get('revision'))
    summary['result']='failed'
    summary.pop('failure',None)
    def run(*command):
        command = list(map(str,command))
        p = subprocess.run(command,cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=300)
        summary['checks'].append({'command':command,'exit':p.returncode})
        if p.returncode: print(p.stdout[-6000:]); raise RuntimeError('Failed: '+str(command))
        return p.stdout.strip()
    def build(project,*extra): return run(DOTNET,'build',project,*FLAGS,*extra)
    try:
        summary['revision'] = run('git','rev-parse','HEAD')
        if not resume: summary['testRevision']=summary['revision']
        assert run('git','branch','--show-current') == 'codex/m2-external-declaration-hook'
        assert run(DOTNET,'--version') == '10.0.400'
        summary['runtime'] = run(DOTNET,'--list-runtimes').splitlines()
        if not resume: build('tests/Xemele.Tests/Xemele.Tests.csproj')
        runtime = ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll'
        test = ROOT/'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll'
        if resume:
            assert digest(runtime)==summary['productSha256'] and digest(test)==summary['testSha256']
        else:
            summary['productSha256'] = digest(runtime)
            summary['testSha256'] = digest(test)
        with tempfile.TemporaryDirectory(prefix='xemele-hook-',dir='/private/tmp') as directory:
            temp = Path(directory)
            if not resume:
                filters = [part for pattern in ['Xemele.Tests.ExternalDeclarationHook.*','Xemele.Tests.Entities.*','Xemele.Tests.Versions.*','Xemele.Tests.FramingTests','Xemele.Tests.EncodingCompatibility.*'] for part in ['-class',pattern]]
                held = ['Xemele.Tests.FramingTests.WarmWhitespacePrimitiveHasNoAllocations','Xemele.Tests.EncodingCompatibility.EncodingTests.WarmLegacyConversionAndDeclarationMatchingAllocateZero']
                for method in held: filters += ['-method-',method]
                summary['preservedUnchangedMeasurements'] = held
                xml = temp/'tests.xml'
                run(DOTNET,test,'-noColor',*filters,'-result-xml',xml)
                rows = list(ET.parse(xml).iter('test'))
                assert rows and all(row.get('result')=='Pass' for row in rows)
                summary['tests'] = {'passed':len(rows),'failed':0,'skipped':0,'owned':sum(row.get('type','').startswith('Xemele.Tests.ExternalDeclarationHook.') for row in rows)}
                assert summary['tests']['owned']==17
                summary['tests']['byType'] = {t:sum(r.get('type')==t for r in rows) for t in sorted({r.get('type') for r in rows})}
            foundation_xml=temp/'foundation.xml'
            run(DOTNET,test,'-noColor','-class','Xemele.Tests.EntitiesFoundation.*','-result-xml',foundation_xml)
            foundation=list(ET.parse(foundation_xml).iter('test'))
            assert foundation and all(r.get('result')=='Pass' for r in foundation)
            summary['foundationTests']={'passed':len(foundation),'failed':0,'skipped':0,'revision':summary['testRevision']}
            build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj')
            sys.path.insert(0,str(ROOT/'tools/ApiBaseline'))
            from compare import compare
            old_path = os.environ.get('PATH',''); os.environ['PATH']=env['PATH']
            try:
                snapshot=temp/'api-snapshot'; (snapshot/'docs/api').mkdir(parents=True)
                for fragment in (ROOT/'docs/api').glob('*.txt'): shutil.copyfile(fragment,snapshot/'docs/api'/fragment.name)
                api=compare(snapshot,runtime)
                summary['apiScope']='All approved API fragments copied unchanged; API-only discovery snapshot, no corpus discovery claim.'
            finally: os.environ['PATH']=old_path
            assert api['matched'],api['checkerOutput']
            assert len(api['provenance'])==290
            summary['api']={'members':len(api['provenance']),'matched':True,'checkerOutput':api['checkerOutput']}
            sample='samples/M2-external-declaration-hook/Sample.csproj'
            build(sample)
            sample_path=ROOT/'samples/M2-external-declaration-hook/bin/Release/net10.0'
            summary['sourceEntrypoint']=run(DOTNET,sample_path/'Sample.dll')
            feed=temp/'feed'; run(DOTNET,'pack','src/Xemele/Xemele.csproj',*FLAGS,'--no-build','-o',feed)
            package=next(feed.glob('*.nupkg'))
            with zipfile.ZipFile(package) as archive:
                packaged=archive.read('lib/net10.0/Xemele.dll')
            assert hashlib.sha256(packaged).hexdigest()==summary['productSha256']
            output=temp/'package-build'
            build(sample,'--artifacts-path',output,'-p:UsePackage=true','-p:ManagePackageVersionsCentrally=false',f'-p:RestoreSources={feed}',f'-p:RestorePackagesPath={temp}/packages')
            package_path=output/'bin/Sample/release'
            summary['packageEntrypoint']=run(DOTNET,package_path/'Sample.dll')
            assert summary['sourceEntrypoint']==summary['packageEntrypoint']
            for copy in [test.parent/'Xemele.dll',sample_path/'Xemele.dll',package_path/'Xemele.dll']:
                assert digest(copy)==summary['productSha256']
            assert digest(runtime)==summary['productSha256']
            summary['productByteIdentity']='source/test/source sample/fresh local package/packaged sample identical'
        summary['temporaryOutputsRemoved']=True
        summary['planningControls']=run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py')
        summary['planningValidation']=run('python3','-B','tools/planning/tasks.py','validate')
        run('git','diff','--check')
        summary['result']='passed'
    except Exception as error:
        summary['failure']=str(error); raise
    finally:
        report.write_text(json.dumps(summary,indent=2)+'\n'); print(json.dumps({k:v for k,v in summary.items() if k in ['revision','result','failure','tests','api']}))
if __name__=='__main__': main()
