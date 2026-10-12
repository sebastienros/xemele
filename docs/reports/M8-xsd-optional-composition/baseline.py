"""Original correctness observations only; no counters or network."""
import os,subprocess,json,xml.etree.ElementTree as E
from pathlib import Path
D='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags=['--disable-build-servers','-m:1','-p:RuntimeFrameworkVersion=10.0.11','-p:UseSharedCompilation=false','-p:NuGetAudit=false']
out=Path('artifacts/M8-xsd-optional-composition');out.mkdir(parents=True,exist_ok=True)
commands=[]
def run(args,**kw):
 r=subprocess.run(args,timeout=180,**kw);commands.append(dict(command=args,exit=r.returncode));r.check_returncode()
run([D,'restore','tests/Xemele.Tests/Xemele.Tests.csproj','--source','/Users/sebastienros/.nuget/packages',*flags])
run([D,'build','tests/Xemele.Tests/Xemele.Tests.csproj','-c','Release','--no-restore',*flags])
run([D,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-class','Xemele.Tests.M8XsdOptionalComposition.OriginalTests','-parallelMode','none','-noLogo','-noColor','-result-xml',str(out/'baseline.xml')],env=dict(os.environ,OPTIONAL_BASELINE='1'))
tests=list(E.parse(out/'baseline.xml').getroot().iter('test'));assert len(tests)==5 and all(t.get('result')=='Pass' for t in tests)
Path('docs/reports/M8-xsd-optional-composition/original-baseline.json').write_text(json.dumps(dict(revision=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),compiler_sha256=__import__('hashlib').sha256(Path('src/Xemele/Validation/Xsd/Compilation/Compiler.cs').read_bytes()).hexdigest(),commands=commands,observations=[dict(identity=t.get('name'),result=t.get('result'),output=t.findtext('output')) for t in tests],meaning='Four original schemas refuse SCHEMA.ResourceDenied; instances unassessed, never passing validity. Fifth row pins unchanged bytes/metadata/copyright.'),indent=2)+'\n')
