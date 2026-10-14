#!/usr/bin/env python3
"""Build existing pinned RNG references and restore canonical XML reference API."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'artifacts/M9-ci-maintenance/references'
FROZEN = ROOT / 'docs/reports/M8-rng-corpus'


def run(*arguments, cwd=None, timeout=240):
    arguments = list(map(str, arguments))
    result = subprocess.run(arguments, cwd=cwd, text=True,
                            encoding='utf-8', stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            timeout=timeout)
    print(result.stdout, end='', flush=True)
    result.check_returncode()
    return result.stdout.strip()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def checkout(source, revision, destination):
    run('git', 'init', destination)
    run('git', '-C', destination, 'fetch', '--depth', '1', source, revision)
    run('git', '-C', destination, '-c', 'core.autocrlf=false', 'checkout', '--detach', 'FETCH_HEAD')
    assert run('git', '-C', destination, 'rev-parse', 'HEAD') == revision
    run('git', '-C', destination, 'diff', '--exit-code', 'HEAD')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--jing-source', default='https://github.com/relaxng/jing-trang.git')
    parser.add_argument('--libxml2-source', default='https://gitlab.gnome.org/GNOME/libxml2.git')
    args = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    identities = json.loads((FROZEN / 'reference-build.json').read_text(encoding='utf-8'))
    for engine, name in [('jing', 'RngReference.java'), ('libxml2', 'libxml2_reference.c')]:
        assert sha(FROZEN / name) == identities[engine]['sourceSha256']
    jing = WORK / 'jing'
    checkout(args.jing_source, identities['jing']['revision'], jing)
    run('git', '-C', jing, '-c', 'core.autocrlf=false', 'submodule', 'update', '--init', '--depth', '1')
    assert run('git', '-C', jing / 'relaxng.org', 'rev-parse', 'HEAD') == identities['jing']['submoduleRevision']
    java = Path(shutil.which('java')).resolve()
    javac = Path(shutil.which('javac')).resolve()
    run(java, '-Dant.home=' + str(jing), '-cp', jing / 'lib/ant-launcher.jar',
        'org.apache.tools.ant.launch.Launcher', '-buildfile', jing / 'build.xml', 'ant-jar', cwd=jing)
    jar = jing / 'build/jing.jar'
    run(javac, '-cp', jar, '-d', jing / 'build', FROZEN / 'RngReference.java')
    identities['jing'].update(jarPath=str(jar), jarSha256=sha(jar), classPath=str(jing / 'build'),
                              classSha256=sha(jing / 'build/RngReference.class'),
                              buildCommand='Java Ant launcher ant-jar; javac against built jing.jar',
                              java=run(java, '-version'),
                              runnerClassHashes={p.name: sha(p) for p in sorted((jing / 'build').glob('RngReference*.class'))})
    source = WORK / 'libxml2'
    checkout(args.libxml2_source, identities['libxml2']['revision'], source)
    assert (source / 'VERSION').read_text().strip() == identities['libxml2']['version']
    native = WORK / 'native'
    native.mkdir(exist_ok=True)
    # The original bridge uses POSIX line/token readers. Keep its engine calls
    # verbatim and provide only equivalent CRT glue when compiling with MSVC.
    shim = '''#include <stdio.h>
#include <stdlib.h>
#ifdef _WIN32
static int ci_getline(char **line, size_t *capacity, FILE *input) {
 size_t used = 0; int ch;
 while ((ch = fgetc(input)) != EOF) {
  if (used + 1 >= *capacity) {
   size_t next = *capacity ? *capacity * 2 : 256;
   char *buffer = (char *)realloc(*line, next); if (!buffer) return -1;
   *line = buffer; *capacity = next;
  }
  (*line)[used++] = (char)ch; if (ch == '\\n') break;
 }
 if (!used) return -1;
 (*line)[used] = 0; return (int)used;
}
#define getline ci_getline
#define strtok_r strtok_s
#endif
'''
    (native / 'bridge.c').write_text(shim + '#include "' + (FROZEN / 'libxml2_reference.c').as_posix() + '"\n', encoding='utf-8')
    cmake = f'''cmake_minimum_required(VERSION 3.16)
project(XemeleRngReference C)
add_subdirectory("{source.as_posix()}" libxml2)
add_executable(rng-reference bridge.c)
target_link_libraries(rng-reference PRIVATE LibXml2::LibXml2)
target_compile_features(rng-reference PRIVATE c_std_11)
if(NOT MSVC)
 target_compile_options(rng-reference PRIVATE -Wall -Wextra -Werror)
endif()
'''
    (native / 'CMakeLists.txt').write_text(cmake, encoding='utf-8')
    configuration = {k: v for k, v in identities['libxml2']['config'].items() if k != 'CMAKE_OSX_DEPLOYMENT_TARGET'}
    build = WORK / 'native-build'
    run('cmake', '-S', native, '-B', build, *['-D' + k + '=' + v for k, v in configuration.items()])
    run('cmake', '--build', build, '--config', 'Release', '--target', 'rng-reference', '--parallel', '2')
    candidates = list(build.rglob('rng-reference.exe' if os.name == 'nt' else 'rng-reference'))
    assert len(candidates) == 1
    binary = candidates[0]
    archives = list(build.rglob('libxml2s.lib' if os.name == 'nt' else 'libxml2.a'))
    assert len(archives) == 1
    compiler_metadata = next(build.glob('CMakeFiles/*/CMakeCCompiler.cmake'))
    compiler = re.search(r'set\(CMAKE_C_COMPILER "([^"]+)"\)',
                         compiler_metadata.read_text(encoding='utf-8')).group(1)
    identities['libxml2'].update(binaryPath=str(binary), binarySha256=sha(binary), config=configuration,
                                 archiveSha256=sha(archives[0]), compiler=compiler,
                                 compilerMetadataSha256=sha(compiler_metadata),
                                 cmake=run('cmake', '--version'), bridgeFlags='CMake static pinned libxml2 with original bridge; Windows CRT glue only')
    identities['platform'] = platform.platform()
    identity = WORK / 'reference-build.json'
    identity.write_text(json.dumps(identities, indent=2) + '\n', encoding='utf-8')
    package = WORK / 'canonical'
    package.mkdir(exist_ok=True)
    project = package / 'CanonicalReference.csproj'
    project.write_text('''<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><TargetFramework>net10.0</TargetFramework><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally><WarningsNotAsErrors>NU1902;NU1903</WarningsNotAsErrors></PropertyGroup>
 <ItemGroup><PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.11" /></ItemGroup>
</Project>
''', encoding='utf-8')
    run('dotnet', 'restore', project, '--disable-build-servers')
    if os.environ.get('GITHUB_ENV'):
        with open(os.environ['GITHUB_ENV'], 'a', encoding='utf-8') as environment:
            environment.write(f'XEMELE_RNG_REFERENCE_IDENTITY={identity}\nXEMELE_JAVA={java}\n')
    print(json.dumps(dict(identity=str(identity), java=str(java)), sort_keys=True))


if __name__ == '__main__':
    main()
