"""Build the immutable reference engine; generated sources/libraries stay ignored.
Requires Python 3, Git, CMake 3.31.6 and the recorded Apple Clang 17 toolchain.
Run with --fetch only when network provisioning is authorized.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
CACHE = ROOT / 'artifacts/M0-references-libxml2'
SOURCE = CACHE / 'upstream'
BUILD = CACHE / 'native-build'
COMMIT = '96498992efa48d52b0e8b83058bd88dbdaf153c1'
URL = 'https://gitlab.gnome.org/GNOME/libxml2.git'


def run(args, **kwargs):
    print('$ ' + ' '.join(map(str, args)), flush=True)
    return subprocess.check_output(list(map(str, args)), **kwargs).decode().strip()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    options = argparse.ArgumentParser()
    options.add_argument('--fetch', action='store_true')
    args = options.parse_args()
    CACHE.mkdir(parents=True, exist_ok=True)
    if args.fetch:
        if not (SOURCE / '.git').exists():
            run(['git', 'init', SOURCE])
            run(['git', '-C', SOURCE, 'remote', 'add', 'origin', URL])
        run(['git', '-C', SOURCE, 'fetch', '--depth=1', 'origin', COMMIT])
        run(['git', '-C', SOURCE, 'checkout', '--detach', COMMIT])
    assert run(['git', '-C', SOURCE, 'rev-parse', 'HEAD']) == COMMIT, 'Wrong upstream commit'
    assert not run(['git', '-C', SOURCE, 'status', '--porcelain', '--untracked-files=all']), 'Dirty upstream source'
    assert (SOURCE / 'VERSION').read_text().strip() == '2.15.4'
    cmake = os.environ.get('CMAKE') or shutil.which('cmake')
    if not cmake:
        cmake = str(CACHE / 'cmake-env/bin/cmake')
    cmake_version = run([cmake, '--version']).splitlines()[0]
    assert cmake_version == 'cmake version 3.31.6', cmake_version
    cc = os.environ.get('CC') or shutil.which('clang')
    compiler = run([cc, '--version'])
    assert run(['make', '--version']).splitlines()[0] == 'GNU Make 3.81'
    assert run(['xcrun', '--show-sdk-version']) == '26.2'
    sdk_path = run(['xcrun', '--show-sdk-path'])
    assert compiler.splitlines()[0] == 'Apple clang version 17.0.0 (clang-1700.6.3.2)', compiler
    # This build pins a single verified toolchain. Additional ports require recorded verification.
    assert os.uname().sysname == 'Darwin' and os.uname().machine == 'arm64', 'Unverified platform'
    features = dict(BUILD_SHARED_LIBS='OFF', CMAKE_BUILD_TYPE='Release', CMAKE_POSITION_INDEPENDENT_CODE='ON', CMAKE_OSX_DEPLOYMENT_TARGET='15.0', CMAKE_OSX_ARCHITECTURES='arm64',
                    CMAKE_OSX_SYSROOT=sdk_path, CMAKE_C_FLAGS='', CMAKE_C_FLAGS_RELEASE='-O3 -DNDEBUG',
                    LIBXML2_WITH_CATALOG='OFF', LIBXML2_WITH_DEBUG='OFF', LIBXML2_WITH_DOCS='OFF',
                    LIBXML2_WITH_HTML='OFF', LIBXML2_WITH_HTTP='OFF', LIBXML2_WITH_ICONV='OFF',
                    LIBXML2_WITH_ICU='OFF', LIBXML2_WITH_ISO8859X='OFF', LIBXML2_WITH_LEGACY='OFF',
                    LIBXML2_WITH_MODULES='OFF', LIBXML2_WITH_OUTPUT='OFF', LIBXML2_WITH_PATTERN='OFF',
                    LIBXML2_WITH_PROGRAMS='OFF', LIBXML2_WITH_PUSH='ON', LIBXML2_WITH_PYTHON='OFF',
                    LIBXML2_WITH_READLINE='OFF', LIBXML2_WITH_REGEXPS='OFF', LIBXML2_WITH_SAX1='OFF',
                    LIBXML2_WITH_TESTS='OFF', LIBXML2_WITH_THREADS='ON', LIBXML2_WITH_TLS='OFF',
                    LIBXML2_WITH_VALID='ON', LIBXML2_WITH_XINCLUDE='OFF', LIBXML2_WITH_XPATH='OFF')
    # Fresh configuration avoids inheriting options or compiler launchers from a stale cache.
    if BUILD.exists():
        shutil.rmtree(BUILD)
    os.environ['ZERO_AR_DATE'] = '1'
    config = [cmake, '-S', SOURCE, '-B', BUILD, '-G', 'Unix Makefiles', '-DCMAKE_C_COMPILER=' + cc]
    config += ['-D' + k + '=' + v for k, v in features.items()]
    print(run(config))
    print(run([cmake, '--build', BUILD, '--parallel', '2']))
    bridge = BUILD / 'libxemele_libxml2.dylib'
    command = [cc, '-dynamiclib', '-target', 'arm64-apple-macos15.0', '-isysroot', sdk_path, '-Wl,-install_name,@rpath/libxemele_libxml2.dylib', '-O2', '-std=c11', '-Wall', '-Wextra', '-Werror',
               '-I' + str(SOURCE / 'include'), '-I' + str(BUILD), HERE / 'bridge.c',
               BUILD / 'libxml2.a', '-o', bridge]
    print(run(command))
    dependencies = run(['otool', '-L', bridge])
    assert all('libxml2.' not in item or 'libxemele_libxml2.dylib' in item for item in dependencies.splitlines()[1:]), 'Unexpected host libxml2 linkage'
    archive = subprocess.check_output(['git', '-C', str(SOURCE), 'archive', '--format=tar', COMMIT])
    macros = run([cc, '-dM', '-E', '-I' + str(BUILD), '-include', 'libxml/xmlversion.h', '-x', 'c', '/dev/null'])
    enabled = sorted(line.strip() for line in macros.splitlines()
                     if line.startswith('#define LIBXML_') and '_ENABLED' in line)
    manifest = dict(upstream=URL, commit=COMMIT, version='2.15.4', sourceArchiveSha256=hashlib.sha256(archive).hexdigest(),
                    license='MIT', licenseSha256=sha(SOURCE / 'Copyright'), cmake=cmake_version,
                    compiler=compiler, platform=os.uname().sysname, architecture=os.uname().machine,
                    make=run(['make', '--version']).splitlines()[0], sdk=run(['xcrun', '--show-sdk-version']),
                    configure=features, enabledFeatureMacros=enabled, bridgeSourceSha256=sha(HERE / 'bridge.c'), bridgeSha256=sha(bridge),
                    staticLibrarySha256=sha(BUILD / 'libxml2.a'), dynamicDependencies=dependencies)
    (BUILD / 'provenance.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print('Bridge: ' + str(bridge))
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
