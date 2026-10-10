"""Independent expected-model interpreter for the classified, accepted originals.

This is test preparation, not a second validating XML implementation. It applies
XML 1.0 sections 2.11, 3.3.3, 4.4/4.5 and NS 1.0 sections 6.1/6.2 to lexical
originals. It never imports Xemele or a reference response. Unsupported constructs
fail preparation; no product-derived snapshots or silent empty substitutions.
"""
import re
import urllib.parse

S = ' \t\r\n'
PREDEFINED = {'lt': '<', 'gt': '>', 'amp': '&', 'apos': "'", 'quot': '"'}
XML = 'http://www.w3.org/XML/1998/namespace'
TOKEN = re.compile(r'''"[^"]*"|'[^']*'|\([^)]*\)|[^\s]+''')
CHARREF = re.compile(r'&#(x[0-9a-fA-F]+|[0-9]+);')


def characters(value):
    return CHARREF.sub(lambda m: chr(int(m[1][1:], 16) if m[1][0] == 'x' else int(m[1])), value)


def decode(data):
    # Strict source decoding. No replacement fallbacks, including preparation.
    if data.startswith(b'\xef\xbb\xbf'):
        value = data[3:].decode('utf-8')
    elif data.startswith((b'\xff\xfe', b'\xfe\xff')):
        value = data.decode('utf-16')
    elif data.startswith(b'\x00<'):
        value = data.decode('utf-16-be')
    elif data.startswith(b'<\x00'):
        value = data.decode('utf-16-le')
    else:
        declaration = re.match(br'(?:\xef\xbb\xbf)?<\?xml\s[^?]*encoding\s*=\s*[\'"]([^\'"]+)', data)
        value = data.decode(declaration[1].decode('ascii') if declaration else 'utf-8-sig')
    # BOM-aware codecs consume exactly one signature. A second U+FEFF is data.
    return value.replace('\r\n', '\n').replace('\r', '\n')


def markup_end(value, start, doctype=False):
    quote = None
    depth = 0
    i = start
    while i < len(value):
        if quote:
            if value[i] == quote:
                quote = None
        elif value.startswith('<!--', i):
            i = value.index('-->', i + 4) + 2
        elif value[i] in '\'"':
            quote = value[i]
        elif doctype and value[i] == '[':
            depth += 1
        elif doctype and value[i] == ']':
            depth -= 1
        elif value[i] == '>' and depth == 0:
            return i + 1
        i += 1
    raise ValueError('unterminated oracle markup')


