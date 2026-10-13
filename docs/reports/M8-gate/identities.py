"""Read-only closure membership/bytes guards. No product execution on import."""
import sys
sys.dont_write_bytecode=True
import hashlib,json,os,subprocess
from pathlib import Path
from recipes import ROOT,HERE,NET,BINARY_FOLDERS,PACKAGE_WORK as WORK

def sha(path):
 with Path(path).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def digest(value):return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def tree(folder):
 folder=Path(folder);assert folder.is_dir(),str(folder)
 return {str(p.relative_to(folder)):sha(p) for p in sorted(folder.rglob('*')) if p.is_file()}
def source_identity():
 files=[]
 for base in ['src','tests','tools','samples','benchmarks','docs/api']:
  files += [p for p in (ROOT/base).rglob('*') if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.parts)
            and not (base=='tests' and ('xmlconf' in p.parts or 'upstream' in p.parts or p.name in ['source.bundle.json','xmlts20130923.tar.gz']))]
 reports=['M8-gate','M7-gate','M8-cli','M0-contracts/ApiCheck','M8-contracts/ApiCheck','M8-xsd-resolved-availability/harness',
          'M1-corpus-import','M2-conformance-profile','M2-conformance-grammar','M4-corpus','M7-corpus']
 for base in reports:
  files += [p for p in (ROOT/'docs/reports'/base).rglob('*') if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.parts)
            and not p.name.endswith('.receipt.json') and p.name not in ['prepared.json','results.json','source-packet.json','handoff.md']]
 for name in ['global.json','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','NuGet.config','NuGet.Config',
              'Xemele.slnx','.editorconfig','docs/reports/M8-xsd-corpus/supervision.py','docs/reports/M8-xsd-corpus/nested_ownership.py',
              'docs/reports/M8-xsd-resolved-availability/verified-runtime-api.json','docs/reports/M8-xsd-resolved-availability/verified-added.golden.txt',
              'docs/reports/M8-xsd-compile/compiler.golden.txt','docs/reports/M8-xsd-resolved-availability/added-joins.golden.txt',
              'docs/reports/M5-validation-model/check_api.py','tools/planning/tasks.py']:
  if (ROOT/name).is_file():files.append(ROOT/name)
 rows={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))}
 return {'files':len(rows),'sha256':digest(rows),'rows':rows,
         'configurationPresence':{name:(ROOT/name).is_file() for name in ['Directory.Build.targets','NuGet.config','NuGet.Config']}}
def runtime_identity():
 installation=Path(NET).parent
 dirs=[installation/'shared/Microsoft.NETCore.App/10.0.11',installation/'sdk/10.0.400',installation/'host/fxr',
       Path('/Users/sebastienros/.nuget/packages'),Path('/Library/Java/JavaVirtualMachines/temurin-25.jdk/Contents/Home')]
 # Expanded platform, compiler, linker and cache inputs are bound, not only the host/runner DLL.
 return {'executables':{str(p):sha(p.resolve()) for p in [Path(NET),Path(sys.executable),Path('/usr/bin/git'),Path('/bin/ps'),Path('/usr/bin/file'),Path('/usr/bin/xcrun')]},
         'directories':{str(p):{'files':len(rows),'sha256':digest(rows)} for p in dirs for rows in [tree(p)]},
         'references':reference_identity(),'nativeToolchain':native_identity()}
def reference_identity():
 d=json.loads((ROOT/'docs/reports/M8-rng-corpus/reference-build.json').read_text())
 values={d['jing']['jarPath']:d['jing']['jarSha256'],d['libxml2']['binaryPath']:d['libxml2']['binarySha256']}
 values.update({str(Path(d['jing']['classPath'])/name):value for name,value in d['jing']['runnerClassHashes'].items()})
 assert all(sha(p)==value for p,value in values.items()),'Pinned existing reference binary drift/missing'
 return values
