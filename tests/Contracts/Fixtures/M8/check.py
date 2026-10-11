#!/usr/bin/env python3
"""Compile independent M8 declarations; execute only integrated M2/M4/M5 APIs."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--dotnet', required=True)
p.add_argument('--ownership-base', default='e0357391da5232e67b4e1de09d6aac52d732275f')
p.add_argument('--emit-baselines', action='store_true', help='Author baselines; normal checks only compare')
a = p.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[3]
lanes = ['Model', 'Regex', 'Datatypes', 'Facets', 'RelaxNg', 'Xsd', 'Schematron', 'Reporting']

def run(args, expected=0):
    result = subprocess.run(args, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if result.returncode != expected:
        raise AssertionError(f'{args!r}\n{result.stdout}')
    return result.stdout

def build(project):
    run([a.dotnet, 'restore', str(project), '--source', str(here), '-p:NuGetAudit=false', '--disable-build-servers', '--verbosity', 'quiet'])
    run([a.dotnet, 'build', str(project), '-c', 'Release', '--no-restore', '-p:RuntimeFrameworkVersion=10.0.11', '-p:UseSharedCompilation=false', '--disable-build-servers', '--verbosity', 'quiet'])

assert run([a.dotnet, '--version']).strip() == '10.0.400'
assert 'Microsoft.NETCore.App 10.0.11 ' in run([a.dotnet, '--list-runtimes'])
for lane in lanes:
    project = here / lane / (lane + '.csproj')
    assert '<IsPackable>false</IsPackable>' in project.read_text()
    build(project)
    output = run([a.dotnet, '--fx-version', '10.0.11', str(project.parent / 'bin/Release/net10.0' / (lane + '.dll'))])
    assert 'runtime 10.0.11' in output
    print(output.strip(), flush=True)

checker_project = root / 'docs/reports/M8-contracts/ApiCheck/ApiCheck.csproj'
build(checker_project)
checker = checker_project.parent / 'bin/Release/net10.0/ApiCheck.dll'
for lane in lanes:
    assembly = here / lane / 'bin/Release/net10.0' / (lane + '.dll')
    baseline = here / (lane + '.golden.txt')
    output = run([a.dotnet, '--fx-version', '10.0.11', str(checker), 'emit' if a.emit_baselines else 'compare', str(assembly), str(baseline)])
    print('PASS: ' + lane + ' ' + output.strip(), flush=True)
    # Mutate member presence, nullable metadata and init-only metadata independently.
    rows = baseline.read_text().splitlines()
    for label, bad_rows in [
        ('missing-member', rows[:-1]),
        ('nullable', [row.replace('XmlResourceResolver?', 'XmlResourceResolver') for row in rows]),
        ('init-only', [row.replace('init=True', 'init=False') for row in rows]),
        ('internal-work-width', [row.replace('Charge(System.Int64 work', 'Charge(System.Int32 work') for row in rows]),
    ]:
        assert bad_rows != rows, (lane, label)
        with tempfile.TemporaryDirectory(prefix='m8-api-control-') as temp:
            bad = Path(temp) / 'bad.txt'
            bad.write_text('\n'.join(bad_rows) + '\n')
            output = run([a.dotnet, '--fx-version', '10.0.11', str(checker), 'compare', str(assembly), str(bad)], expected=1)
            assert 'API mismatch' in output
print('PASS: 32 rejecting member/nullable/init-only/internal-work-width API controls', flush=True)

inventory = json.loads((root / 'docs/decisions/M8/inventory.json').read_text())
assert inventory['profiles'] == ['rng-xml-2001', 'xsd-1.0-second-edition', 'schematron-iso2016-xpath1-svrl']
assert inventory['schema_resource_kind'] == {'name': 'Schema', 'value': 5, 'state': 'ordered-repair-required'}
for group, entries in inventory['features'].items():
    assert entries and len(entries) == len(set(e['id'] for e in entries)), group
    for entry in entries:
        assert entry['disposition'] in ['required', 'excluded'], entry
        assert entry['owner'].startswith('M8-') and entry['reason'], entry
        assert entry['disposition'] != 'excluded' or entry['id'] in inventory['explicit_exclusions'], entry
assert {e['id'] for e in inventory['features']['datatypes']} == set('anySimpleType string boolean decimal float double duration dateTime time date gYearMonth gYear gMonthDay gDay gMonth hexBinary base64Binary anyURI QName NOTATION normalizedString token language NMTOKEN NMTOKENS Name NCName ID IDREF IDREFS ENTITY ENTITIES integer nonPositiveInteger negativeInteger long int short byte nonNegativeInteger unsignedLong unsignedInt unsignedShort unsignedByte positiveInteger'.split())
assert set('length minLength maxLength pattern enumeration whiteSpace maxInclusive maxExclusive minInclusive minExclusive totalDigits fractionDigits'.split()) <= {e['id'] for e in inventory['features']['facets']}
assert {'key', 'unique', 'keyref'} <= {e['id'] for e in inventory['features']['xsd-identity']}
assert {'property', 'properties', 'abstract-pattern', 'extends'} <= {e['id'] for e in inventory['features']['schematron-compilation']}
print('PASS: ' + str(sum(map(len, inventory['features'].values()))) + ' classified inventory entries', flush=True)

owned = lambda s: s in ['docs/specs/schemas.md', 'docs/tasks/M8-contracts.json'] or any(s.startswith(x) for x in ['tests/Contracts/Fixtures/M8/', 'docs/decisions/M8/', 'docs/reports/M8-contracts/'])
for args in [['git', 'diff', '--name-only', a.ownership_base], ['git', 'ls-files', '--others', '--exclude-standard']]:
    assert all(owned(s) for s in run(args).splitlines())
print('PASS: owned paths only; eight independent nonpackaged consumers; no HTML/CSS source or assembly dependency')
