"""Validate the stable latestFeature SDK policy used by maintenance checks."""
import json
import re


def stable_version(value):
    if not isinstance(value, str) or not re.fullmatch(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', value):
        raise ValueError(f'Expected a stable SDK version, got {value!r}')
    return tuple(int(part) for part in value.split('.'))


def validate_sdk(root, selected):
    policy = json.loads((root / 'global.json').read_text(encoding='utf-8'))['sdk']
    if policy.get('rollForward') != 'latestFeature' or policy.get('allowPrerelease') is not False:
        raise ValueError('Maintenance SDK validation requires latestFeature with allowPrerelease=false')
    minimum = stable_version(policy['version'])
    actual = stable_version(selected)
    if actual[:2] != minimum[:2] or actual < minimum:
        raise ValueError(
            f'SDK {selected} does not satisfy global.json: stable {minimum[0]}.{minimum[1]} SDK '
            f'>= {policy["version"]} with latestFeature roll-forward required')