def corpus_resources():
 inventory=json.loads((ROOT/'tests/corpora/xml/inventory.json').read_text())
 assert len(inventory['files'])==3386 and sum(row['bytes'] for row in inventory['files'])==2722978
 assert len(inventory['cases'])==len({row['id'] for row in inventory['cases']})==2585
 expected={row['path']:row['sha256'] for row in inventory['files']}
 actual={str(p.relative_to(ROOT/'tests/corpora/xml')):sha(p) for p in (ROOT/'tests/corpora/xml/xmlconf').rglob('*') if p.is_file()}
 assert actual==expected,'Every XML DTD/entity/original resource membership/hash must match'
 assert sha(ROOT/'tests/corpora/xml/source.bundle.json')=='c67aa290f2491d23d160242e8184c81b616366e6a345e510020050414b24bd5e'
 e14=[r for r in inventory['files'] if r['path'].endswith('/E14.dtd')];assert len(e14)==1 and e14[0]['sha256']=='acfca92db2a079a319237acf2334acd215aa4c609a344e066f97fe6fd138adab'
 archive=Path('/private/tmp/xemele-corpus-cache/w3c-xml-20130923/xmlts20130923.tar.gz')
 assert archive.stat().st_size==641522 and sha(archive)=='9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f'
 return {'files':3386,'bytes':2722978,'cases':2585,'resourceMembershipSha256':digest(actual),'caseIdsSha256':digest(sorted(row['id'] for row in inventory['cases'])),
         'sourceBundleSha256':sha(ROOT/'tests/corpora/xml/source.bundle.json'),'e14Sha256':e14[0]['sha256'],'archiveSha256':sha(archive)}
def package_identity():
 package=json.loads((WORK/'package-identity.json').read_text())
 assert sha(WORK/'feed/Xemele.0.8.0-integration.nupkg')==package['packageSha256'], 'Actual nupkg archive mutated after freeze'
 assert sha(ROOT/'src/Xemele/bin/Release/net10.0/Xemele.dll')==package['productSha256'], 'Actual source/API product DLL drift'
 from package_inputs import verify_linker_host
 assert verify_linker_host()==package['linkerHost'], 'Frozen linker host/tool identity drift'
 return package

def binaries():
 package=package_identity()
 folders=[ROOT/p for p in BINARY_FOLDERS]+[WORK/'consumer/bin/Release/net10.0',WORK/'trim',WORK/'aot']
 values={str(p.relative_to(ROOT)):{'files':len(rows),'sha256':digest(rows),'rows':rows} for p in folders for rows in [tree(p)]}
 for group in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']:
  framework=json.loads((ROOT/f'tests/{group}/bin/Release/net10.0/{group}.runtimeconfig.json').read_text())['runtimeOptions']['framework']
  assert framework=={'name':'Microsoft.NETCore.App','version':'10.0.11'},framework
 return {'directories':values,'xmlResources':corpus_resources(),'schemaResources':schema_resources(),'package':package}

def native_identity():
 developer=Path('/var/db/xcode_select_link').resolve()
 assert developer==Path('/Applications/Xcode.app/Contents/Developer'), 'Reviewed Xcode selection changed'
 toolchain=developer/'Toolchains/XcodeDefault.xctoolchain'
 sdk=developer/'Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk'
 files={str(p):sha(p.resolve()) for p in [toolchain/'usr/bin/clang',toolchain/'usr/bin/ld',toolchain/'usr/bin/ar',toolchain/'ToolchainInfo.plist',sdk/'SDKSettings.json']}
 directories={str(p.resolve()):{'files':len(rows),'sha256':digest(rows)} for p in [toolchain/'usr',sdk] for rows in [tree(p.resolve())]}
 links={str(p):str(p.resolve()) for base in [toolchain,sdk] for p in base.rglob('*') if p.is_symlink()}
 return {'developer':str(developer),'clang':str((toolchain/'usr/bin/clang').resolve()),'linker':str((toolchain/'usr/bin/ld').resolve()),
         'sdk':str(sdk.resolve()),'files':files,'directories':directories,'symlinkTargetsSha256':digest(links)}

def schema_resources():
 ledger=json.loads((ROOT/'tests/corpora/xsd/resources.json').read_text())
 assert len(ledger)==42270 and len({row['path'] for row in ledger})==42270
 actual=tree(ROOT/'tests/corpora/xsd/upstream');expected={row['path']:row['sha256'] for row in ledger}
 assert actual==expected, 'Complete original XSD resource membership/bytes changed'
 assert all((ROOT/'tests/corpora/xsd/upstream'/row['path']).stat().st_size==row['bytes'] for row in ledger)
 external=json.loads((ROOT/'tests/corpora/xsd/external-resources.json').read_text())
 assert all(sha(ROOT/'tests/corpora/xsd'/row['path'])==row['sha256'] for row in external)
 return {'files':42270,'bytes':sum(row['bytes'] for row in ledger),'identitySha256':digest(actual),'externalPinsSha256':digest(external)}
