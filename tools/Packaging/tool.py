"""Build and smoke-test native and portable Xemele tool distributions without uploading."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
NATIVE_RIDS = ('linux-x64', 'osx-arm64', 'win-x64')
RIDS = (*NATIVE_RIDS, 'any')
VERSION_PATTERN = r'(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?'
COMMANDS = {
    'parse', 'format', 'xpath', 'validate-dtd', 'canonicalize', 'catalog', 'xinclude',
    'html-legacy', 'html5', 'html-fragment', 'html-select',
    'validate-rng', 'validate-xsd', 'validate-schematron', 'lint',
}


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def validate_version(version):
    match = re.fullmatch(VERSION_PATTERN, version, flags=re.ASCII)
    if not match:
        raise ValueError('Version must be a three-part NuGet version with an optional prerelease suffix.')
    suffix = match.group(4)
    if suffix and any(part.isdigit() and len(part) > 1 and part[0] == '0' for part in suffix.split('.')):
        raise ValueError('Numeric prerelease identifiers must not have leading zeroes.')
    return match


def release_plan(ref):
    prefix = 'refs/tags/v'
    if not ref.startswith(prefix):
        raise ValueError('Publication requires a release tag such as v1.2.3 or v1.2.3-preview.1.')
    version = ref[len(prefix):]
    match = validate_version(version)
    return version, bool(match.group(4))


def validate_tag_source(ref):
    run(['git', 'fetch', '--no-tags', 'origin', 'main'])
    commit = subprocess.check_output(['git', 'rev-parse', '--verify', f'{ref}^{{commit}}'],
                                     cwd=ROOT, text=True, timeout=60).strip()
    result = subprocess.run(['git', 'merge-base', '--is-ancestor', commit, 'FETCH_HEAD'],
                            cwd=ROOT, timeout=60)
    if result.returncode == 1:
        raise ValueError('Release tag must point to a commit contained in origin/main.')
    result.check_returncode()


def run(argv, **kwargs):
    print('+', ' '.join(map(str, argv)), flush=True)
    return subprocess.run(list(map(str, argv)), cwd=ROOT, check=True, timeout=900, **kwargs)


def smoke(command):
    def invoke(args, text=None, expected=0):
        result = subprocess.run([*map(str, command), *args], input=text, text=True,
                                capture_output=True, timeout=60, cwd=ROOT)
        require(result.returncode == expected,
                f'{args}: expected exit {expected}, got {result.returncode}\n{result.stdout}\n{result.stderr}')
        return result

    help_text = invoke(['--help']).stdout
    listed = {line.strip().split()[0] for line in help_text.splitlines() if line.startswith('  ') and not line.lstrip().startswith('--')}
    require(listed == COMMANDS, f'Unexpected command inventory: {listed}')
    require('Unknown command:' in invoke(['not-a-command'], expected=2).stderr, 'Missing invocation diagnostic')
    xml = '<root><item/><item/></root>'
    require(invoke(['xpath', '--expression', 'count(//item)'], xml).stdout.strip() == '2', 'XPath output mismatch')
    require(invoke(['canonicalize'], xml).stdout.strip() == '<root><item></item><item></item></root>',
            'Canonical XML output mismatch')
    require('<p>Hello</p>' in invoke(['html5'], '<p>Hello').stdout, 'HTML5 output mismatch')
    require('Hello' == invoke(['html-select', '--selector', 'p', '--result', 'text'], '<p>Hello</p>').stdout.strip(),
            'CSS output mismatch')
    invoke(['parse'], '<root>', expected=1)
    with tempfile.TemporaryDirectory(prefix='xemele-tool-smoke-') as temporary:
        work = Path(temporary)
        schemas = {
            'validate-rng': '<element xmlns="http://relaxng.org/ns/structure/1.0" name="root"><empty/></element>',
            'validate-xsd': '<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"><xs:element name="root"/></xs:schema>',
            'validate-schematron': '<schema xmlns="http://purl.oclc.org/dsdl/schematron"><pattern><rule context="*"><assert test="self::root">Expected root</assert></rule></pattern></schema>',
        }
        for name, schema in schemas.items():
            path = work / (name + '.xml')
            path.write_text(schema, encoding='utf-8')
            args = [name, '--schema', str(path), '--diagnostics', 'json']
            valid = json.loads(invoke(args, '<root/>').stderr)
            require(valid['isComplete'] and valid['isValid'], f'{name}: valid document rejected')
            invalid = json.loads(invoke(args, '<wrong/>', expected=1).stderr)
            require(invalid['isComplete'] and not invalid['isValid'] and invalid['diagnostics'],
                    f'{name}: missing validation diagnostics')
            if name == 'validate-schematron':
                svrl = ET.fromstring(invoke([*args, '--svrl', '-'], '<root/>').stdout)
                require(svrl.tag == '{http://purl.oclc.org/dsdl/svrl}schematron-output', 'Missing SVRL output')
        (work / 'part.xml').write_text('<included/>', encoding='utf-8')
        source = work / 'input.xml'
        source.write_text('<root xmlns:xi="http://www.w3.org/2001/XInclude"><xi:include href="part.xml"/></root>', encoding='utf-8')
        included = invoke(['xinclude', '--input', str(source), '--allow-root', str(work)]).stdout
        require('<included' in included and 'xi:include' not in included, 'XInclude output mismatch')
    print('PASS tool XML/XPath/HTML/CSS/RNG/XSD/Schematron/SVRL/XInclude and failure exits', flush=True)


def package_settings(path):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        require({'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'icon.png'} <= set(names),
                f'{path}: missing package documentation or notices')
        settings = [name for name in names if name.endswith('/DotnetToolSettings.xml')]
        require(len(settings) == 1, f'{path}: expected one tool settings file')
        return ET.fromstring(archive.read(settings[0])), names


def check_feed(feed, version):
    expected = {f'Xemele.Tool.{version}.nupkg', f'Xemele.{version}.nupkg'}
    expected.update(f'Xemele.Tool.{rid}.{version}.nupkg' for rid in RIDS)
    require({p.name for p in feed.glob('*.nupkg')} == expected, 'Release feed must contain exactly the library, tool router, and four implementation packages')
    router, names = package_settings(feed / f'Xemele.Tool.{version}.nupkg')
    identifiers = {node.get('RuntimeIdentifier'): node.get('Id') for node in router.iter('RuntimeIdentifierPackage')}
    require(identifiers == {rid: f'Xemele.Tool.{rid}' for rid in RIDS},
            f'Incorrect tool routing: {ET.tostring(router, encoding="unicode")}')
    require(not any(name.endswith('.dll') for name in names), 'Router must not contain implementation assemblies')
    for rid in RIDS:
        check_implementation(feed / f'Xemele.Tool.{rid}.{version}.nupkg', rid)


def check_implementation(path, rid):
    settings, names = package_settings(path)
    command = settings.find('.//Command')
    require(command is not None and command.get('Name') == 'xemele', f'{rid}: incorrect tool command')
    require(command.get('Runner') == ('dotnet' if rid == 'any' else 'executable'), f'{rid}: incorrect tool runner')
    require(any(name.endswith(f'/{rid}/{command.get("EntryPoint")}') for name in names), f'{rid}: missing entry point')
    configs = [name for name in names if name.endswith('.runtimeconfig.json')]
    require(bool(configs) == (rid == 'any'), f'{rid}: incorrect runtime dependency')
    if rid == 'any':
        with zipfile.ZipFile(path) as archive:
            options = json.loads(archive.read(configs[0]))['runtimeOptions']
        framework = options.get('framework', {})
        require(framework.get('name') == 'Microsoft.NETCore.App' and framework.get('version', '').startswith('10.'),
                'Portable tool must depend on the .NET 10 runtime')
        require('includedFrameworks' not in options, 'Portable tool must not bundle the runtime')
    else:
        require(not any(name.endswith('.dll') for name in names), f'{rid}: native tool must not ship managed assemblies')


def build(args):
    validate_version(args.version)
    output = args.output.resolve() / args.rid
    output.mkdir(parents=True, exist_ok=False)
    feed = output / 'packages'
    published = output / 'publish'
    props = [
        f'-p:DirectoryBuildTargetsPath={ROOT / "tools/Packaging/Release.targets"}',
        f'-p:Version={args.version}', '-p:XemelePlatformToolPackages=true',
        '-p:IncludeSymbols=false', '-p:UseSharedCompilation=false',
    ]
    pack = [args.dotnet, 'pack', ROOT / 'src/Xemele.Tool/Xemele.Tool.csproj', '-c', 'Release',
            '--artifacts-path', output / 'build', '-o', feed, *props, '-p:DebugType=None']
    if args.rid == 'any':
        run(pack)
        run([args.dotnet, 'pack', ROOT / 'src/Xemele/Xemele.csproj', '-c', 'Release',
             '--artifacts-path', output / 'library-build', '-o', feed, *props, '-p:DebugType=embedded'])
    run([*pack, '-r', args.rid, f'-p:PublishDir={published}{os.sep}'])
    payload = output / 'payload'
    payload.mkdir()
    if args.rid == 'any':
        for path in published.iterdir():
            if path.suffix in ('.dll', '.json'):
                shutil.copy2(path, payload / path.name)
        command = [args.dotnet, payload / 'Xemele.Tool.dll']
    else:
        extension = '.exe' if args.rid == 'win-x64' else ''
        executable = payload / ('xemele' + extension)
        shutil.copy2(published / ('Xemele.Tool' + extension), executable)
        command = [executable]
    for name in ('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md'):
        shutil.copy2(ROOT / 'tools/Packaging' / name, payload / name)
    smoke(command)
    archives = output / 'archives'
    archives.mkdir()
    archive_format = 'zip' if args.rid in ('any', 'win-x64') else 'gztar'
    archive = shutil.make_archive(str(archives / f'xemele-{args.version}-{args.rid}'), archive_format, payload)
    print(f'Built {archive}', flush=True)


def verify_installation(args):
    feed = args.feed.resolve()
    with tempfile.TemporaryDirectory(prefix='xemele-tool-install-') as temporary:
        work = Path(temporary)
        config = work / 'NuGet.Config'
        configuration = ET.Element('configuration')
        sources = ET.SubElement(configuration, 'packageSources')
        ET.SubElement(sources, 'clear')
        ET.SubElement(sources, 'add', key='release', value=str(feed))
        ET.SubElement(sources, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json')
        mappings = ET.SubElement(configuration, 'packageSourceMapping')
        release = ET.SubElement(mappings, 'packageSource', key='release')
        ET.SubElement(release, 'package', pattern='Xemele*')
        platform = ET.SubElement(mappings, 'packageSource', key='nuget.org')
        ET.SubElement(platform, 'package', pattern='Microsoft.*')
        ET.ElementTree(configuration).write(config, encoding='utf-8', xml_declaration=True)
        environment = dict(os.environ, NUGET_PACKAGES=str(work / 'cache'))
        installed = work / 'installed'
        run([args.dotnet, 'tool', 'install', 'Xemele.Tool', '--version', args.version,
             '--tool-path', installed, '--configfile', config, '--no-http-cache'], env=environment)
        selected = args.rid if args.rid in NATIVE_RIDS else 'any'
        require(list(installed.glob(f'.store/xemele.tool/*/xemele.tool.{selected}/*/tools/*/*/DotnetToolSettings.xml')),
                f'Installer did not select {selected}')
        smoke([installed / ('xemele.exe' if os.name == 'nt' else 'xemele')])
        archive_rid = selected
        extension = '.zip' if archive_rid in ('any', 'win-x64') else '.tar.gz'
        archive = args.archives / f'xemele-{args.version}-{archive_rid}{extension}'
        extracted = work / 'archive'
        extracted.mkdir()
        if extension == '.zip':
            with zipfile.ZipFile(archive) as zipped:
                zipped.extractall(extracted)
        else:
            with tarfile.open(archive) as tar:
                tar.extractall(extracted, filter='data')
        if selected == 'any':
            smoke([args.dotnet, extracted / 'Xemele.Tool.dll'])
        else:
            executable = extracted / ('xemele.exe' if os.name == 'nt' else 'xemele')
            require(os.name == 'nt' or executable.stat().st_mode & 0o111, 'Archive lost executable permissions')
            smoke([executable])
    print(f'PASS automatic {selected} package selection and extracted archive execution', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    plan = commands.add_parser('plan')
    plan.add_argument('--ref', required=True)
    plan.add_argument('--github-output', type=Path, required=True)
    for name in ('build', 'verify'):
        sub = commands.add_parser(name)
        sub.add_argument('--version', required=True)
        sub.add_argument('--dotnet', default=shutil.which('dotnet') or 'dotnet')
        sub.add_argument('--rid', required=True, choices=RIDS if name == 'build' else None)
        if name == 'build':
            sub.add_argument('--output', type=Path, required=True)
        else:
            sub.add_argument('--feed', type=Path, required=True)
            sub.add_argument('--archives', type=Path, required=True)
    args = parser.parse_args()
    if args.command == 'plan':
        version, prerelease = release_plan(args.ref)
        validate_tag_source(args.ref)
        with args.github_output.open('a', encoding='utf-8') as output:
            output.write(f'version={version}\nprerelease={str(prerelease).lower()}\n')
    elif args.command == 'build':
        build(args)
    else:
        validate_version(args.version)
        check_feed(args.feed, args.version)
        verify_installation(args)


if __name__ == '__main__':
    main()
