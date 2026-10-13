"""Preparation-only local package inputs. No runtime executable launch."""
import sys
sys.dont_write_bytecode=True
import hashlib,json,os,shutil,time,zipfile
from pathlib import Path
from recipes import ROOT,HERE,PACKAGE_WORK as WORK
CACHE=Path('/Users/sebastienros/.nuget/packages')
GENERATED_ID='Xemele'
GENERATED_VERSION='0.8.0-integration'
GENERATED_FILENAME=f'{GENERATED_ID}.{GENERATED_VERSION}.nupkg'

def sha(p):
 with p.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def digest(value):return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=True).encode('utf-8')).hexdigest()
def remaining(deadline):
 if time.monotonic()>=deadline:raise TimeoutError('Offline catalog immutable work deadline exhausted')
def archive_identity(path,deadline):
 remaining(deadline)
 before=path.stat();hasher=hashlib.sha256()
 with path.open('rb') as stream:
  while block:=stream.read(1024*1024):
   remaining(deadline);hasher.update(block)
 after=path.stat()
 assert (before.st_dev,before.st_ino,before.st_size,before.st_mtime_ns)==(after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns), 'Archive changed while binding'
 return {'bytes':after.st_size,'sha256':hasher.hexdigest()}

def preflight_catalog(cache,deadline):
 """Bind exact global-cache coordinates and bytes; never choose among differing archives.

 No NuGet version normalization is inferred. ID/version are exact directory
 components; canonical basenames match that coordinate under casefold().
 """
 cache=Path(cache).resolve();assert cache.is_dir(),'Offline cache missing'
 remaining(deadline)
 files=sorted(p for p in cache.rglob('*') if p.is_file() and p.suffix.casefold()=='.nupkg')
 coordinates={};targets={}
 for source in files:
  remaining(deadline)
  relative=source.relative_to(cache)
  assert len(relative.parts)==3,'Unexpected archive path: '+str(relative)
  package_id,version,filename=relative.parts
  assert package_id not in ['', '.', '..'] and version not in ['', '.', '..'], 'Identity/path mismatch'
  assert source.resolve().is_relative_to(cache), 'Archive source escapes cache'
  expected=f'{package_id}.{version}.nupkg';target=expected.casefold()
  assert target!=GENERATED_FILENAME.casefold(),'Generated integration package identity already cached'
  coordinate=(package_id,version)
  assert target not in targets or targets[target]==coordinate,'Casefold canonical target conflict: '+target
  targets[target]=coordinate
  row=coordinates.setdefault(coordinate,{'id':package_id,'version':version,'target':target,'canonical':[], 'aliases':[]})
  if filename.casefold()==target:row['canonical'].append(source)
  else:
   assert filename.casefold()==f'{package_id}.nupkg'.casefold(),'Identity/path mismatch: '+str(relative)
   row['aliases'].append(source)
 entries=[];alias_rows=[]
 for coordinate,row in sorted(coordinates.items(),key=lambda pair:pair[1]['target']):
  remaining(deadline)
  assert len(row['canonical'])==1,'Missing/ambiguous canonical peer: '+row['target']
  canonical=row['canonical'][0];identity=archive_identity(canonical,deadline)
  entry={'id':row['id'],'version':row['version'],'target':row['target'],'canonicalSourcePath':str(canonical),**identity}
  for alias in sorted(row['aliases']):
   alias_identity=archive_identity(alias,deadline)
   assert alias_identity==identity,'Conflicting alias archive bytes: '+str(alias)
   alias_rows.append({'source':str(alias),'canonicalSourcePath':str(canonical),**alias_identity})
  entries.append(entry)
 assert entries,'Empty offline canonical catalog'
 remaining(deadline)
 return {'entries':entries,'aliases':alias_rows,'archiveCount':len(files),'canonicalCount':len(entries),'aliasCount':len(alias_rows),
         'canonicalCatalogSha256':digest(entries),'aliasIdentitySha256':digest(alias_rows),'reservedIntegrationIdentity':GENERATED_FILENAME.casefold()}

