#!/usr/bin/env python3
"""Offline external package, trim, native XML integration checks; retains logs."""
import hashlib,json,os,shutil,signal,subprocess,tempfile,zipfile
from pathlib import Path
root=Path(__file__).resolve().parents[3]
art=root/'artifacts/M7-gate/package';art.mkdir(parents=True,exist_ok=True)
host='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
cache=Path('/Users/sebastienros/.nuget/packages')
work=Path(tempfile.mkdtemp(prefix='xemele-m7-package-',dir='/private/tmp'))
feed=work/'feed';feed.mkdir();consumer=work/'consumer';consumer.mkdir();packages=work/'packages'
assert not packages.exists()
commands=[]
def run(label,args,cwd=root,timeout=120,separate=False):
    commands.append({'label':label,'args':[str(a) for a in args],'cwd':str(cwd)})
    stdout_path=art/(label+('.stdout' if separate else '.log'))
    with stdout_path.open('wb') as log, (art/(label+'.stderr')).open('wb') as error:
        p=subprocess.Popen([str(a) for a in args],cwd=cwd,stdout=log,stderr=error if separate else subprocess.STDOUT,start_new_session=True)
        try:
            code=p.wait(timeout=timeout)
        finally:
            # This process owns exactly this new session/process group. Cleanup
            # runs on timeout, interruption, any exception and ordinary exit.
            try: os.killpg(p.pid,signal.SIGTERM)
            except ProcessLookupError: pass
            try: p.wait(timeout=10)
            except subprocess.TimeoutExpired:
                try: os.killpg(p.pid,signal.SIGKILL)
                except ProcessLookupError: pass
                p.wait(timeout=10)
            try: os.killpg(p.pid,signal.SIGKILL)
            except ProcessLookupError: pass
    assert code==0,(label,code)
    return stdout_path.read_bytes()

try:
    shutil.copy(root/'global.json',work/'global.json')
    assert run('external-sdk',[host,'--version'],consumer).strip()==b'10.0.400'
    # Cached .nupkg files are explicit local-feed platform/compiler inputs.
    for nupkg in cache.rglob('*.nupkg'):
        target=feed/nupkg.name
        if not target.exists():target.symlink_to(nupkg)
    run('pack',[host,'pack','src/Xemele/Xemele.csproj','-c','Release','--no-build','--no-restore','-p:PackageVersion=0.7.0-integration','-o',feed,'--disable-build-servers','-m:1'])
    package=feed/'Xemele.0.7.0-integration.nupkg'
    with zipfile.ZipFile(package) as archive: packaged=archive.read('lib/net10.0/Xemele.dll')
    source=(root/'src/Xemele/bin/Release/net10.0/Xemele.dll').read_bytes()
    assert packaged==source
    shutil.copy(root/'docs/reports/M7-gate/Consumer.csproj.template',consumer/'Consumer.csproj')
    shutil.copy(root/'docs/reports/M7-gate/Consumer.cs.template',consumer/'Program.cs')
    shutil.copy(root/'tests/Xemele.Conformance.Tests/Integration/ToolkitScenario.cs',consumer/'ToolkitScenario.cs')
    assert not any('ProjectReference' in p.read_text() for p in consumer.glob('*.csproj'))
    common=['-p:NuGetAudit=false','--source',feed,'--packages',packages,'--disable-build-servers','-m:1']
    run('package-restore',[host,'restore','Consumer.csproj',*common],consumer)
    assert (packages/'xemele/0.7.0-integration/lib/net10.0/Xemele.dll').read_bytes()==source
    build=['-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1']
    run('package-build',[host,'build','Consumer.csproj',*build],consumer)
    expected=(root/'tests/corpora/xinclude/expected.txt').read_bytes()+b'\n'+(root/'docs/reports/M7-gate/consumer-expected.txt').read_bytes()
    def execute(label,args):
        actual=run(label,args,consumer,60,True)
        assert (art/(label+'.stderr')).read_text().strip()=='Framework=.NET 10.0.11; Architecture=Arm64'
        assert actual==expected,(label,actual)
    execute('package-managed',[host,'--fx-version','10.0.11',consumer/'bin/Release/net10.0/Consumer.dll',root/'tests/corpora'])
    for lane,prop in [('trim','PublishTrimmed'),('aot','PublishAot')]:
        run(lane+'-restore',[host,'restore','Consumer.csproj','-r','osx-arm64',f'-p:{prop}=true',*common],consumer)
        output=work/lane
        run(lane+'-publish',[host,'publish','Consumer.csproj','-c','Release','--no-restore','--disable-build-servers','-m:1','-t:Rebuild,Publish','-r','osx-arm64','--self-contained','true',f'-p:{prop}=true','-o',output],consumer,180)
        execute(lane,[output/'Consumer',root/'tests/corpora'])
        if lane=='aot':
            info=subprocess.run(['file',str(output/'Consumer')],capture_output=True,text=True,check=True).stdout
            (art/'aot-file.txt').write_text(info)
            assert 'Mach-O 64-bit executable arm64' in info and not (output/'Consumer.dll').exists()
    (art/'package-identity.json').write_text(json.dumps({'work':str(work),'package':str(package),'sha256':hashlib.sha256(source).hexdigest(),'source_package_dll_equal':True,'initial_consumer_cache_absent':True,'offline_local_feed':str(feed),'actual_native_platform':'osx-arm64'},indent=2))
finally:
    (art/'package-commands.json').write_text(json.dumps(commands,indent=2))
    (art/'external-work-path.txt').write_text(str(work)+'\n')
print('PASS: external package, trimmed and native osx-arm64 XML composition; '+str(work))
