#!/usr/bin/env python3
"""Offline external package, trim, native XML integration checks; retains logs."""
import hashlib,json,os,shutil,signal,subprocess,tempfile,zipfile
from pathlib import Path
root=Path(__file__).resolve().parents[3]
art=root/'artifacts/M6-integration';art.mkdir(parents=True,exist_ok=True)
host='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
cache=Path('/Users/sebastienros/.nuget/packages')
work=Path(tempfile.mkdtemp(prefix='xemele-m6-package-',dir='/private/tmp'))
feed=work/'feed';feed.mkdir();consumer=work/'consumer';consumer.mkdir();packages=work/'packages'
assert not packages.exists()
commands=[]
def run(label,args,cwd=root,timeout=120):
    commands.append({'label':label,'args':[str(a) for a in args],'cwd':str(cwd)})
    with (art/(label+'.log')).open('wb') as log:
        p=subprocess.Popen([str(a) for a in args],cwd=cwd,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
        try: code=p.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            os.killpg(p.pid,signal.SIGTERM)
            try:p.wait(timeout=10)
            except subprocess.TimeoutExpired:os.killpg(p.pid,signal.SIGKILL);p.wait()
            raise
    assert code==0,(label,code)
    return (art/(label+'.log')).read_bytes()
try:
    # Cached .nupkg files are explicit local-feed platform/compiler inputs.
    for nupkg in cache.rglob('*.nupkg'):
        target=feed/nupkg.name
        if not target.exists():target.symlink_to(nupkg)
    run('pack',[host,'pack','src/Xemele/Xemele.csproj','-c','Release','--no-build','--no-restore','-p:PackageVersion=0.6.0-integration','-o',feed,'--disable-build-servers','-m:1'])
    package=feed/'Xemele.0.6.0-integration.nupkg'
    with zipfile.ZipFile(package) as archive: packaged=archive.read('lib/net10.0/Xemele.dll')
    source=(root/'src/Xemele/bin/Release/net10.0/Xemele.dll').read_bytes()
    assert packaged==source
    shutil.copy(root/'tests/PackageSmoke/Xml/Consumer.csproj',consumer/'Consumer.csproj')
    shutil.copy(root/'samples/XmlToolkit/Program.cs',consumer/'Program.cs')
    shutil.copy(root/'tests/Xemele.Conformance.Tests/Integration/ToolkitScenario.cs',consumer/'ToolkitScenario.cs')
    assert not any('ProjectReference' in p.read_text() for p in consumer.glob('*.csproj'))
    common=['-p:NuGetAudit=false','--source',feed,'--packages',packages,'--disable-build-servers','-m:1']
    run('package-restore',[host,'restore','Consumer.csproj',*common],consumer)
    assert (packages/'xemele/0.6.0-integration/lib/net10.0/Xemele.dll').read_bytes()==source
    build=['-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1']
    run('package-build',[host,'build','Consumer.csproj',*build],consumer)
    expected=(root/'tests/corpora/xinclude/expected.txt').read_bytes()+b'\n'
    def execute(label,args):
        p=subprocess.run([str(a) for a in args],cwd=consumer,capture_output=True,timeout=60)
        (art/(label+'.stdout')).write_bytes(p.stdout);(art/(label+'.stderr')).write_bytes(p.stderr)
        assert p.returncode==0 and p.stdout==expected,(label,p.returncode,p.stdout,p.stderr)
    execute('package-managed',[host,'--fx-version','10.0.11',consumer/'bin/Release/net10.0/Consumer.dll',root/'tests/corpora'])
    for lane,prop in [('trim','PublishTrimmed'),('aot','PublishAot')]:
        run(lane+'-restore',[host,'restore','Consumer.csproj','-r','osx-arm64',f'-p:{prop}=true',*common],consumer)
        output=work/lane
        run(lane+'-publish',[host,'publish','Consumer.csproj',*build,'-r','osx-arm64','--self-contained','true',f'-p:{prop}=true','-o',output],consumer,180)
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
