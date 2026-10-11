"""Focused correctness only. No timing, retention or allocation probes."""
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path
NET = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
FLAGS = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
def run(*args): subprocess.run(args, check=True)
def build(project, *extra):
    run(NET, 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *FLAGS, *extra)
    run(NET, 'build', project, '-c', 'Release', '--no-restore', *FLAGS, *extra)
build('tests/Xemele.Tests/Xemele.Tests.csproj')
out = Path('artifacts/M8-datatypes-temporal/correctness/focused.xml')
out.parent.mkdir(parents=True, exist_ok=True)
run(NET, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll',
    '-class', 'Xemele.Tests.M8DatatypesTemporal.TemporalTests', '-class', 'Xemele.Tests.M8DatatypesCore.CoreTests',
    '-class', 'Xemele.Tests.M8Model.ModelTests', '-result-xml', str(out))
r = ET.parse(out).getroot(); a = r.find('assembly')
assert int(a.attrib['passed']) == 52 and all(int(a.attrib[k]) == 0 for k in ['failed', 'errors', 'skipped', 'not-run']), a.attrib
assert sum(t.attrib['type'] == 'Xemele.Tests.M8DatatypesTemporal.TemporalTests' for t in r.iter('test')) == 22
assert '10.0.11' in a.attrib['environment']
print('ASSERTED:22 temporal+18 core+12 model=52 passed; zero failures/skips/not-run; no allocation tests')
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj')
run('python3', 'docs/reports/M5-validation-model/check_api.py')
project = 'docs/reports/M8-datatypes-temporal/Consumer.csproj'
build(project)
run(NET, '--fx-version', '10.0.11', 'docs/reports/M8-datatypes-temporal/bin/Release/net10.0/Xemele.Benchmarks.dll')
build(project, '-p:TemporalXmlOnly=true')
run(NET, '--fx-version', '10.0.11', 'docs/reports/M8-datatypes-temporal/bin/Release/net10.0/Xemele.Benchmarks.dll', '--xml-only')
run('python3', 'tools/planning/tasks.py', 'validate')
run('git', 'diff', '--check')
