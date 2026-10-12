"""Correctness only; no allocation counters or timing assertions."""
import json, subprocess, xml.etree.ElementTree as ET
from pathlib import Path
root = Path(__file__).resolve().parents[3]
net = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
def run(*args): subprocess.run(args, cwd=root, check=True)
def build(project, *extra):
 run(net, 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *flags, *extra)
 run(net, 'build', project, '-c', 'Release', '--no-restore', *flags, *extra)
build('tests/Xemele.Tests/Xemele.Tests.csproj')
out = root / 'artifacts/M8-rng-validate/correctness/focused.xml'; out.parent.mkdir(parents=True, exist_ok=True)
run(net, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll', '-class', 'Xemele.Tests.M8RngValidate.*', '-result-xml', str(out))
a = ET.parse(out).getroot().find('assembly')
assert all(int(a.attrib[k]) == 0 for k in ['failed', 'errors', 'skipped', 'not-run']), a.attrib
assert int(a.attrib['passed']) == 89 and '10.0.11' in a.attrib['environment'], a.attrib
print('ASSERTED owned counts:', a.attrib)
arguments = []
for name in ['M8RngCompile.*', 'M8Facets.*', 'M8Datatypes*', 'M8Regex.*', 'M8Model.*', 'M8SchemaResourceBase.*', 'M8SourceLocations.*', 'M4Evaluate.*', 'M5ValidationModel.*', 'DtdValidation.*']:
 arguments += ['-class', 'Xemele.Tests.' + name]
predecessors = root / 'artifacts/M8-rng-validate/correctness/predecessors.xml'
run(net, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll', *arguments, '-result-xml', str(predecessors))
b = ET.parse(predecessors).getroot().find('assembly')
assert int(b.attrib['passed']) == 1191 and all(int(b.attrib[k]) == 0 for k in ['failed', 'errors', 'skipped', 'not-run']), b.attrib
print('ASSERTED predecessor counts:', b.attrib['passed'])
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj')
run('python3', 'docs/reports/M5-validation-model/check_api.py')
build('docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj')
run('python3', 'docs/reports/M8-rng-validate/frozen.py')
consumer = 'samples/M8-rng-validate/Consumer.csproj'
build(consumer); run(net, '--fx-version', '10.0.11', 'samples/M8-rng-validate/bin/Release/net10.0/Consumer.dll')
build(consumer, '-p:RngXmlOnly=true'); run(net, '--fx-version', '10.0.11', 'samples/M8-rng-validate/bin/Release/net10.0/Consumer.dll')
run('python3', 'tools/planning/tasks.py', 'validate'); run('git', 'diff', '--check')
