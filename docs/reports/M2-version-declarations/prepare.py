"""Prepare frozen standalone boundaries and independent BDN children; never measure."""
import hashlib, json, os, platform, shutil, signal, subprocess, sys
from pathlib import Path
from measure import validate_variant, case_id, expected_cases
ROOT=Path(__file__).resolve().parents[3]
REPORT=ROOT/'docs/reports/M2-version-declarations'
RAW=REPORT/'.measurement'
FLAGS=['-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false']

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def closure(path): return {str(f.relative_to(path)):digest(f) for f in sorted(path.rglob('*')) if f.is_file()}
def main():
    summary={'result':'failed','measured':False,'architecture':platform.machine(),'commands':[]}
    env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0')
    def run(*args):
        command=list(map(str,args))
        child=subprocess.Popen(command,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,start_new_session=True)
        try: output,_=child.communicate(timeout=300)
        except subprocess.TimeoutExpired:
            os.killpg(child.pid,signal.SIGKILL);child.communicate();raise
        summary['commands'].append({'command':command,'exit':child.returncode})
        if child.returncode: raise RuntimeError(str(command)+': '+output[-2000:])
        return output
    try:
        assert not RAW.exists(),'Preparation directory already exists; do not overwrite frozen preparation.'
        revision=run('git','rev-parse','HEAD').strip();summary['revision']=revision
        assert run('git','branch','--show-current').strip()=='codex/m2-version-declarations'
        names=run('git','ls-files','--','src/Xemele','samples/M2-version-declarations','benchmarks/Xemele.Benchmarks','docs/reports/M2-version-declarations','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.config').splitlines()
        names=[n for n in names if Path(n).suffix in ('.cs','.csproj','.props','.targets','.config','.py') or n=='global.json']
        sources={n:digest(ROOT/n) for n in names}
        for n in names: assert (ROOT/n).read_bytes()==subprocess.check_output(['git','show',revision+':'+n],cwd=ROOT)
        summary['sourceIdentities']=sources
        RAW.mkdir();build=RAW/'build'
        assert run('dotnet','--version').strip()=='10.0.400'
        summary['sdk']={'version':'10.0.400','hostSha256':digest(Path(shutil.which('dotnet')).resolve()),'runtimes':run('dotnet','--list-runtimes').splitlines()}
        for project in ['samples/M2-version-declarations/Sample.csproj','docs/reports/M2-version-declarations/BenchmarkDriver.csproj','docs/reports/M2-version-declarations/BuildOnly.csproj']:
            run('dotnet','build',project,*FLAGS,'--artifacts-path',build)
        sample=build/'bin/Sample/release/Xemele.SyntaxSample.dll'
        benchmark=build/'bin/BenchmarkDriver/release/Xemele.Benchmarks.dll'
        generator=build/'bin/BuildOnly/release/VersionBuildOnly.dll'
        summary['sampleOracles']=[run('dotnet',sample,mode).strip() for mode in ['--oracle','--boundary-oracle']]
        summary['benchmarkOracle']=run('dotnet',benchmark,'--oracle').strip()
        summary['sampleIdentity']=json.loads(run('dotnet',sample,'--identity'))
        assert summary['sampleIdentity']['Runtime']=='10.0.11'
        assert all(summary['sampleIdentity'][key]['FullVersion']=='1.0.0+'+revision for key in ['Product','Probe'])
        summary['generatorOutput']=run('dotnet',generator,benchmark,RAW/'generated',RAW/'generation.json').strip().splitlines()[-1]
        generated=json.loads((RAW/'generation.json').read_text());assert len(generated)==1 and generated[0]['cases']==14 and generated[0]['built']
        child=Path(generated[0]['binaries']);project=Path(generated[0]['project'])
        summary['generated']=generated[0]
        summary['generatedInputIdentities']={str(f.relative_to(project.parent)):digest(f) for f in sorted(project.parent.iterdir()) if f.is_file() and f.suffix in ('.cs','.notcs','.csproj','.config','.sh','.bat')}
        summary['childClosure']=closure(child)
        assert len(summary['childClosure']) == 47 and 'Xemele.dll' in summary['childClosure'] and 'Xemele.Benchmarks.dll' in summary['childClosure']
        summary['caseIdentities'] = generated[0]['identities']
        assert len(summary['caseIdentities']) == 14
        generated_project = project.read_text()
        assert str(ROOT/'benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj') in generated_project
        summary['generatedSourceBinding'] = 'Actual generated project references frozen main benchmark source tree; independent generated child is a legitimate variant, compared by exact complete raw closure.'
        summary['childMetadata']={name:json.loads(run('dotnet',sample,'--file-identity',child/name)) for name in ['Xemele.dll','Xemele.Benchmarks.dll',Path(generated[0]['executable']).name]}
        for name in summary['childMetadata']:
            assert summary['childMetadata'][name]['FullVersion']=='1.0.0+'+revision
            assert summary['childMetadata'][name]['Sha256']==summary['childClosure'][name]
        # The generated child is separately frozen; exclude only its explicitly
        # named build subtree from the primary runtime closure (not its siblings).
        summary['generatedSubtree'] = project.parent.name
        summary['primaryClosures']={name:{n:h for n,h in closure(build/f'bin/{name}/release').items() if name != 'BenchmarkDriver' or not n.startswith(summary['generatedSubtree']+'/')} for name in ['Sample','BenchmarkDriver','BuildOnly']}
        summary['generatorIdentity'] = json.loads(run('dotnet',sample,'--file-identity',generator))
        lifecycle = json.loads(run('dotnet',generator,'--lifecycle-oracle',child/'Xemele.Benchmarks.dll'))
        assert lifecycle['mode'] == 'lifecycle-oracle' and len(lifecycle['cases']) == 14 and all(c['iterations'] == 1 and c['allocatedBytes'] == 0 for c in lifecycle['cases'])
        assert lifecycle['product'] == summary['childMetadata']['Xemele.dll'] and lifecycle['benchmark'] == summary['childMetadata']['Xemele.Benchmarks.dll'] and lifecycle['probe'] == summary['generatorIdentity'] and lifecycle['runtime'] == '10.0.11' and lifecycle['architecture'] == 'Arm64'
        assert {case_id(c['identity']) for c in lifecycle['cases']} == expected_cases()
        validate_variant(summary,summary['childClosure'],summary['generatedInputIdentities'],summary['childMetadata'],revision)
        summary['generatedLifecycleOracle'] = lifecycle

        assert sources=={n:digest(ROOT/n) for n in names}
        summary['sourceFreezeUnchanged']=True;summary['result']='ready-and-parked'
    except Exception as error:
        summary['failure']={'type':type(error).__name__,'message':str(error)}
        try:
            if RAW.exists():shutil.rmtree(RAW)
            summary['temporaryOutputsRemoved']=not RAW.exists()
        except Exception as cleanup:summary['cleanupFailure']=str(cleanup)
    (REPORT/'preparation-summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    print(json.dumps({k:summary[k] for k in ['result','revision','failure','sampleOracles','benchmarkOracle','generatorOutput','sourceFreezeUnchanged'] if k in summary},indent=2),flush=True)
    return 0 if summary['result']=='ready-and-parked' else 1
if __name__=='__main__':sys.exit(main())
