"""Freeze prepared inputs after accepted source/verified compiler repair; no measurements."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[3]
if len(sys.argv)!=2 or len(sys.argv[1])!=40: raise SystemExit('Usage: prepare.py <full accepted runtime/source revision>')
revision=sys.argv[1]
subprocess.run(['git','merge-base','--is-ancestor',revision,'HEAD'],cwd=root,check=True)
tests=ET.parse(root/'artifacts/M8-xsd-content/correctness.xml').getroot()
assembly=tests.find('assembly')
assert int(assembly.attrib['passed'])==243 and all(int(assembly.attrib[k])==0 for k in ['failed','errors','skipped','not-run'])
required=[t for t in tests.iter('test') if 'NonExpressibleIntermediateMustBeRejectedBeforeCompilerDiscardsIt' in t.attrib['name']]
assert len(required)==1 and required[0].attrib['result']=='Pass', 'Verified compiler repair regression must pass before preparation.'
effective_empty=[t for t in tests.iter('test') if 'EffectiveEmptyExtensionInheritsMixedBaseContent' in t.attrib['name']]
assert len(effective_empty)==2 and all(t.attrib['result']=='Pass' for t in effective_empty), 'Both corrected effective-empty mapping positives must pass before preparation.'
control=json.loads((root/'artifacts/M8-xsd-content/control.json').read_text())
assert control['mode']=='--control' and control['runtime']=='.NET 10.0.11' and len(control['rows'])==10
assert all(row['operations']==64 and row['bytes']==0 and row['distribution']==[0]*64 for row in control['rows'])
assert all(control[key]==0 for key in ['coldSetupBytes','positiveBytes','retainedBefore','retainedAfterPrograms','retainedAfterCursors'])
# These zeros explicitly verify disabled control instrumentation; they are NOT allocation evidence.
commands=json.loads(subprocess.check_output([sys.executable,str(root/'docs/reports/M8-xsd-content/measure.py'),'--commands'],cwd=root,text=True))
paths=set()
for relative in ['benchmarks/Xemele.Benchmarks/M8-xsd-content','src/Xemele/Validation/Xsd/ContentModels','src/Xemele/Validation/Xsd/Compilation','tests/Xemele.Tests/M8-xsd-content']:
 folder=root/relative
 for path in folder.rglob('*'):
  if path.is_file() and not any(part in ('bin','obj') for part in path.relative_to(folder).parts):paths.add(path)
project=root/'docs/reports/M8-xsd-content/XmlOnly/XmlOnly.csproj'
for item in ET.parse(project).getroot().iter('Compile'):
 pattern=item.attrib['Include'];absolute=project.parent/pattern
 import glob
 paths.update(Path(p) for p in glob.glob(str(absolute),recursive=True) if Path(p).is_file())
for relative in ['benchmarks/Xemele.Benchmarks/M8-xsd-content/bin/probe/net10.0','benchmarks/Xemele.Benchmarks/M8-xsd-content/bin/Release/net10.0']:
 paths.update(path for path in (root/relative).rglob('*') if path.is_file())
for relative in ['src/Xemele/Xemele.csproj','src/Xemele/bin/Release/net10.0/Xemele.dll','Directory.Build.props','Directory.Packages.props','global.json',
 'docs/reports/M8-xsd-content/measure.py','docs/reports/M8-xsd-content/prepare.py','docs/reports/M8-xsd-content/content.golden.txt',
 'docs/reports/M8-xsd-content/XmlOnly/XmlOnly.csproj','samples/M8-xsd-content/Content.Consumer.csproj','samples/M8-xsd-content/Program.cs',
 'artifacts/M8-xsd-content/control.json']:
 paths.add(root/relative)
net=Path(commands[0][0]);paths.add(net)
runtime=net.parent/'shared/Microsoft.NETCore.App/10.0.11'
for name in ['System.Private.CoreLib.dll','System.Runtime.dll','libcoreclr.dylib','libclrjit.dylib']:
 paths.add(runtime/name)
def digest(path):
 with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
assert control['assemblySha256'].lower()==digest(root/'src/Xemele/bin/Release/net10.0/Xemele.dll')
assert control['probeSha256'].lower()==digest(root/'benchmarks/Xemele.Benchmarks/M8-xsd-content/bin/probe/net10.0/Xemele.Benchmarks.dll')
identities={str(path.resolve()):digest(path.resolve()) for path in sorted(paths)}
manifest={'sourceRevision':revision,'sdk':'10.0.400','runtime':'10.0.11','architecture':control['architecture'],
 'maximumSeconds':600,'workSeconds':590,'cleanupSeconds':10,
 'ownership':'M8-xsd-content owned compiler/cursor/name/setup/growth/diagnostics and real XML-only composition; no other actor measurements',
 'commands':commands,'identities':identities,
 'controls':['10 allocation-probe semantic routes:32warm/64samples with all allocation/retention counters and timing disabled',
 '5 prebuilt BDN semantic methods consumed without counters/timing/BDN','exactly5MemoryDiagnoserShortRun InProcessNoEmit cases discovered; no generated compilation',
 'warm primitive candidates separate from owned cursor/setup/bank-growth/program/diagnostic and retained costs; synchronous counter paths only'],
 'environment':{'DOTNET_ROLL_FORWARD':'Disable'},
 'execution':'Root grant required: --granted SHA latest-start-UTC expiry-UTC. Explicit latest-start and expiry are validated before creating output or children; an existing measurement directory is refused. Same elevated ps permission is preflighted before any child. Fixed two commands, no builds/retries; absolute UTC grant expiry bounds600second supervisor; durable RELEASE precedes result inspection.'}
p=root/'docs/reports/M8-xsd-content/preparation.json';p.write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({'identities':len(identities),'manifestSha256':digest(p),'measurementStarted':False}))
