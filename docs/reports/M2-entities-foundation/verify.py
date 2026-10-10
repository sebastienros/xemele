"""Bounded entity foundation checks, preserving actual cumulative predecessors."""
import argparse, gzip, hashlib, json, os, platform, re, shlex, shutil, subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
REPORT=ROOT/'docs/reports/M2-entities-foundation'
CLAIM='bd99686e19ed877a11da2af02f6ac6b30e129e3f'
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']

def main():
    p=argparse.ArgumentParser();p.add_argument('--report-prefix',required=True);p.add_argument('--audit-base',default=CLAIM);a=p.parse_args()
    assert re.fullmatch('[a-z0-9-]+',a.report_prefix)
    report=REPORT/a.report_prefix;report.mkdir()
    raw=ROOT/'artifacts/M2-entities-foundation'/a.report_prefix;raw.mkdir(parents=True)
    dotnet=Path(shutil.which('dotnet')).resolve()
    env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0',XEMELE_DOTNET=str(dotnet),XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',XEMELE_CORPUS_RESULTS=str(raw/'case-results'))
    logs=[];summary={'result':'failed','host':platform.platform(),'architecture':platform.machine()}
    def run(*cmd, expected=0):
        cmd=list(map(str,cmd));cmd[0]=str(dotnet) if cmd[0]=='dotnet' else cmd[0]
        print('$ '+shlex.join(cmd),flush=True)
        result=subprocess.run(cmd,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=600)
        logs.append('$ '+shlex.join(cmd)+'\n'+result.stdout+'\nexit='+str(result.returncode));print(result.stdout[-900:],flush=True)
        assert result.returncode==expected,logs[-1]
        return result.stdout
    try:
        revision=run('git','rev-parse','HEAD').strip();summary['sourceRevision']=revision
        run('git','merge-base','--is-ancestor',CLAIM,'HEAD');run('git','merge-base','--is-ancestor',a.audit_base,'HEAD')
        task=json.loads((ROOT/'docs/tasks/M2-entities-foundation.json').read_text())
        for dep in task['depends_on']:
            record=json.loads((ROOT/f'docs/tasks/{dep}.json').read_text());assert record['status']=='verified'
            run('git','merge-base','--is-ancestor',record['integration_revision'],'HEAD')
        assert run('dotnet','--version').strip()=='10.0.400'
        run('dotnet','build','samples/M2-entities-foundation/Sample.csproj',*FLAGS)
        sample=ROOT/'samples/M2-entities-foundation/bin/Release/net10.0/Xemele.EntitiesFoundationSample.dll'
        assert run('dotnet',sample,'--runtime').strip()=='10.0.11'
        actual=run('dotnet',sample,'--contract');(report/'actual-contract.txt').write_text(actual)
        approved=raw/'approved';approved.mkdir()
        fixture_root=ROOT/'tests/Contracts/Fixtures'
        names=['M2/Common','M2/Options','M2/Resolver','M2/Dtd','M1/Tree','M2/Entities','M2/Syntax','M1/Input','M2/DomMetadata']
        for name in names:
            source=(fixture_root/(name+'.cs.fixture')).read_text()
            if name=='M2/Options':source=source.replace('    public bool Recover { get; init; }\n','')
            (approved/(name.replace('/','-')+'.cs')).write_text(source)
        (approved/'Program.cs').write_text('Inventory.Print();')
        (approved/'Approved.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><Compile Include="'+str(ROOT/'samples/M2-entities-foundation/Inventory.cs')+'" /></ItemGroup></Project>')
        run('dotnet','build',approved/'Approved.csproj',*FLAGS)
        model=approved/'bin/Release/net10.0/Approved.dll'
        approved_output=run('dotnet',model);(report/'approved-contract.txt').write_text(approved_output)
        assert actual==approved_output,'approved nullable/default/generated record/event/hook inventory mismatch'
        (REPORT/'internal-contract.txt').write_text(actual)
        source=approved/'M2-Entities.cs';original=source.read_text();source.write_text(original.replace('string? PublicId','string PublicId'))
        run('dotnet','build',approved/'Approved.csproj',*FLAGS)
        assert run('dotnet',model)!=actual,'compiled nullability mutation not detected'
        source.write_text(original)
        summary['approvedContract']={'types':10,'matched':True,'compiledNullableMutationDetected':True,'recoverExcludedAsDownstream':True}
        sample_output=run('dotnet',sample)
        expected='defaults=Prohibit;resolver=True;clone=Parse;limitsOwned=True\nmanualCallback=True;name=root;entity=owned;rootAbsent=True\n'
        assert sample_output==expected
        (report/'sample.txt').write_text(sample_output)
        allocation_output=run('dotnet',sample,'--allocations');(report/'allocations.txt').write_text(allocation_output)
        m=re.search(r'iterations=10000;record=(\d+);optionAndLimitsSnapshot=(\d+);callback=(\d+)',allocation_output);assert m
        summary['allocationBytesPerOperation']={k:int(v)/10000 for k,v in zip(['record','optionsAndLimitsSnapshot','callback'],m.groups())}
        run('dotnet','build','tests/Xemele.Tests/Xemele.Tests.csproj',*FLAGS)
        focused=raw/'focused.xml'
        run('dotnet',ROOT/'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-noColor','-class','Xemele.Tests.EntitiesFoundation.*','-result-xml',focused)
        cases=list(ET.parse(focused).iter('test'));assert cases and all(t.get('result')=='Pass' for t in cases)
        summary['focusedTests']=len(cases);(report/'focused.xml.gz').write_bytes(gzip.compress(focused.read_bytes(),mtime=0))
        run('python3','-B','docs/reports/M1-corpus-import/provision.py')
        run('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS)
        original=ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
        shutil.copytree(original.parent,raw/'pinned-reference');pinned=raw/'pinned-reference'/original.name
        runner_hash=hashlib.sha256(pinned.read_bytes()).hexdigest();env['XEMELE_BCL_RUNNER']=str(pinned)
        inherited=run('python3','-B','docs/reports/M1-contracts/verify.py')
        totals=re.findall(r'^\s*total:\s*(\d+)\s*$',inherited,re.MULTILINE)
        expected_runtime=4460+len(cases);expected_total=expected_runtime+255+299
        assert totals and int(totals[-1])==expected_total,(totals,expected_total)
        summary['mtpTests']={'total':int(totals[-1]),'failed':0,'skipped':0}
        run('python3','-B','tools/ApiBaseline/compare.py','--report',report/'runtime-api.json')
        api=json.loads((report/'runtime-api.json').read_text());assert api['matched'] and len(api['provenance'])==272
        summary['publicApi']={'preserved':251,'addedApproved':21,'total':272}
        counts={};cumulative_names=set()
        for assembly,expected in [('Xemele.Tests',expected_runtime),('Xemele.Conformance.Tests',255),('Xemele.Differential.Tests',299)]:
            discovery=run('dotnet','test','--project',ROOT/f'tests/{assembly}','-c','Release','--no-build','--no-restore','--list-tests')
            (report/(assembly+'-discovery.txt.gz')).write_bytes(gzip.compress(discovery.encode(),mtime=0))
            discoveries=re.findall(r'Discovered (\d+) tests\.',discovery);assert discoveries and int(discoveries[-1])==expected
            xml=raw/(assembly+'.xml')
            run('dotnet',ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll','-noColor','-result-xml',xml)
            rows=list(ET.parse(xml).iter('test'));assert len(rows)==expected and all(t.get('result')=='Pass' for t in rows)
            counts[assembly]={'total':len(rows),'failed':0,'skipped':0}
            (report/(assembly+'.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(),mtime=0))
            if assembly=='Xemele.Tests':cumulative_names={(t.get('type'),t.get('method'),t.get('name')) for t in rows}
        assert {(t.get('type'),t.get('method'),t.get('name')) for t in cases}.issubset(cumulative_names)
        summary['directAndDiscoveryTests']=counts
        registry=json.loads(run('python3','-B','tools/ApiBaseline/discover.py'));assert registry['fixtures']==3388 and registry['apiDeclarations']==272
        summary['registry']={'fixtures':3388,'apiDeclarations':272}
        records=[json.loads(f.read_text()) for f in (raw/'case-results').glob('reference-*.json')]
        assert len(records)==242 and len({r['id'] for r in records})==242
        assert hashlib.sha256(pinned.read_bytes()).hexdigest()==runner_hash
        for record in records:
            assert record['runnerSha256']==runner_hash and record['response']['engine']['build']=='1.0.0+'+revision
        summary['referenceProvenance']={'records':242,'runnerSha256':runner_hash,'build':'1.0.0+'+revision,'freshCompleteCopy':True,'unchangedCopy':True}
        run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v')
        validation=run('python3','-B','tools/planning/tasks.py','validate');summary['planningValidation']=validation.strip()
        paths=[]
        for cmd in [('git','diff','--name-only',a.audit_base+'..HEAD'),('git','diff','--name-only'),('git','diff','--cached','--name-only'),('git','ls-files','--others','--exclude-standard')]:paths+=run(*cmd).splitlines()
        owned=task['owns']+['docs/tasks/M2-entities-foundation.json']
        assert all(any(path.startswith(own) if own.endswith('/') else path==own for own in owned) for path in paths),paths
        for suffix in [[a.audit_base+'..HEAD'],['--cached'],[]]:run('git','diff','--check',*suffix)
        summary['ownedPaths']=sorted(set(paths));summary['result']='passed'
        summary['scope']='Options and immutable metadata/manual optional callback only. No production DOCTYPE/entity parsing, callback scheduling, resolver access, or new timed workload. No dependency timing repeat. Windows/x64/AOT unrun.'
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0))
        (report/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
if __name__=='__main__':main()