class Expected:
    def __init__(self, primary, source_uri, resources):
        self.resources = resources
        self.entities = {}
        self.parameters = {}
        self.attributes = {}
        self.declarations = []
        self.events = []
        self.unresolved = []
        self.incomplete_defaults = []
        self.scopes = [{'xml': XML, '': ''}]
        self.names = []
        self.opens = []
        self.reading_declarations = True
        self.version = None
        self.encoding = None
        self.standalone = None
        self.doctype = None
        self.document(primary, source_uri)
        if self.names:
            raise ValueError('unfinished oracle elements')

    def external(self, system, base):
        uri = urllib.parse.urljoin(base, system)
        if uri not in self.resources:
            raise ValueError('ungranted oracle resource: ' + uri)
        self.opens.append(uri)
        text = decode(self.resources[uri])
        if text.startswith('<?xml') and text[5:6] in S:
            text = text[text.index('?>') + 2:]
        return text, uri

    def parameter(self, name, containing=None):
        value, system, base, external = self.parameters[name]
        if system is not None:
            text, uri = self.external(system, base)
            return text, uri, True
        # XML 4.2.2: declarations in an internal PE use the resource containing
        # its reference, unlike declarations read in a physical external entity.
        return value, containing or base, True

    def dtd(self, value, base, external=False):
        i = 0
        while i < len(value):
            if not self.reading_declarations:
                return
            if value[i] in S:
                i += 1
            elif value.startswith('<!--', i):
                i = value.index('-->', i + 4) + 3
            elif value.startswith('<?', i):
                i = value.index('?>', i + 2) + 2
            elif value[i] == '%':
                end = value.index(';', i)
                if value[i + 1:end] not in self.parameters:
                    # XML 5.1 permits nonvalidating first-unread PE processing.
                    # Preserve declarations already read; never invent a value.
                    self.reading_declarations = False
                    return
                text, uri, origin = self.parameter(value[i + 1:end], base)
                # PE boundaries may split declarations (a VC, not a WFC).
                value = value[:i] + ' ' + text + ' ' + value[end + 1:]
                if uri != base:
                    self.dtd(text, uri, origin)
                    value = value[:i] + value[i + len(text) + 2:]
                else:
                    external = external or origin
            elif value.startswith('<![', i):
                # The status PE may supply INCLUDE plus its opening '['.
                header_end = value.find('<!', i + 3)
                header_end = len(value) if header_end < 0 else header_end
                prefix = value[i + 3:header_end]
                expanded = re.sub(r'%([^;]+);', lambda m: self.parameter(m[1])[0], prefix)
                value = value[:i + 3] + expanded + value[header_end:]
                bracket = value.index('[', i + 3)
                keyword = value[i + 3:bracket].strip(S)
                keyword = re.sub(r'%([^;]+);', lambda m: self.parameter(m[1])[0], keyword).strip(S)
                depth, end = 1, bracket + 1
                while depth:
                    if value.startswith('<![', end):
                        depth += 1
                        end += 3
                    elif value.startswith(']]>', end):
                        depth -= 1
                        end += 3
                    else:
                        end += 1
                    if end > len(value):
                        raise ValueError('unterminated oracle conditional')
                if keyword == 'INCLUDE':
                    self.dtd(value[bracket + 1:end - 3], base, external)
                elif keyword != 'IGNORE':
                    raise ValueError('unknown oracle conditional: ' + keyword)
                i = end
            elif value.startswith('<!', i):
                # Expand PE references outside quotes before locating the closing
                # delimiter: replacement may contain that delimiter.
                j, quote = i + 2, None
                while j < len(value):
                    ch = value[j]
                    if quote:
                        if ch == quote:
                            quote = None
                    elif ch in '\'"':
                        quote = ch
                    elif ch == '%' and re.match(r'%[^\s;]+;', value[j:]):
                        end = value.index(';', j)
                        replacement, _, _ = self.parameter(value[j + 1:end])
                        value = value[:j] + ' ' + replacement + ' ' + value[end + 1:]
                        external = True
                        continue
                    elif ch == '>':
                        break
                    j += 1
                declaration = value[i + 2:j]
                self.declaration(declaration, base, external)
                i = j + 1
            else:
                raise ValueError('unknown oracle DTD token: ' + repr(value[i:i + 40]))

    def declaration(self, value, base, external):
        tokens = TOKEN.findall(value)
        kind = tokens.pop(0)
        if kind == 'ENTITY':
            parameter = tokens[0] == '%'
            if parameter:
                tokens.pop(0)
            name = tokens.pop(0)
            if tokens[0][0] in '\'"':
                literal = tokens[0][1:-1]
                literal = re.sub(r'%([^;]+);', lambda m: self.parameter(m[1])[0], literal)
                declaration = (characters(literal), None, base, external)
            elif tokens[0] in ('SYSTEM', 'PUBLIC'):
                system = tokens[1 if tokens[0] == 'SYSTEM' else 2][1:-1]
                declaration = (None, system, base, external)
            else:
                raise ValueError('unknown oracle entity declaration')
            table = self.parameters if parameter else self.entities
            table.setdefault(name, declaration)
        elif kind == 'ATTLIST':
            element = tokens.pop(0)
            while tokens:
                name, type_ = tokens.pop(0), tokens.pop(0)
                if type_ == 'NOTATION':
                    tokens.pop(0)
                default = tokens.pop(0)
                if default == '#FIXED':
                    default = tokens.pop(0)
                literal = default[1:-1] if default[0] in '\'"' else None
                self.attributes.setdefault((element, name), (type_, literal, base, external))
        elif kind not in ('ELEMENT', 'NOTATION'):
            raise ValueError('unknown oracle declaration: ' + kind)

    def attribute_parts(self, literal, base):
        # XML 3.3.3: normalize literal S, preserve numeric-reference characters;
        # reparse general entity replacement, whose numeric refs were processed
        # at declaration time, so their whitespace is now replacement literal S.
        result = []
        known = []
        def unknown(name):
            if known:
                result.append({'kind': 'known', 'text': ''.join(known)})
                known.clear()
            result.append({'kind': 'unknown', 'name': name, 'lexical': '&' + name + ';'})
        i = 0
        while i < len(literal):
            if literal[i] == '&':
                end = literal.index(';', i)
                name = literal[i + 1:end]
                if name.startswith('#'):
                    known.append(characters(literal[i:end + 1]))
                elif name in PREDEFINED:
                    known.append(PREDEFINED[name])
                elif name in self.entities:
                    value, system, uri, _ = self.entities[name]
                    if system is not None:
                        raise ValueError('external oracle attribute entity')
                    for part in self.attribute_parts(value, uri):
                        if part['kind'] == 'known':
                            known.append(part['text'])
                        else:
                            unknown(part['name'])
                else:
                    unknown(name)
                i = end + 1
            else:
                known.append(' ' if literal[i] in S else literal[i])
                i += 1
        if known:
            result.append({'kind': 'known', 'text': ''.join(known)})
        return result

    def attribute(self, literal, base):
        parts = self.attribute_parts(literal, base)
        if any(p['kind'] == 'unknown' for p in parts):
            raise ValueError('applied unknown oracle attribute requires symbolic model')
        return ''.join(p['text'] for p in parts)

    def incomplete_default_models(self):
        for (element, name), (type_, literal, base, external) in self.attributes.items():
            if literal is None:
                continue
            parts = self.attribute_parts(literal, base)
            if any(p['kind'] == 'unknown' for p in parts):
                self.incomplete_defaults.append({'elementName': element, 'attributeName': name, 'lexicalValue': literal,
                                                 'isTokenized': type_ != 'CDATA', 'isExternal': external,
                                                 'sequence': len(self.incomplete_defaults), 'parts': parts})

    def text(self, value):
        if not value or not self.names:
            return
        if self.events and self.events[-1]['kind'] == 'text':
            self.events[-1]['value'] += value
        else:
            self.events.append({'kind': 'text', 'value': value})

    def expanded(self, name, scope, attribute=False):
        if ':' in name:
            prefix, local = name.split(':', 1)
            return scope[prefix], local
        return '' if attribute else scope.get('', ''), name

    def document(self, value, base):
        if isinstance(value, bytes):
            value = decode(value)
        i = 0
        while i < len(value):
            if value.startswith('<!--', i):
                i = value.index('-->', i + 4) + 3
            elif value.startswith('<?', i):
                end = value.index('?>', i + 2)
                if i == 0 and value.startswith('<?xml', i) and value[5:6] in S:
                    attrs = dict(re.findall(r'''(version|encoding|standalone)\s*=\s*['"]([^'"]*)['"]''', value[i:end]))
                    self.version, self.encoding, self.standalone = (attrs.get(n) for n in ('version', 'encoding', 'standalone'))
                i = end + 2
            elif value.startswith('<!DOCTYPE', i):
                end = markup_end(value, i, True)
                lexical = value[i + 9:end - 1]
                subset = lexical[lexical.index('[') + 1:lexical.rindex(']')] if '[' in lexical else ''
                header = lexical[:lexical.index('[')] if '[' in lexical else lexical
                tokens = TOKEN.findall(header)
                self.doctype = tokens[0]
                self.dtd(subset, base)
                if len(tokens) > 1:
                    text, uri = self.external(tokens[2 if tokens[1] == 'SYSTEM' else 3][1:-1], base)
                    self.dtd(text, uri, True)
                self.incomplete_default_models()
                i = end
            elif value.startswith('<![CDATA[', i):
                end = value.index(']]>', i + 9)
                self.text(value[i + 9:end])
                i = end + 3
            elif value.startswith('</', i):
                end = value.index('>', i + 2)
                name = value[i + 2:end].strip(S)
                if self.names.pop() != name:
                    raise ValueError('oracle end name mismatch')
                ns, local = self.expanded(name, self.scopes.pop())
                self.events.append({'kind': 'end', 'namespaceUri': ns, 'localName': local})
                i = end + 1
            elif value[i] == '<':
                end = markup_end(value, i)
                inside = value[i + 1:end - 1]
                empty = inside.endswith('/')
                if empty:
                    inside = inside[:-1]
                name = re.match(r'[^\s]+', inside)[0]
                pairs = re.findall(r'''([^\s=]+)\s*=\s*("[^"]*"|'[^']*')''', inside[len(name):])
                attrs = {n: self.attribute(v[1:-1], base) for n, v in pairs}
                for (element, n), (type_, literal, uri, _) in self.attributes.items():
                    if element != name:
                        continue
                    if n not in attrs and literal is not None:
                        attrs[n] = self.attribute(literal, uri)
                    if n in attrs and type_ != 'CDATA':
                        attrs[n] = ' '.join(part for part in attrs[n].split(' ') if part)
                scope = self.scopes[-1].copy()
                for n, v in attrs.items():
                    if n == 'xmlns':
                        scope[''] = v
                    elif n.startswith('xmlns:'):
                        scope[n[6:]] = v
                ns, local = self.expanded(name, scope)
                members = []
                for n, v in attrs.items():
                    if n == 'xmlns' or n.startswith('xmlns:'):
                        continue
                    a_ns, a_local = self.expanded(n, scope, True)
                    members.append({'namespaceUri': a_ns, 'localName': a_local, 'value': v})
                members.sort(key=lambda a: (a['namespaceUri'].encode('utf-16-be'), a['localName'].encode('utf-16-be')))
                self.events.append({'kind': 'start', 'namespaceUri': ns, 'localName': local, 'attributes': members})
                if empty:
                    self.events.append({'kind': 'end', 'namespaceUri': ns, 'localName': local})
                else:
                    self.names.append(name)
                    self.scopes.append(scope)
                i = end
            elif value[i] == '&':
                end = value.index(';', i)
                name = value[i + 1:end]
                if name.startswith('#'):
                    self.text(characters(value[i:end + 1]))
                elif name in PREDEFINED:
                    self.text(PREDEFINED[name])
                elif name in self.entities:
                    literal, system, uri, _ = self.entities[name]
                    if system is not None:
                        literal, uri = self.external(system, uri)
                    self.document(literal, uri)
                else:
                    ns, local = self.expanded(self.names[-1], self.scopes[-1])
                    self.unresolved.append({'name': name, 'lexicalReference': '&' + name + ';',
                                            'parentNamespace': ns, 'parentName': local,
                                            'sourceUri': base, 'sequence': len(self.unresolved)})
                i = end + 1
            else:
                end = re.search(r'[<&]', value[i:])
                end = i + end.start() if end else len(value)
                self.text(value[i:end])
                i = end
