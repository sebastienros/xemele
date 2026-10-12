"""Owned repair correctness checks; no timing or allocation counters."""
import json, re, subprocess
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
NET = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
FLAGS = ['--disable-build-servers', '-m:1', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
def run(*args):
    result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    print(result.stdout, end='', flush=True)
    result.check_returncode()
    return result.stdout
def build(project, *extra):
    run(NET, 'restore', project, '--source', '/Users/sebastienros/.nuget/packages', *FLAGS, *extra)
    run(NET, 'build', project, '-c', 'Release', '--no-restore', *FLAGS, *extra)

build('tests/Xemele.Tests/Xemele.Tests.csproj')
output = run(NET, '--fx-version', '10.0.11', 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll',
             '-class', 'Xemele.Tests.M8Rng*', '-class', 'Xemele.Tests.M8DatatypesTextBinary.*', '-noColor', '-noLogo')
assert re.search(r'Total: 405, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0', output), output
build('docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj')
output = run('python3', 'docs/reports/M5-validation-model/check_api.py')
assert 'Matched 1410 compiled members' in output, output
build('docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj')
out = ROOT / 'artifacts/M8-rng-lexical/correctness/runtime-internal.txt'
out.parent.mkdir(parents=True, exist_ok=True)
run(NET, '--fx-version', '10.0.11', 'docs/reports/M8-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll',
    'emit', 'src/Xemele/bin/Release/net10.0/Xemele.dll', str(out))
def rows(path): return {row['id']: row for row in (json.loads(line) for line in path.read_text().splitlines()[1:])}
expected = rows(ROOT / 'tests/Contracts/Fixtures/M8/RelaxNg.golden.txt')
actual = rows(out)
assert len(expected) == 444
assert all(actual.get(key) == value for key, value in expected.items())
print('PASS: 444 frozen public/internal RELAX NG contract members.')
consumer = 'docs/reports/M8-rng-lexical/Consumer/Consumer.csproj'
for extra in [(), ('-p:LexicalXmlOnly=true',)]:
    build(consumer, *extra)
    output = run(NET, '--fx-version', '10.0.11', 'docs/reports/M8-rng-lexical/Consumer/bin/Release/net10.0/Xemele.Benchmarks.dll')
    assert 'PASS: 10 consumed lexical/public compile-validation routes.' in output
run('python3', 'tools/planning/tasks.py', 'validate')
run('git', 'diff', '--check')
print('PASS: lexical repair correctness; all checks actually executed.')
