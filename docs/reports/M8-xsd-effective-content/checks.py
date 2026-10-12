import subprocess, json, xml.etree.ElementTree as E
from pathlib import Path
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(*args): subprocess.run(args,check=True)
def build(project,*extra):
 run(net,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags,*extra)
 run(net,'build',project,'-c','Release','--no-restore',*flags,*extra)
def verify(path,count):
 a=E.parse(path).getroot().find('assembly'); assert int(a.attrib['passed'])==count and all(int(a.attrib[k])==0 for k in ['failed','errors','skipped','not-run']),a.attrib
 print('ASSERTED',path,count,flush=True)
out=Path('artifacts/M8-xsd-effective-content/checks');out.mkdir(parents=True,exist_ok=True)
build('tests/Xemele.Tests/Xemele.Tests.csproj')
prefix=[net,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
for name,classes,count in [
 ('focused',['Xemele.Tests.M8XsdEffectiveContent.MappingTests'],28),
 ('wildcards',['Xemele.Tests.M8XsdWildcardComposition.CompositionTests'],11),
 ('resourceBase',['Xemele.Tests.M8SchemaResourceBase.ResourceBaseTests'],11),
 ('rng',['Xemele.Tests.M8RngCompile.CompilerTests','Xemele.Tests.M8RngCompile.ResourceTests'],88),
 ('xsd',['Xemele.Tests.M8XsdCompile.CompilerTests','Xemele.Tests.M8XsdCompile.CompositionTests','Xemele.Tests.M8XsdCompile.LocationTests'],72),
 ('prerequisites',['Xemele.Tests.M8Model.ModelTests','Xemele.Tests.M8Model.ResourceTests','Xemele.Tests.M8SourceLocations.SourceLocationTests'],30)]:
 args=[]
 for c in classes:args+=['-class',c]
 path=out/(name+'.xml');run(*prefix,*args,'-result-xml',str(path));verify(path,count)
a=json.load(open('docs/reports/M8-resource-kind/test-commands.json'))[1]['args'];a[-1]=str(out/'resolver.xml');run(*a);verify(out/'resolver.xml',42)
build('docs/reports/M8-rng-compile/ApiCheck/ApiCheck.csproj')
run(net,'--fx-version','10.0.11','docs/reports/M8-rng-compile/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll','docs/reports/M8-rng-compile/frozen.txt')
build('docs/reports/M8-model/ApiCheck/ApiCheck.csproj')
run(net,'--fx-version','10.0.11','docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll','docs/reports/M8-xsd-compile/compiler.golden.txt')
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj');run('python3','docs/reports/M5-validation-model/check_api.py')
for project,property in [('samples/M8-rng-compile/Consumer.csproj','RngXmlOnly'),('samples/M8-xsd-compile/Xsd.Consumer.csproj','XsdXmlOnly')]:
 build(project,'-p:'+property+'=true')
 run(net,'--fx-version','10.0.11',str(Path(project).parent/'bin/Release/net10.0/Xemele.Benchmarks.dll'))
run('python3','tools/planning/tasks.py','validate');run('git','diff','--check')