def verify_catalog_sources(catalog,deadline):
 for row in catalog['entries']:
  assert archive_identity(Path(row['canonicalSourcePath']),deadline)=={'bytes':row['bytes'],'sha256':row['sha256']}, 'Canonical archive changed after preflight'
 for row in catalog['aliases']:
  assert archive_identity(Path(row['source']),deadline)=={'bytes':row['bytes'],'sha256':row['sha256']}, 'Alias changed after preflight'

def expected_linker_host_binding():
 pins_path=HERE/'linker-inputs.json';template_path=HERE/'LinkerHost.py.template'
 pins=json.loads(pins_path.read_text());private=WORK/pins['privateToolRelative']
 for role in ['dotnet','python']:
  assert sha(Path(pins[role]['path']))==pins[role]['sha256'],'Pinned linker host executable drift'
 assert Path(sys.executable).resolve()==Path(pins['python']['path']).resolve(),'Layout requires pinned Python'
 canonical=Path(pins['canonicalToolDirectory'])
 assert all(sha(canonical/name)==value for name,value in pins['files'].items()),'Canonical linker source drift'
 assert sha(Path(pins['installedTargets']['path']))==pins['installedTargets']['sha256'],'Original linker targets changed'
 private_files={str(private/name):value for name,value in pins['files'].items()}
 replacements={'@PYTHON_SHEBANG@':pins['python']['path'],'@DOTNET_PATH@':json.dumps(pins['dotnet']['path']),
  '@DOTNET_SHA@':json.dumps(pins['dotnet']['sha256']),'@PYTHON_PATH@':json.dumps(pins['python']['path']),
  '@PYTHON_SHA@':json.dumps(pins['python']['sha256']),'@EXPECTED_DLL@':json.dumps(str(private/'illink.dll')),
  '@EXPECTED_FILES@':json.dumps(private_files,sort_keys=True)}
 assert not any(char in pins['python']['path'] for char in [' ', '\n', '\r']),'Exact executable shebang path required'
 rendered=template_path.read_text()
 for token,value in replacements.items():rendered=rendered.replace(token,value)
 assert not any(token in rendered for token in replacements),'Unrendered host binding token'
 binding={'wrapperPath':str(WORK/'consumer/linker-host/dotnet'),'wrapperSha256':hashlib.sha256(rendered.encode('utf-8')).hexdigest(),
  'wrapperMode':0o755,'templateSha256':sha(template_path),'inputPinsSha256':sha(pins_path),
  'consumerTemplateSha256':sha(HERE/'Consumer.csproj.template'),'dotnet':pins['dotnet'],'python':pins['python'],
  'expectedDllPath':str(private/'illink.dll'),'expectedDllSha256':pins['files']['illink.dll'],
  'privateToolFiles':private_files,'canonicalToolFiles':{str(canonical/name):value for name,value in pins['files'].items()},
  'runtime':'10.0.11','rollForward':'Disable','arguments':'Original linker args forwarded unchanged after exact --fx-version10.0.11',
  'operation':'Original ILLink ToolTask; no new session/children before exec'}
 return rendered,binding

def create_linker_host():
 rendered,binding=expected_linker_host_binding();path=Path(binding['wrapperPath'])
 path.parent.mkdir(exist_ok=False);path.write_text(rendered);path.chmod(binding['wrapperMode'])
 assert sha(path)==binding['wrapperSha256']
 return binding

def verify_linker_host(require_private=True):
 rendered,expected=expected_linker_host_binding()
 layout=json.loads((WORK/'layout.json').read_text())
 assert layout['linkerHost']==expected,'Prepared linker binding changed'
 wrapper=Path(expected['wrapperPath'])
 assert wrapper.read_bytes()==rendered.encode('utf-8') and sha(wrapper)==expected['wrapperSha256'],'Rendered linker host drift'
 assert wrapper.stat().st_mode & 0o777==expected['wrapperMode'],'Linker host executable mode drift'
 assert (WORK/'consumer/Consumer.csproj').read_bytes()==(HERE/'Consumer.csproj.template').read_bytes(),'Consumer host-selection target drift'
 if require_private:
  assert all(sha(Path(name))==value for name,value in expected['privateToolFiles'].items()),'Actual private linker input drift'
 return expected

