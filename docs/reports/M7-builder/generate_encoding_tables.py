#!/usr/bin/env python3
"""Regenerate owned immutable WHATWG label/index tables without network access."""
import hashlib, json
from pathlib import Path
root = Path(__file__).resolve().parents[3]
fixtures = root / 'tests/fixtures/M7-builder/encoding'
pins = {'encodings.json': '078212b3697f60b81225b6671bd9da2604497abff0aa8e96e4d46605c02ac9e7', 'indexes.json': 'b7c8961095f0b4cae8f4a16a5011e7c84047a60c1277e28ee8df9e56074ee509'}
for file, sha in pins.items():
    assert hashlib.sha256((fixtures / file).read_bytes()).hexdigest() == sha, file
labels = json.loads((fixtures / 'encodings.json').read_text())
indexes = json.loads((fixtures / 'indexes.json').read_text())
code = '#nullable enable\nnamespace Xemele.Html;\n// Generated from unchanged WHATWG encoding index snapshots in M7-builder fixtures.\ninternal static class HtmlEncodingTables\n{\n    internal static string? Label(ReadOnlySpan<char> label)\n    {\n        label = label.Trim(" \\t\\n\\r\\f".AsSpan());\n'
for group in labels:
    for enc in group['encodings']:
        for label in enc['labels']:
            code += '        if (HtmlAscii.Equals(label, ' + json.dumps(label) + ')) return ' + json.dumps(enc['name'].lower()) + ';\n'
code += '        return null;\n    }\n    internal static string? Label(ReadOnlySpan<byte> label)\n    {\n        int start = 0, end = label.Length;\n        while (start < end && label[start] is 9 or 10 or 12 or 13 or 32) start++;\n        while (end > start && label[end - 1] is 9 or 10 or 12 or 13 or 32) end--;\n        label = label[start..end];\n'
for group in labels:
    for enc in group['encodings']:
        for label in enc['labels']:
            code += '        if (HtmlAscii.Equals(label, ' + json.dumps(label) + 'u8)) return ' + json.dumps(enc['name'].lower()) + ';\n'
code += '        return null;\n    }\n'
for key, values in indexes.items():
    name = key.replace('-', '_')
    values = [x for row in values for x in row] if key == 'gb18030-ranges' else values
    code += '    internal static ReadOnlySpan<int> ' + name + ' => [\n'
    for i in range(0, len(values), 24):
        code += '        ' + ','.join(str(x if x is not None else -1) for x in values[i:i+24]) + ',\n'
    code += '    ];\n'
code += '    internal static ReadOnlySpan<int> Single(string encoding) => encoding switch\n    {\n'
for group in labels:
    for enc in group['encodings']:
        key = enc['name'].lower()
        if key in indexes and len(indexes[key]) == 128:
            code += '        ' + json.dumps(key) + ' => ' + key.replace('-', '_') + ',\n'
code += '        "iso-8859-8-i" => iso_8859_8,\n        _ => [],\n    };\n'
code += '    internal static ReadOnlySpan<int> Index(string name) => name switch\n    {\n'
for key in indexes:
    code += '        ' + json.dumps(key) + ' => ' + key.replace('-', '_') + ',\n'
code += '        _ => [],\n    };\n}\n'
target = root / 'src/Xemele/Html5/TreeConstruction/HtmlEncodingTables.Generated.cs'
if '--check' in __import__('sys').argv:
    assert target.read_text() == code, 'Generated source differs; rerun this script.'
    print('All labels and index rows reproduce exactly from pinned originals.')
else:
    target.write_text(code)
