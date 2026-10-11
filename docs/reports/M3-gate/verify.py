#!/usr/bin/env python3
"""Cumulative M3 checkpoint, using verified corpus/child infrastructure."""
import asyncio
import gzip
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[3]
OWN = ROOT / 'docs/reports/M3-gate'
DOTNET = '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
ENV = os.environ | {'PATH': str(Path(DOTNET).parent) + os.pathsep + os.environ['PATH'],
                    'XEMELE_DOTNET': DOTNET, 'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER': '1',
                    'PYTHONDONTWRITEBYTECODE': '1'}
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:BuildInParallel=false',
         '-p:UseSharedCompilation=false', '-p:NuGetAudit=false', '-p:RestoreIgnoreFailedSources=true', '--verbosity', 'minimal']


def run(args, name, timeout=240):
    p = subprocess.run([str(x) for x in args], cwd=ROOT, env=ENV, capture_output=True, timeout=timeout)
    (OWN / (name + '.log')).write_bytes(p.stdout + p.stderr)
    print(name, 'exit', p.returncode, flush=True)
    if p.returncode:
        print((p.stdout + p.stderr).decode(errors='replace')[-5000:], flush=True)
        raise RuntimeError(name + ' failed with ' + str(p.returncode))
    return p.stdout.decode()


def prepare():
    for project, name in [('Xemele.slnx', 'solution-build'),
                          ('samples/M2-conformance-grammar/Probe.csproj', 'grammar-build'),
                          ('docs/reports/M3-gate/Demo/Demo.csproj', 'demo-build')]:
        run([DOTNET, 'build', project, *FLAGS], name)
    run([DOTNET, OWN / 'Demo/bin/Release/net10.0/Demo.dll'], 'source-demo', 30)


def cumulative():
    rows = []
    for group in ['Xemele.Tests', 'Xemele.Conformance.Tests', 'Xemele.Differential.Tests']:
        xml = OWN / (group + '.xml')
        run([DOTNET, ROOT / 'tests' / group / 'bin/Release/net10.0' / (group + '.dll'),
             '-parallelMode', 'none', '-noColor', '-result-xml', xml], group, 240)
        tree = ET.parse(xml)
        assemblies = list(tree.getroot().iter('assembly'))
        total = sum(int(a.get('total', '0')) for a in assemblies)
        passed = sum(int(a.get('passed', '0')) for a in assemblies)
        failed = sum(int(a.get('failed', '0')) for a in assemblies)
        skipped = sum(int(a.get('skipped', '0')) for a in assemblies)
        errors = sum(int(a.get('errors', '0')) for a in assemblies)
        assert total > 0 and passed == total and failed == skipped == errors == 0
        identities = sorted(t.get('name', '') for t in tree.getroot().iter('test'))
        rows.append(dict(group=group, total=total, passed=passed, failed=failed, skipped=skipped, errors=errors,
                         identitiesSha256=hashlib.sha256('\n'.join(identities).encode()).hexdigest()))
        with gzip.open(str(xml) + '.gz', 'wb') as f: f.write(xml.read_bytes())
        xml.unlink()
    report = dict(revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(), result='passed', groups=rows)
    (OWN / 'cumulative-tests.json').write_text(json.dumps(report, indent=2) + '\n')


def grammar():
    sys.path.insert(0, str(ROOT / 'docs/reports/M2-conformance-grammar'))
    import full_grammar
    summary = {'result': 'failed', 'scope': 'Existing M2 original-byte/source-context normative model, resources and all callback pauses; no modified inputs/exclusions.'}
    try:
        asyncio.run(asyncio.wait_for(full_grammar.execute(summary), 180))
    except BaseException as e:
        summary['failure'] = str(e) or type(e).__name__
        raise
    finally:
        (OWN / 'source-context-grammar.json').write_text(json.dumps(summary, indent=2) + '\n')
        print(json.dumps({k: summary[k] for k in ('result', 'counts', 'failure') if k in summary}), flush=True)


def api():
    path = ROOT / 'artifacts/M3-gate/api-full.json'; path.parent.mkdir(parents=True, exist_ok=True)
    run([sys.executable, '-B', ROOT / 'tools/ApiBaseline/compare.py', '--report', path], 'api-check', 120)
    d = json.loads(path.read_text()); assert d['matched'] and d['checkerExit'] == 0 and d['inventory']['apiDeclarations'] == 720
    (OWN / 'api-summary.json').write_text(json.dumps({k: d[k] for k in ['matched', 'checkerExit', 'checkerOutput', 'assemblySha256']}, indent=2) + '\n')


def package():
    feed = ROOT / 'artifacts/M3-gate/packages'; feed.mkdir(parents=True, exist_ok=True)
    version = '1.0.0-m3-preview'
    run([DOTNET, 'pack', ROOT / 'src/Xemele/Xemele.csproj', *FLAGS, '--no-build', '--no-restore',
         '-p:PackageVersion=' + version, '-o', feed], 'package-pack')
    path = feed / ('Xemele.' + version + '.nupkg')
    with zipfile.ZipFile(path) as z:
        entries = z.namelist(); assert 'lib/net10.0/Xemele.dll' in entries
        assert not any('Contracts' in p or 'Foundation' in p or 'corpora' in p for p in entries)
        assert not any(p.startswith('lib/') and not p.startswith('lib/net10.0/') for p in entries)
        assembly_hash = hashlib.sha256(z.read('lib/net10.0/Xemele.dll')).hexdigest()
    assert assembly_hash == json.loads((OWN / 'api-summary.json').read_text())['assemblySha256']
    with tempfile.TemporaryDirectory(prefix='xemele-m3-package-') as tmp:
        tmp = Path(tmp)
        (tmp / 'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="' + str(feed) + '"/></packageSources></configuration>')
        (tmp / 'Consumer.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><PackageReference Include="Xemele" Version="' + version + '"/></ItemGroup></Project>')
        (tmp / 'Program.cs').write_bytes((OWN / 'Demo/Program.cs').read_bytes())
        run([DOTNET, 'build', tmp / 'Consumer.csproj', *FLAGS, '-p:RestorePackagesPath=' + str(tmp / 'packages')], 'package-consumer-build')
        output = run([DOTNET, tmp / 'bin/Release/net10.0/Consumer.dll'], 'package-demo', 30)
        assert output == (OWN / 'source-demo.log').read_text()
    (OWN / 'package-summary.json').write_text(json.dumps(dict(result='passed', version=version,
        packageSha256=hashlib.sha256(path.read_bytes()).hexdigest(), assemblySha256=assembly_hash,
        targetFramework='net10.0', isolatedConsumer=True, onlyLocalFeed=True, matchesSourceDemo=True,
        published=False), indent=2) + '\n')


if __name__ == '__main__':
    phases = {'prepare': prepare, 'tests': cumulative, 'grammar': grammar, 'api': api, 'package': package}
    if len(sys.argv) != 2 or sys.argv[1] not in phases: raise SystemExit('prepare|tests|grammar|api|package')
    phases[sys.argv[1]]()
