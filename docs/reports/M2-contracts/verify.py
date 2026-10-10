"""Independent M2 declaration/model checks, with preserved M1 runtime checks."""
import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT/'docs/reports/M2-contracts'
FIXTURE = ROOT/'tests/Contracts/Fixtures/M2'
FLAGS = ['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']
LANES = ['Resolver','Dtd','Encoding','Entities','Recovery']
CHECKER = ROOT/'docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'
CLAIM = 'd89824f96523c2caa65645d5f1892c04a767a409'

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report-prefix',required=True)
    parser.add_argument('--emit-reviewed-model-baselines',action='store_true')
    parser.add_argument('--models-only',action='store_true')
    parser.add_argument('--audit-base',default=CLAIM)
    parser.add_argument('--expected-branch',default='codex/m2-contracts')
    args = parser.parse_args()
    if not re.fullmatch('[a-z0-9-]+',args.report_prefix): parser.error('invalid report prefix')
    report = REPORT/args.report_prefix; report.mkdir()
    raw = ROOT/'artifacts/M2-contracts'/args.report_prefix; raw.mkdir(parents=True)
    dotnet = Path(shutil.which('dotnet') or 'dotnet-unavailable').resolve(strict=True)
    runner = ROOT/'tools/ReferenceRunners/Bcl/bin/Release/net10.0/BclReference.dll'
    env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', PATH=str(dotnet.parent)+os.pathsep+os.environ.get('PATH',''), XEMELE_DOTNET=str(dotnet),
        XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',
        XEMELE_CORPUS_RESULTS=str(raw/'case-results'),
        XEMELE_BCL_RUNNER=str(runner))
    logs=[]; summary={'result':'failed','host':platform.platform(),'architecture':platform.machine(), 'baselinePreparation':args.emit_reviewed_model_baselines}
    def run(*command, expected=0):
        command=list(map(str,command))
        if command[0]=='dotnet':command[0]=str(dotnet)
        print('$ '+shlex.join(command),flush=True)
        result=subprocess.run(command,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=300)
        logs.append('$ '+shlex.join(command)+'\n'+result.stdout+f'\nexit={result.returncode}; expected={expected}\n')
        print(result.stdout[-1200:],flush=True)
        assert result.returncode==expected, logs[-1]
        return result.stdout
    def project(lane): return REPORT/f'Projects/{lane}/{lane}.csproj'
    def dll(lane): return REPORT/f'Projects/{lane}/bin/Release/net10.0/M2{lane}.dll'
    def rows(path): return {r['member']:r['declaration'] for r in map(json.loads,path.read_text().splitlines()[1:])}
    try:
        revision=run('git','rev-parse','HEAD').strip();summary['sourceRevision']=revision
        run('git','merge-base','--is-ancestor',CLAIM,'HEAD')
        run('git','merge-base','--is-ancestor','8c9bd349ed4156ea0295b5160d87d39434293bb2','HEAD')
        assert run('git','branch','--show-current').strip()==args.expected_branch
        run('git','merge-base','--is-ancestor',args.audit_base,'HEAD')
        summary['auditBase']=args.audit_base;summary['expectedBranch']=args.expected_branch
        assert json.loads((ROOT/'docs/tasks/M1-gate.json').read_text())['status']=='verified'
        assert run('dotnet','--version').strip()=='10.0.400'
        run('dotnet','build','docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj',*FLAGS)
        if not args.emit_reviewed_model_baselines:
            source_paths=['docs/specs/common.md','docs/specs/parser.md','docs/specs/integration.md','docs/decisions/M2','tests/Contracts/Fixtures/M2','docs/reports/M2-contracts/Projects','docs/reports/M2-contracts/verify.py']
            assert not run('git','diff','--name-only','HEAD','--',*source_paths).strip(),'contract source must be committed before verification'
            assert not run('git','ls-files','--others','--exclude-standard','--',*source_paths).strip(),'untracked contract source'
        model_counts={}
        for lane in LANES:
            run('dotnet','build',project(lane),*FLAGS)
            golden=FIXTURE/(lane+'.golden.txt')
            run('dotnet',CHECKER,'emit' if args.emit_reviewed_model_baselines else 'compare',dll(lane),golden)
            model_counts[lane]=len(rows(golden))
        common=rows(ROOT/'tests/Contracts/Fixtures/M1/Common.golden.txt')
        for lane in LANES:
            after=rows(FIXTURE/(lane+'.golden.txt'))
            assert all(after.get(k)==v for k,v in common.items()),lane+' changed common baseline'
        foundation=rows(ROOT/'tests/Contracts/Fixtures/foundation/M2.golden.txt')
        after=rows(FIXTURE/'Recovery.golden.txt')
        selected={k:v for k,v in foundation.items() if any(k.startswith(prefix) for prefix in ['T:Xemele.XmlResource','M:Xemele.XmlResource','P:Xemele.XmlResource','F:Xemele.XmlResource','T:Xemele.XmlResolved','M:Xemele.XmlResolved','P:Xemele.XmlResolved','T:Xemele.XmlDtd','F:Xemele.XmlDtd'])}
        assert selected and all(after.get(k)==v for k,v in selected.items()),'foundation M2 resolver ABI changed'
        summary['models']=model_counts;summary['preservedCommon']=len(common);summary['preservedFoundationResourceMembers']=len(selected)
        run('dotnet','build',project('Checks'),*FLAGS)
        assertions=run('dotnet',dll('Checks')); summary['modelAssertions']=int(re.search(r'Passed (\d+)',assertions)[1])
        negatives=[
            ('Dtd','no-resolver','class Probe { Xemele.XmlResourceResolver? resolver; }','CS0234'),
            ('Resolver','no-dtd','class Probe { Xemele.Parsing.Dtd.DtdModel? model; }','CS0234'),
            ('Dtd','no-tree','class Probe { Xemele.XmlDocument? document; }','CS0234'),
            ('Dtd','no-decoder','class Probe { Xemele.Parsing.Input.XmlInputDecoder? decoder; }','CS0234'),
            ('Dtd','immutable-declaration','class Probe { void Use(Xemele.Parsing.Dtd.XmlTextDeclaration declaration) { declaration.EncodingName = "UTF-8"; } }','CS8852'),
            ('Recovery','no-async','class Probe { void Use() { _ = Xemele.XmlDocument.ParseWithDiagnosticsAsync("<r/>"); } }','CS0117'),
            ('Resolver','read-only-uri','class Probe { void Use(Xemele.XmlResolvedResource resource) { resource.SourceUri = new System.Uri("https://fixture.invalid/"); } }','CS0200'),
            ('Dtd','immutable-model','class Probe { void Use(Xemele.Parsing.Dtd.DtdModel model) { model.GeneralEntities = []; } }','CS0200'),
            ('Entities','borrowed-core-view','class Probe { Xemele.Parsing.Syntax.XmlNameView retained; }','CS8345'),
            ('Recovery','reference-result','class Probe { Xemele.XmlParseResult<int>? result; }','CS0452'),
            ('Resolver','token-required','class Probe { void Use(Xemele.XmlResourceResolver resolver, Xemele.XmlResourceRequest request) { resolver.Resolve(request); } }','CS7036'),
        ]
        with tempfile.TemporaryDirectory(prefix='xemele-m2-negative-') as directory:
            for lane,name,source,code in negatives:
                probe=Path(directory)/(name+'.cs');probe.write_text(source)
                output=run('dotnet','build',project(lane),*FLAGS,f'-p:ProbeSource={probe}',expected=1)
                assert code in output,(name,code)
            # A real compiled metadata mutation must fail the approved normalizer.
            mutation=Path(directory)/'Mutation.cs'; mutation.write_text('namespace Xemele; public sealed class UnexpectedPublicContract { }')
            run('dotnet','build',project('Resolver'),*FLAGS,f'-p:ProbeSource={mutation}')
            run('dotnet',CHECKER,'compare',dll('Resolver'),FIXTURE/'Resolver.golden.txt',expected=1)
        for lane in LANES:
            run('dotnet','build',project(lane),*FLAGS)
            run('dotnet',CHECKER,'compare',dll(lane),FIXTURE/(lane+'.golden.txt'))
        summary['negativeCompilationProbes']=len(negatives);summary['metadataMutationProbes']=1
        if not args.models_only:
            run('python3','-B','docs/reports/M1-corpus-import/provision.py')
            run('dotnet','build','tools/ReferenceRunners/Bcl/BclReference.csproj',*FLAGS)
            runner_hash=hashlib.sha256(runner.read_bytes()).hexdigest()
            # Historical verifier writes only ignored artifacts, preserves all old model/golden files.
            run('python3','-B','docs/reports/M1-contracts/verify.py')
            run('python3','-B','tools/ApiBaseline/compare.py','--report',report/'runtime-api.json')
            counts={}
            for assembly,expected in [('Xemele.Tests',247),('Xemele.Conformance.Tests',255),('Xemele.Differential.Tests',299)]:
                xml=raw/(assembly+'.xml')
                run('dotnet',ROOT/f'tests/{assembly}/bin/Release/net10.0/{assembly}.dll','-noColor','-result-xml',xml)
                tests=list(ET.parse(xml).iter('test'))
                assert len(tests)==expected and all(t.get('result')=='Pass' for t in tests),(assembly,len(tests))
                counts[assembly]={'total':len(tests),'failed':0,'skipped':0}
                (report/(assembly+'.xml.gz')).write_bytes(gzip.compress(xml.read_bytes(),mtime=0))
            summary['runtimeTests']=counts
            records=[json.loads(p.read_text()) for p in (raw/'case-results').glob('reference-*.json')]
            assert len(records)==242 and len({r['id'] for r in records})==242
            assert hashlib.sha256(runner.read_bytes()).hexdigest()==runner_hash,'fresh reference runner changed'
            for record in records:
                assert record['runnerSha256']==runner_hash,'reference runner hash is stale'
                assert record['response']['engine']['build']=='1.0.0+'+revision,'reference adapter source revision is stale'
            summary['referenceProvenance']={'records':242,'runner':str(runner),'runnerSha256':runner_hash,'expectedBuild':'1.0.0+'+revision,'dotnet':str(dotnet),'dotnetSha256':hashlib.sha256(dotnet.read_bytes()).hexdigest()}
            (report/'reference-provenance.json').write_text(json.dumps({'sourceRevision':revision,'records':[{'id':r['id'],'runnerSha256':r['runnerSha256'],'build':r['response']['engine']['build']} for r in sorted(records,key=lambda r:r['id'])]},indent=2)+'\n')
        else: summary['runtimeTests']='unrun during model-baseline preparation'
        run('python3','-B','-m','unittest','discover','-s','tools/planning','-p','test_tasks.py','-v')
        run('python3','-B','tools/planning/tasks.py','validate')
        task=json.loads((ROOT/'docs/tasks/M2-contracts.json').read_text());owned=task['owns']+['docs/tasks/M2-contracts.json']
        paths=[]
        for command in [('git','diff','--name-only',args.audit_base+'..HEAD'),('git','diff','--name-only'),('git','diff','--cached','--name-only'),('git','ls-files','--others','--exclude-standard')]: paths.extend(run(*command).splitlines())
        assert all(any(path.startswith(own) if own.endswith('/') else path==own for own in owned) for path in paths),paths
        for suffix in [args.audit_base+'..HEAD',None,'--cached']:run('git','diff','--check',*([suffix] if suffix else []))
        summary['ownedPaths']=sorted(set(paths));summary['fixtureSha256']={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(FIXTURE.glob('*')) if p.is_file()}
        summary['result']='passed';summary['scope']='Nonpackaged M2 declarations/models; preserved M1 actual runtime only. M2 parser/resource/encoding/recovery behavior unimplemented.'
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0))
        (report/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')

if __name__=='__main__':main()