def dll_equal():
 with zipfile.ZipFile(WORK/'feed'/GENERATED_FILENAME) as z:
  assert z.read('lib/net10.0/Xemele.dll')==(ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll').read_bytes()
def main():
 phase=sys.argv[1];assert sys.argv[1:]==[phase]
 if phase=='layout':
  assert not WORK.exists(),'Fresh initially absent external package directory required'
  deadline=float(os.environ['XEMELE_GATE_WORK_DEADLINE'])
  # Complete fail-closed preflight before creating the external layout or any feed link.
  catalog=preflight_catalog(CACHE,deadline)
  (WORK/'feed').mkdir(parents=True);(WORK/'consumer').mkdir()
  assert not (WORK/'packages').exists()
  shutil.copyfile(ROOT/'global.json',WORK/'global.json')
  for row in catalog['entries']:
   remaining(deadline);target=WORK/'feed'/row['target'];assert not target.exists() and not target.is_symlink()
   target.symlink_to(Path(row['canonicalSourcePath']))
  assert not (WORK/'feed'/GENERATED_FILENAME).exists() and not (WORK/'feed'/GENERATED_FILENAME).is_symlink()
  for source,target in [(HERE/'Consumer.csproj.template','Consumer.csproj'),(HERE/'Consumer.cs.template','Program.cs'),
                       (ROOT/'tests/Xemele.Conformance.Tests/Integration/ToolkitScenario.cs','ToolkitScenario.cs')]:
   shutil.copyfile(source,WORK/'consumer'/target)
  assert all('ProjectReference' not in p.read_text() for p in (WORK/'consumer').glob('*.csproj'))
  linker_host=create_linker_host()
  verify_catalog_sources(catalog,deadline)
  remaining(deadline)
  # Complete bindings stay active in memory; retain concise count/digest evidence only.
  summary={key:catalog[key] for key in ['archiveCount','canonicalCount','aliasCount','canonicalCatalogSha256','aliasIdentitySha256','reservedIntegrationIdentity']}
  (WORK/'layout.json').write_text(json.dumps({'initialCacheAbsent':True,'published':False,'offlineFeed':str(WORK/'feed'),'catalog':summary,'linkerHost':linker_host},indent=2)+'\n')
 elif phase=='package':dll_equal()
 elif phase=='freeze':
  dll_equal()
  linker_host=verify_linker_host()
  assert (WORK/'packages/xemele/0.8.0-integration/lib/net10.0/Xemele.dll').read_bytes()==(ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll').read_bytes()
  for assets in (WORK/'consumer/obj').rglob('project.assets.json'):
   data=json.loads(assets.read_text());assert set(map(lambda p:str(Path(p).resolve()),data['packageFolders']))=={str((WORK/'packages').resolve())}
   assert all('http' not in source for source in data['project']['restore']['sources'])
  assert not (WORK/'aot/Consumer.dll').exists()
  magic=(WORK/'aot/Consumer').read_bytes()[:8]
  assert magic[:4]==b'\xcf\xfa\xed\xfe' and int.from_bytes(magic[4:8],'little')==0x0100000c,'Actual arm64 Mach-O required'
  layout=json.loads((WORK/'layout.json').read_text());assert layout['initialCacheAbsent'] and not layout['published']
  (WORK/'package-identity.json').write_text(json.dumps({'packageSha256':sha(WORK/'feed'/GENERATED_FILENAME),
   'productSha256':sha(ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll'),'packageDllEqual':True,'initialConsumerCacheAbsent':True,
   'projectReference':False,'offlineOnly':True,'published':False,'nativeArchitecture':'osx-arm64','consumerSourceSha256':sha(WORK/'consumer/Program.cs'),
   'offlineCatalog':layout['catalog'],'linkerHost':linker_host},indent=2)+'\n')
 else:raise ValueError(phase)
 print('PASS package preparation '+phase)
if __name__=='__main__':main()
