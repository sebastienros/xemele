"""Owned and affected correctness; no allocation counters or timing probes."""
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

D = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
FLAGS = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'artifacts/M8-xsd-publication-checkpoints/checks'
OUT.mkdir(parents=True, exist_ok=True)

def run(*args):
    subprocess.run(args, cwd=ROOT, check=True)

def build(project, *extra):
    run(D, 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *FLAGS, *extra)
    run(D, 'build', project, '-c', 'Release', '--no-restore', *FLAGS, *extra)

def verify(path):
    tree = ET.parse(path)
    assembly = tree.getroot().find('assembly')
    assert all(int(assembly.attrib[k]) == 0 for k in ['errors', 'failed', 'skipped', 'not-run']), assembly.attrib
    tests = list(tree.iter('test'))
    assert len(tests) == int(assembly.attrib['passed']) and tests
    assert all(t.attrib['result'] == 'Pass' and 'WarmBorrowed' not in t.attrib['name'] for t in tests)
    print(f'ASSERTED {path.name}: {len(tests)} passed', flush=True)

build('tests/Xemele.Tests/Xemele.Tests.csproj')
prefix = [D, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
for label, namespaces in [
    ('owned', ['M8XsdPublicationCheckpoints']),
    ('affected', ['M8Model', 'M8SourceLocations', 'M8SchemaResourceBase', 'M8XsdCompile', 'M8XsdEffectiveContent', 'M8XsdWildcardComposition', 'M8XsdContent', 'M8XsdValidate', 'M8DatatypesCore', 'M8Datatypes', 'M8Facets', 'M8ConstraintLexical']),
]:
    args = list(prefix)
    for ns in namespaces:
        args += ['-namespace', 'Xemele.Tests.' + ns]
    path = OUT / (label + '.xml')
    run(*args, '-parallelMode', 'none', '-noLogo', '-noColor', '-result-xml', str(path))
    verify(path)
resolver = next(item['args'] for item in json.loads((ROOT / 'docs/reports/M8-resource-kind/test-commands.json').read_text()) if item['label'] == 'resolver')
resolver = resolver[:resolver.index('-result-xml')] + ['-result-xml', str(OUT / 'resolver.xml')]
assert not any('WarmBorrowed' in arg for arg in resolver)
run(*resolver)
verify(OUT / 'resolver.xml')
for project in ['docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj', 'docs/reports/M8-model/ApiCheck/ApiCheck.csproj']:
    build(project)
run('python3', 'docs/reports/M5-validation-model/check_api.py')
for baseline in ['docs/reports/M8-xsd-compile/compiler.golden.txt', 'tests/Contracts/Fixtures/M8/Model.golden.txt']:
    run(D, '--fx-version', '10.0.11', 'docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll', 'compare', 'src/Xemele/bin/Release/net10.0/Xemele.dll', baseline)
for extra in [[], ['-p:XsdXmlOnly=true']]:
    build('samples/M8-xsd-publication-checkpoints/Consumer.csproj', *extra)
    run(D, '--fx-version', '10.0.11', 'samples/M8-xsd-publication-checkpoints/bin/Release/net10.0/Xemele.Benchmarks.dll')
run('python3', 'tools/planning/tasks.py', 'validate')
run('git', 'diff', '--check')
