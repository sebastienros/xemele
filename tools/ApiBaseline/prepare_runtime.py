#!/usr/bin/env python3
"""Install the test-pinned runtime and pin generated application configs."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
VERSION = '10.0.11'


def main():
    host = Path(shutil.which('dotnet')).resolve()
    installation = host.parent
    if not (installation / 'shared/Microsoft.NETCore.App' / VERSION).is_dir():
        windows = os.name == 'nt'
        name = 'dotnet-install.ps1' if windows else 'dotnet-install.sh'
        with tempfile.TemporaryDirectory(prefix='xemele-runtime-') as temporary:
            script = Path(temporary) / name
            with urllib.request.urlopen('https://dot.net/v1/' + name, timeout=30) as response:
                payload = response.read(1024 * 1024)
                assert len(payload) < 1024 * 1024, 'Unexpected installer size'
                script.write_bytes(payload)
            command = (['pwsh', '-NoProfile', '-File', str(script), '-Runtime', 'dotnet',
                        '-Version', VERSION, '-InstallDir', str(installation), '-NoPath'] if windows else
                       ['bash', str(script), '--runtime', 'dotnet', '--version', VERSION,
                        '--install-dir', str(installation), '--no-path'])
            subprocess.run(command, check=True, timeout=240)
    assert (installation / 'shared/Microsoft.NETCore.App' / VERSION).is_dir()
    pinned = []
    for path in sorted(ROOT.rglob('*.runtimeconfig.json')):
        if 'bin' not in path.parts or 'net10.0' not in path.parts:
            continue
        value = json.loads(path.read_text(encoding='utf-8'))
        options = value['runtimeOptions']
        framework = options.get('framework', {})
        if framework.get('name') != 'Microsoft.NETCore.App':
            continue
        framework['version'] = VERSION
        options['rollForward'] = 'Disable'
        path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
        pinned.append(path.relative_to(ROOT).as_posix())
    assert pinned, 'Build applications before pinning test runtime'
    print(json.dumps(dict(runtime=VERSION, applications=pinned), sort_keys=True))


if __name__ == '__main__':
    main()
