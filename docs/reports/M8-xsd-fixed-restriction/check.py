"""Task correctness and compatibility only; never execute allocation counters."""
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[3]
net = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
flags = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
out = root / 'artifacts/M8-xsd-fixed-restriction/checks'
out.mkdir(parents=True, exist_ok=True)

def run(label, args):
    result = subprocess.run(args, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (out / (label + '.log')).write_text(result.stdout)
    if result.returncode:
        raise AssertionError(f'{label}: exit {result.returncode}\n{result.stdout}')
    lines = result.stdout.strip().splitlines()
    print(f'PASS {label}: ' + (lines[-1] if lines else 'exit 0'), flush=True)
    return result.stdout

def build(label, project, extra=()):
    run(label + '-restore', [net, 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *flags, *extra])
    run(label + '-build', [net, 'build', project, '-c', 'Release', '--no-restore', *flags, *extra])

build('tests', 'tests/Xemele.Tests/Xemele.Tests.csproj')
namespaces = ['M8XsdFixedRestriction', 'M8XsdContent', 'M8XsdCompile', 'M8XsdValidate', 'M8XsdIdentity', 'M8Facets',
              'M8Datatypes', 'M8DatatypesCore', 'M8DatatypesNumeric', 'M8DatatypesTextBinary', 'M8DatatypesTemporal',
              'M8ConstraintLexical', 'M8Model', 'M8SourceLocations', 'M8XsdWildcardComposition', 'M8XsdEffectiveContent']
args = [net, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll']
for namespace in namespaces:
    args += ['-namespace', 'Xemele.Tests.' + namespace]
args += ['-method-', '*WarmBorrowed*', '-parallelMode', 'none', '-noLogo', '-noColor', '-result-xml', str(out / 'tests.xml')]
run('correctness', args)
assembly = ET.parse(out / 'tests.xml').getroot().find('assembly')
assert all(int(assembly.attrib[key]) == 0 for key in ['errors', 'failed', 'skipped', 'not-run']), assembly.attrib
assert '10.0.11' in assembly.attrib['environment']
assert all('WarmBorrowed' not in test.attrib['name'] for test in assembly.iter('test'))
counts = {}
for test in assembly.iter('test'):
    assert test.attrib['result'] == 'Pass'
    namespace = test.attrib['type'].rsplit('.', 1)[0]
    counts[namespace] = counts.get(namespace, 0) + 1
print('PASS correctness counts: ' + json.dumps(counts, sort_keys=True), flush=True)

build('public-checker', 'docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj')
run('public-api', ['python3', 'docs/reports/M5-validation-model/check_api.py'])
build('internal-checker', 'docs/reports/M8-model/ApiCheck/ApiCheck.csproj')
for name, path in [('content', 'docs/reports/M8-xsd-content/content.golden.txt'),
                   ('model', 'tests/Contracts/Fixtures/M8/Model.golden.txt'),
                   ('compiler', 'docs/reports/M8-xsd-compile/compiler.golden.txt'),
                   ('identity', 'docs/reports/M8-xsd-identity/identity.golden.txt'),
                   ('evaluation', 'docs/reports/M8-xsd-validate/evaluation.golden.txt')]:
    run('frozen-' + name, [net, '--fx-version', '10.0.11', 'docs/reports/M8-model/ApiCheck/bin/Release/net10.0/ApiCheck.dll', 'compare',
                           'src/Xemele/bin/Release/net10.0/Xemele.dll', path])
build('whole-internal-checker', 'docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj')
run('internal-emit', [net, '--fx-version', '10.0.11', 'docs/reports/M8-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll',
                      'emit', 'src/Xemele/bin/Release/net10.0/Xemele.dll', str(out / 'internal.txt')])
def rows(path):
    return {row['id']: row for row in (json.loads(line) for line in Path(path).read_text().splitlines()[1:])}
expected = rows(root / 'tests/Contracts/Fixtures/M8/Datatypes.golden.txt')
actual = rows(out / 'internal.txt')
assert all(actual.get(key) == value for key, value in expected.items())
print(f'PASS {len(expected)} frozen datatype public/internal rows', flush=True)

resolver = next(row['args'] for row in json.loads((root / 'docs/reports/M8-resource-kind/test-commands.json').read_text()) if row['label'] == 'resolver')
resolver = resolver[:resolver.index('-result-xml')] + ['-result-xml', str(out / 'resolver.xml')]
assert not any('WarmBorrowed' in arg for arg in resolver)
run('safe-resolver', resolver)
resolved = ET.parse(out / 'resolver.xml').getroot().find('assembly')
assert int(resolved.attrib['passed']) == 42 and all(int(resolved.attrib[key]) == 0 for key in ['errors', 'failed', 'skipped', 'not-run'])

for xmlonly in [False, True]:
    label = 'consumer-' + ('xmlonly' if xmlonly else 'normal')
    build(label, 'samples/M8-xsd-fixed-restriction/Consumer.csproj', ('-p:FixedXmlOnly=' + str(xmlonly).lower(),))
    run(label, [net, '--fx-version', '10.0.11', 'samples/M8-xsd-fixed-restriction/bin/Release/net10.0/Xemele.Benchmarks.dll'])
# Preserve the earlier independently compiled structural source lane, too.
build('structural-xmlonly', 'samples/M8-xsd-content/Content.Consumer.csproj', ('-p:ContentXmlOnly=true',))
run('structural-xmlonly', [net, '--fx-version', '10.0.11', 'samples/M8-xsd-content/bin/Release/net10.0/Xemele.Benchmarks.dll'])
run('planning', ['python3', 'tools/planning/tasks.py', 'validate'])
run('diff', ['git', 'diff', '--check'])
(out / 'summary.json').write_text(json.dumps({'counts': counts, 'total': int(assembly.attrib['passed']), 'publicApi': 'passed',
    'frozenDatatypes': len(expected), 'normalConsumer': 'passed', 'xmlOnlyConsumer': 'passed', 'structuralXmlOnly': 'passed'}, indent=2) + '\n')
