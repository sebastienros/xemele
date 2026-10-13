"""Preparation-only local package inputs. No runtime executable launch."""
import sys
sys.dont_write_bytecode=True
import hashlib,json,shutil,zipfile
from pathlib import Path
from recipes import ROOT,HERE,PACKAGE_WORK as WORK
CACHE=Path('/Users/sebastienros/.nuget/packages')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def dll_equal():
 with zipfile.ZipFile(WORK/'feed/Xemele.0.8.0-integration.nupkg') as z:
  assert z.read('lib/net10.0/Xemele.dll') == (ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll').read_bytes()
def main():
 phase=sys.argv[1]; assert sys.argv[1:] == [phase]
 if phase=='layout':
  assert not WORK.exists(), 'Fresh initially absent external package directory required'
  (WORK/'feed').mkdir(parents=True); (WORK/'consumer').mkdir()
  assert not (WORK/'packages').exists()
  shutil.copyfile(ROOT/'global.json',WORK/'global.json')
  for package in sorted(CACHE.rglob('*.nupkg')):
   target=WORK/'feed'/package.name
   assert not target.exists(), 'Ambiguous local package filename'
   target.symlink_to(package)
  for source,target in [(HERE/'Consumer.csproj.template','Consumer.csproj'),(HERE/'Consumer.cs.template','Program.cs'),
                       (ROOT/'tests/Xemele.Conformance.Tests/Integration/ToolkitScenario.cs','ToolkitScenario.cs')]:
   shutil.copyfile(source,WORK/'consumer'/target)
  assert all('ProjectReference' not in p.read_text() for p in (WORK/'consumer').glob('*.csproj'))
  (WORK/'layout.json').write_text(json.dumps({'initialCacheAbsent':True,'published':False,'offlineFeed':str(WORK/'feed')})+'\n')
 elif phase=='package': dll_equal()
 elif phase=='freeze':
  dll_equal()
  assert (WORK/'packages/xemele/0.8.0-integration/lib/net10.0/Xemele.dll').read_bytes()==(ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll').read_bytes()
  for assets in (WORK/'consumer/obj').rglob('project.assets.json'):
   d=json.loads(assets.read_text());assert set(map(lambda p:str(Path(p).resolve()),d['packageFolders']))=={str((WORK/'packages').resolve())}
   assert all('http' not in source for source in d['project']['restore']['sources'])
  assert not (WORK/'aot/Consumer.dll').exists()
  magic=(WORK/'aot/Consumer').read_bytes()[:8]
  assert magic[:4]==b'\xcf\xfa\xed\xfe' and int.from_bytes(magic[4:8],'little')==0x0100000c, 'Actual arm64 Mach-O required'
  (WORK/'package-identity.json').write_text(json.dumps({'packageSha256':sha(WORK/'feed/Xemele.0.8.0-integration.nupkg'),
   'productSha256':sha(ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll'),'packageDllEqual':True,'initialConsumerCacheAbsent':True,
   'projectReference':False,'offlineOnly':True,'published':False,'nativeArchitecture':'osx-arm64','consumerSourceSha256':sha(WORK/'consumer/Program.cs')},indent=2)+'\n')
 else: raise ValueError(phase)
 print('PASS package preparation '+phase)
if __name__=='__main__':main()
