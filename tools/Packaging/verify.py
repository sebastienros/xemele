"""Build local release archives and execute fresh managed/trim/AOT package consumers."""
import sys
sys.dont_write_bytecode = True
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import zipfile
import xml.etree.ElementTree as ET
from hosted_corpus import prepare as prepare_hosted_corpus

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts/M9-distribution'
WORK = OUT / 'work'
VERSION = '0.9.0-rc.1'

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def canonicalize(path):
    # Unsigned local archives only. Normalize ZIP container metadata, never payload bytes.
    with zipfile.ZipFile(path) as archive:
        assert '.signature.p7s' not in archive.namelist()
        entries = {item.filename: archive.read(item) for item in archive.infolist()}
        assert len(entries) == len(archive.infolist())
    temporary = path.with_suffix(path.suffix + '.canonical')
    with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, payload in sorted(entries.items()):
            item = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            item.create_system = 3
            item.external_attr = 0o100644 << 16
            item.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(item, payload)
    with zipfile.ZipFile(temporary) as archive:
        assert entries == {name: archive.read(name) for name in archive.namelist()}
    temporary.replace(path)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--rid', required=True)
    p.add_argument('--dotnet', default=shutil.which('dotnet'))
    p.add_argument('--cache', type=Path)
    p.add_argument('--unit-tests', action='store_true', help='Run cumulative unit correctness before package smoke on the hosted OS')
    p.add_argument('--reuse-builds', action='store_true', help='Resume output checks only after source/config-equivalent completed clean packs')
    args = p.parse_args()
    host = Path(args.dotnet).resolve()
    assert host.is_file()
    if not args.reuse_builds:
        assert not WORK.exists(), 'Use a fresh work directory; inspect previous failures first.'
        WORK.mkdir(parents=True)
    report = dict(task='M9-distribution', revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                  result='failed', rid=args.rid, runtime='10.0.11', commands=[], packages=[], published=False)
    if args.reuse_builds:
        prior = json.loads((OUT / 'summary.json').read_text())
        assert prior['result'] == 'failed' and len(prior['commands']) in (9, 14)
        assert all(c['receipt']['exit'] == 0 and c['receipt'].get('ownedGroupAbsent', True) for c in prior['commands'])
        relevant = ['src', 'tests/PackageSmoke/Release', 'tools/Packaging/Release.targets', 'tools/Packaging/README.md', 'tools/Packaging/LICENSE', 'tools/Packaging/THIRD-PARTY-NOTICES.md', 'Directory.Build.props', 'Directory.Packages.props', 'global.json']
        assert not subprocess.check_output(['git', 'diff', '--name-only', prior['revision'], 'HEAD', '--', *relevant], cwd=ROOT, text=True).strip()
        assert not subprocess.check_output(['git', 'status', '--porcelain', '--', *relevant], cwd=ROOT, text=True).strip()
        report = prior
        report['packages'] = []
        if len(prior['commands']) == 14:
            trimmed_log = WORK / 'command-13.log'
            assert sha(trimmed_log) == prior['commands'][13]['outputSha256']
            assert 'PASS XML/XPath/SAX ownership/HTML/CSS/RNG/XSD/Schematron/SVRL release usage' in trimmed_log.read_text()
            report['consumers']['trim'] = True
            report['trimExecutionReused'] = 'Actual clean exit0 and inspected original stdout; reporting-only indexing error corrected.'
            trimmed_log.unlink()
        report['verificationRevision'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        report['buildReuse'] = 'Completed two clean packs preserved: runtime/tool/docs/notices/build/consumer source and config unchanged; only verifier reporting assertion changed.'
    environment = dict(os.environ, DOTNET_ROOT=str(host.parent), DOTNET_ROLL_FORWARD='Disable',
                       DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_HOST_PATH=str(host))
    supervisor = None
    if os.name != 'nt':
        spec = importlib.util.spec_from_file_location('supervision', ROOT / 'docs/reports/M8-xsd-corpus/supervision.py')
        supervisor = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(supervisor)
        supervisor.ROOT = ROOT
        supervisor.ARTIFACTS = WORK

    def run(argv, seconds=300):
        index = len(report['commands'])
        log = WORK / f'command-{index}.log'
        if supervisor:
            start = time.monotonic()
            receipt = supervisor.supervise_command(list(map(str, argv)), index, environment, start + seconds, start + seconds + 10)
            valid = supervisor.workload_succeeded(receipt)
        else:
            # Windows workflow remains unverified until executed; use finite whole-tree cleanup.
            with log.open('xb') as output:
                child = subprocess.Popen(list(map(str, argv)), cwd=ROOT, env=environment, stdout=output, stderr=subprocess.STDOUT,
                                         creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
                try:
                    code = child.wait(timeout=seconds)
                except subprocess.TimeoutExpired:
                    subprocess.run(['taskkill', '/PID', str(child.pid), '/T', '/F'], timeout=10, capture_output=True)
                    child.wait(timeout=10)
                    code = child.returncode
                    raise
            receipt = dict(exit=code, reason='completed' if code == 0 else 'failed', directChildReaped=True, logClosed=True)
            valid = code == 0
        output = log.read_text(errors='replace')
        report['commands'].append(dict(argv=list(map(str, argv)), receipt=receipt, outputSha256=sha(log)))
        if not valid:
            report['commands'][-1]['failureTail'] = output[-3000:]
            raise RuntimeError(f'Command {index} failed: {output[-2000:]}')
        print(f'PASS command{index}: {argv[1] if len(argv) > 1 else Path(argv[0]).name}', flush=True)
        log.unlink()
        return output

    targets = ROOT / 'tools/Packaging/Release.targets'
    props = [f'-p:DirectoryBuildTargetsPath={targets}', '-p:UseSharedCompilation=false']
    project = ROOT / 'src/Xemele/Xemele.csproj'
    tool = ROOT / 'src/Xemele.Tool/Xemele.Tool.csproj'
    feed = WORK / 'feed'
    feed.mkdir(exist_ok=args.reuse_builds)
    if args.cache and not args.reuse_builds:
        # Exact version and canonical filename, no generic archive aliases or cache mutation.
        for path in sorted(args.cache.glob('*/10.0.11/*.10.0.11.nupkg')):
            shutil.copyfile(path, feed / path.name)
        sources = ['--source', str(feed)]
    elif not args.cache:
        sources = ['--source', 'https://api.nuget.org/v3/index.json']
    else:
        sources = ['--source', str(feed)]
    try:
        if not args.reuse_builds:
            sdk = run([host, '--version']).strip()
            assert sdk == '10.0.400', sdk
            report['sdk'] = sdk
            if args.unit_tests:
                assert not args.cache, 'Hosted cumulative unit path uses the normal configured online package source.'
                resources, report['unitCorpus'] = prepare_hosted_corpus(ROOT, WORK)
                environment['XEMELE_CORPUS_RESOURCES'] = str(resources)
                unit = ROOT / 'tests/Xemele.Tests/Xemele.Tests.csproj'
                run([host, 'restore', unit, *sources, '--disable-build-servers', '-p:RuntimeFrameworkVersion=10.0.11'], 300)
                run([host, 'build', unit, '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false', '-p:RuntimeFrameworkVersion=10.0.11'], 300)
                environment.update(DOTNET_TieredCompilation='0', DOTNET_TieredPGO='0', DOTNET_ReadyToRun='0', DOTNET_gcServer='0', DOTNET_GCConcurrent='0')
                result_xml = WORK / 'unit.xml'
                try:
                    run([host, '--fx-version', '10.0.11', ROOT / 'tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll', '-parallelMode', 'none', '-culture', 'invariant', '-noColor', '-result-xml', result_xml], 500)
                finally:
                    if result_xml.is_file():
                        xml = ET.parse(result_xml).getroot()
                        assemblies = xml.findall('assembly')
                        counts = {name: sum(int(a.get(name, '0')) for a in assemblies) for name in ['total', 'passed', 'failed', 'errors', 'skipped', 'not-run']}
                        report['unitCorrectness'] = counts
                        report['unitFailures'] = [dict(name=t.get('name'), result=t.get('result'),
                                                      failure=ET.tostring(t.find('failure'), encoding='unicode') if t.find('failure') is not None else '')
                                                  for t in xml.findall('.//test') if t.get('result') != 'Pass']
                    report['unitChildDiagnostics'] = []
                    for path in sorted((ROOT / 'artifacts').glob('M2-*-allocation-probe/child-runs/*/*')):
                        if path.name.endswith(('.stderr.txt', '.stdout.txt', '.exit.json', '.invocation.json')):
                            report['unitChildDiagnostics'].append(dict(path=str(path.relative_to(ROOT)), sha256=sha(path),
                                                                       text=path.read_text(errors='replace')[:16000]))
                assert counts['total'] == counts['passed'] == 10741 and all(counts[name] == 0 for name in ['failed', 'errors', 'skipped', 'not-run']), counts
                result_xml.unlink()
            for candidate in [project, tool]:
                run([host, 'restore', candidate, *sources, '--disable-build-servers', *props])
            for index in range(2):
                destination = WORK / f'pack-{index}'
                run([host, 'clean', tool, '-c', 'Release', '--disable-build-servers', *props])
                run([host, 'pack', project, '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', *props, '-o', destination])
                run([host, 'pack', tool, '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', *props, '-o', destination])
                for archive in sorted(destination.glob('*.nupkg')) + sorted(destination.glob('*.snupkg')):
                    canonicalize(archive)
        archives = [WORK / 'pack-0', WORK / 'pack-1']
        names = sorted(p.name for p in archives[0].glob('*.nupkg'))
        assert names == [f'Xemele.{VERSION}.nupkg', f'Xemele.Tool.{VERSION}.nupkg'], names
        for name in names:
            first, second = archives[0] / name, archives[1] / name
            assert first.read_bytes() == second.read_bytes(), f'Nondeterministic package: {name}'
            with zipfile.ZipFile(first) as archive:
                entries = archive.namelist()
                assert {'LICENSE', 'README.md', 'THIRD-PARTY-NOTICES.md'} <= set(entries)
                assert not any('corpora/' in n or 'ReferenceRunners' in n for n in entries)
                nuspec = ET.fromstring(archive.read(next(n for n in entries if n.endswith('.nuspec'))))
                metadata = nuspec.find('{*}metadata')
                assert metadata.find('{*}repository').get('commit') == report['revision']
                assert metadata.find('{*}readme').text == 'README.md'
                assert metadata.find('{*}license').get('type') == 'file'
                runtime_dll = 'lib/net10.0/Xemele.dll'
                if name.startswith('Xemele.Tool.'):
                    assert 'tools/net10.0/any/DotnetToolSettings.xml' in entries
                else:
                    assert runtime_dll in entries
                    report['documentation'] = 'Packaged user guide and notices; full per-member XML comment coverage not claimed.'
                    assert archive.read(runtime_dll) == (ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.dll').read_bytes()
                    assert not any(n.startswith('lib/') and not n.startswith('lib/net10.0/') for n in entries)
            shutil.copyfile(first, feed / name)
            report['packages'].append(dict(name=name, sha256=sha(first), byteIdenticalCleanRebuild=True, noticesDocsMetadata=True))
        # Validate actual embedded Source Link bytes rather than treating an MSBuild setting as evidence.
        pdb = ROOT / 'src/Xemele/bin/Release/net10.0/Xemele.pdb'
        raw = pdb.read_bytes()
        offset = raw.find(b'{"documents":')
        assert offset >= 0, 'Source Link not embedded in portable PDB'
        mapping, _ = json.JSONDecoder().raw_decode(raw[offset:].decode(errors='replace'))
        assert mapping['documents'] and all(report['revision'] in url and 'github' in url for url in mapping['documents'].values())
        report['sourceLink'] = dict(embedded=True, mappings=mapping['documents'], remoteFetchVerified=False)
        # Copy consumer out of source tree and restore from a fresh package cache; no ProjectReference.
        consumer = WORK / 'consumer'
        consumer_sources = ['--source', str(feed)] if args.cache else ['--source', str(feed), '--source', 'https://api.nuget.org/v3/index.json']
        cp = consumer / 'Consumer.csproj'
        if not report.get('consumers', {}).get('managed'):
            shutil.copytree(ROOT / 'tests/PackageSmoke/Release', consumer, ignore=shutil.ignore_patterns('bin', 'obj'))
            cp = consumer / 'Consumer.csproj'
            private_cache = WORK / 'consumer-cache'
            assert not private_cache.exists()
            consumer_sources = ['--source', str(feed)] if args.cache else ['--source', str(feed), '--source', 'https://api.nuget.org/v3/index.json']
            run([host, 'restore', cp, '--packages', private_cache, *consumer_sources, '--disable-build-servers', '-r', args.rid, '-p:PublishAot=true', '-p:PublishTrimmed=true', '-p:RuntimeFrameworkVersion=10.0.11'])
        else:
            assert cp.read_bytes() == (ROOT / 'tests/PackageSmoke/Release/Consumer.csproj').read_bytes()
            assert (consumer / 'Program.cs').read_bytes() == (ROOT / 'tests/PackageSmoke/Release/Program.cs').read_bytes()
        # Reuse the exact linker-host mechanism proved at M8 when the SDK tool runtime has a servicing suffix.
        if os.name != 'nt':
            linker = consumer / 'linker-host'
            linker.mkdir(exist_ok=args.reuse_builds)
            wrapper = linker / 'dotnet'
            wrapper.write_text('#!' + sys.executable + '\nimport os,sys\nos.execv(' + repr(str(host)) + ',[' + repr(str(host)) + ',"--fx-version","10.0.11",*sys.argv[1:]])\n')
            wrapper.chmod(0o755)
            linker_props = [f'-p:PinnedLinkerHost={linker}']
        else:
            wrapper_project = ROOT / 'tools/Packaging/LinkerHost/LinkerHost.csproj'
            run([host, 'restore', wrapper_project, *consumer_sources, '--disable-build-servers', '-p:RuntimeFrameworkVersion=10.0.11'])
            run([host, 'build', wrapper_project, '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'])
            wrapper_directory = wrapper_project.parent / 'bin/Release/net10.0'
            expected_linker = list(private_cache.glob('microsoft.net.illink.tasks/10.0.11/tools/**/illink.dll'))
            assert len(expected_linker) == 1
            environment.update(XEMELE_PINNED_DOTNET=str(host), XEMELE_PINNED_LINKER_DLL=str(expected_linker[0].resolve()))
            linker_props = [f'-p:PinnedLinkerHost={wrapper_directory}', '-p:PinnedLinkerHostName=LinkerHost.exe']
        if not report.get('consumers', {}).get('managed'):
            run([host, 'build', cp, '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'])
            managed = run([host, '--fx-version', '10.0.11', consumer / 'bin/Release/net10.0/Consumer.dll'], 60)
            assert 'PASS XML/XPath/SAX ownership/HTML/CSS/RNG/XSD/Schematron/SVRL release usage' in managed
            report['consumers'] = dict(managed=True, freshCache=True, packageReferenceOnly=True)
        for mode in ['trim', 'aot']:
            if report['consumers'].get(mode):
                continue
            destination = WORK / mode
            run([host, 'publish', cp, '-c', 'Release', '-r', args.rid, '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false', '-p:SelfContained=true', '-p:RuntimeFrameworkVersion=10.0.11', '-p:PublishTrimmed=true', f'-p:PublishAot={str(mode == "aot").lower()}', *linker_props, '-o', destination], 900)
            output = run([destination / ('Consumer.exe' if os.name == 'nt' else 'Consumer')], 60)
            assert 'PASS XML/XPath/SAX ownership/HTML/CSS/RNG/XSD/Schematron/SVRL release usage' in output
            report['consumers'][mode] = True
        tool_path = WORK / 'installed-tool'
        run([host, 'tool', 'install', 'Xemele.Tool', '--version', VERSION, '--tool-path', tool_path, *consumer_sources, '--disable-parallel'], 180)
        output = run([tool_path / ('xemele.exe' if os.name == 'nt' else 'xemele'), '--help'], 60)
        assert 'xemele' in output.lower()
        report['consumers']['toolInstall'] = True
        report['result'] = 'passed'
        report['matrixCredit'] = [args.rid]
    finally:
        OUT.mkdir(exist_ok=True)
        (OUT / 'summary.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(result=report['result'], rid=args.rid, packages=len(report['packages']), consumers=report.get('consumers'))))

if __name__ == '__main__':
    main()
