import subprocess,json,xml.etree.ElementTree as E
from pathlib import Path
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
def run(*args):subprocess.run(args,check=True)
def build(project,*extra):
 run(net,'restore',project,'--source','/Users/sebastienros/.nuget/packages',*flags,*extra)
 run(net,'build',project,'-c','Release','--no-restore',*flags,*extra)
def verify(path,count):
 a=E.parse(path).getroot().find('assembly');assert int(a.attrib['passed'])==count and all(int(a.attrib[k])==0 for k in ['failed','errors','skipped','not-run']),a.attrib
 print('ASSERTED',path,count,flush=True)
out=Path('artifacts/M8-sch-compile/integration');out.mkdir(parents=True,exist_ok=True)
build('tests/Xemele.Tests/Xemele.Tests.csproj')
prefix=[net,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
run(*prefix,'-class','Xemele.Tests.M8SchCompile.CompilerTests','-class','Xemele.Tests.M8SchCompile.ResourceTests','-result-xml',str(out/'focused.xml'));verify(out/'focused.xml',93)
run(*prefix,'-class','Xemele.Tests.M8SchemaResourceBase.ResourceBaseTests','-result-xml',str(out/'resource-base.xml'));verify(out/'resource-base.xml',11)
a=json.load(open('docs/reports/M8-resource-kind/test-commands.json'))[1]['args'];a[-1]=str(out/'resolver.xml');run(*a);verify(out/'resolver.xml',42)
a=[]
for c in ['Xemele.Tests.M8Model.ModelTests','Xemele.Tests.M8Model.ResourceTests','Xemele.Tests.M8SourceLocations.SourceLocationTests','Xemele.Tests.M5ValidationModel.ValidationModelTests','Xemele.Tests.M4Evaluate.EvaluationTests']:a+=['-class',c]
run(*prefix,*a,'-result-xml',str(out/'prerequisites.xml'));verify(out/'prerequisites.xml',131)
build('docs/reports/M8-sch-compile/ApiCheck/ApiCheck.csproj')
run(net,'--fx-version','10.0.11','docs/reports/M8-sch-compile/ApiCheck/bin/Release/net10.0/ApiCheck.dll','compare','src/Xemele/bin/Release/net10.0/Xemele.dll','docs/reports/M8-sch-compile/frozen.txt')
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj');run('python3','docs/reports/M5-validation-model/check_api.py')
build('samples/M8-sch-compile/Consumer.csproj','-p:SchXmlOnly=true')
run(net,'--fx-version','10.0.11','samples/M8-sch-compile/bin/Release/net10.0/Xemele.Benchmarks.dll')
run('python3','tools/planning/tasks.py','validate');run('git','diff','--check')
