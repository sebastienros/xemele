"""Freeze authored representative XML shapes before retention/reference measurements."""
import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent


def semantic(payload):
    root = ET.fromstring(payload, parser=ET.XMLParser(target=ET.TreeBuilder(insert_comments=True, insert_pis=True)))
    digest = hashlib.sha256()
    counts = dict(elements=0, attributes=0, textUtf8Bytes=0, comments=0, processingInstructions=0, maximumDepth=0)
    def frame(kind, *values):
        digest.update(kind.encode('ascii'))
        for value in values:
            data = value.encode('utf-8'); digest.update(struct.pack('<I', len(data))); digest.update(data)
    def name(value):
        return tuple(value[1:].split('}', 1)) if value.startswith('{') else ('', value)
    def text(value):
        if value:
            counts['textUtf8Bytes'] += len(value.encode('utf-8')); frame('T', value)
    def walk(node, depth):
        if node.tag is ET.Comment:
            counts['comments'] += 1; frame('C', node.text or ''); return
        if node.tag is ET.ProcessingInstruction:
            counts['processingInstructions'] += 1
            values = (node.text or '').split(' ', 1); frame('P', values[0], values[1] if len(values) == 2 else ''); return
        counts['elements'] += 1; counts['maximumDepth'] = max(counts['maximumDepth'], depth)
        frame('S', *name(node.tag))
        for key, value in sorted(node.attrib.items(), key=lambda pair: name(pair[0])):
            counts['attributes'] += 1; frame('A', *name(key), value)
        text(node.text)
        for child in node:
            walk(child, depth + 1); text(child.tail)
        frame('E')
    walk(root, 1)
    return dict(sha256=digest.hexdigest(), **counts)


def main():
    # Original synthetic documents: family names describe workload shapes, not upstream provenance.
    specs = [
        ('feed-small', 4096, 'feed / repeated names', '<rss version="2.0"><channel>', '</channel></rss>', 'item',
         lambda i: f'<item id="{i}"><title>Article {i}</title><description>Owned XML feed text &amp; data.</description></item>'),
        ('svg-small', 16384, 'SVG / attributes', '<svg xmlns="http://www.w3.org/2000/svg">', '</svg>', 'g',
         lambda i: f'<g id="g{i}" transform="translate({i},2)"><path d="M0 0L20 20" fill="blue"/></g>'),
        ('deep-medium', 65536, 'depth 48 / unique names', ''.join(f'<n{i}>' for i in range(48)), ''.join(f'</n{i}>' for i in reversed(range(48))), 'value',
         lambda i: f'<value key="k{i}">{i:08d}-configuration-value</value>'),
        ('entity-medium', 131072, 'internal entities / mixed content', '<!DOCTYPE book [<!ENTITY e "entity value &amp; safe data">]><book>', '</book>', 'part',
         lambda i: f'<part id="{i}">&e; &#945; &e; text</part>'),
        ('xhtml-medium', 262144, 'XHTML / multilingual text', '<html xmlns="http://www.w3.org/1999/xhtml"><body>', '</body></html>', 'section',
         lambda i: f'<section class="entry" id="s{i}"><h2>Résumé {i}</h2><p>日本語 α 😀 text &amp; content.</p></section>'),
        ('build-medium', 524288, 'project / attribute-heavy', '<Project Sdk="Example.Sdk"><ItemGroup>', '</ItemGroup></Project>', 'Compile',
         lambda i: f'<Compile Include="src/file-{i}.cs" Link="Area/file-{i}.cs" Condition="enabled"/>'),
        ('soap-large', 1048576, 'SOAP / namespaces', '<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:m="urn:records"><s:Body>', '</s:Body></s:Envelope>', 'Record',
         lambda i: f'<m:Record m:id="{i}"><m:Name>Name {i}</m:Name><m:Value>123.45</m:Value></m:Record>'),
        ('configuration-large', 2097152, 'configuration / distinct values', '<configuration><services>', '</services></configuration>', 'service',
         lambda i: f'<service name="s{i}" enabled="true" mode="managed" route="/api/{i}"><endpoint uri="https://fixture.invalid/{i}"/></service>'),
        ('business-large', 5242880, 'business / shallow-wide', '<Ledger><!--original synthetic records--><?release fixture?>', '</Ledger>', 'row',
         lambda i: f'<row id="{i}" tenant="t{i%97}" currency="USD" amount="{i}.25"><name>Account {i}</name><note>Business document text.</note></row>'),
        ('text-large', 10485760, 'text-heavy / bounded tokens', '<data>', '</data>', 'block',
         lambda i: '<block>' + ('Multilingual α 日本語 😀 &amp; text. ' * 256) + '</block>')
    ]
    directory = HERE / 'fixtures'; directory.mkdir(exist_ok=True)
    records = []
    for label, target, shape, opening, closing, selected, unit in specs:
        parts = [opening]; size = len((opening + closing).encode('utf-8')); repeat = 0
        while size < target:
            value = unit(repeat); parts.append(value); size += len(value.encode('utf-8')); repeat += 1
        parts.append(closing); payload = ''.join(parts).encode('utf-8')
        path = directory / (label + '.xml')
        if path.exists(): assert path.read_bytes() == payload, 'Frozen bytes changed'
        else: path.write_bytes(payload)
        records.append(dict(id=label, path='fixtures/' + path.name, bytes=len(payload), sha256=hashlib.sha256(payload).hexdigest(),
                            shape=shape, requestedMinimumBytes=target, repeatedUnits=repeat,
                            xpath=f"count(//*[local-name()='{selected}'])", xpathExpected=repeat, semantic=semantic(payload)))
    manifest = dict(schemaVersion=1, origin='Original deterministic synthetic fixtures authored for Xemele M9; no upstream data copied.',
                    license='MIT, repository LICENSE', weighting='All ten fixtures are primary; retained-memory ratio is sum of candidate retained bytes / sum of BCL retained bytes. Per-fixture ratios and geometric mean also reported without exclusions.',
                    semantics='UTF-8, strict XML, whitespace retained, internal entities expanded, no external resolver; expanded names and non-xmlns attributes sorted by namespace/local name; text/comments/PI preserved; DTD/XML declaration metadata omitted from the common DOM digest only.',
                    normalization='Adjacent character-data nodes coalesced for common XML data-model digest; no text or whitespace dropped.', fixtures=records)
    target = HERE / 'fixtures.json'; data = json.dumps(manifest, indent=2) + '\n'
    if target.exists(): assert target.read_text() == data, 'Frozen manifest changed'
    else: target.write_text(data)
    print(json.dumps(dict(fixtures=len(records), bytes=sum(r['bytes'] for r in records), manifestSha256=hashlib.sha256(data.encode()).hexdigest())))


if __name__ == '__main__': main()
