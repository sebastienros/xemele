import json
import os
import sys
import time

request = json.loads(sys.stdin.readline())
if len(sys.argv) > 2:
    with open(sys.argv[2], 'w') as pid_file:
        pid_file.write(str(os.getpid()))
mode = sys.argv[1]
if mode == 'exit':
    sys.stderr.write('intentional child failure')
    sys.exit(7)
if mode == 'sleep':
    time.sleep(30)
elif mode == 'flood':
    sys.stdout.write('x' * 1100000)
    sys.stdout.flush()
    time.sleep(30)
elif mode == 'stderr-flood':
    sys.stderr.write('x' * 1100000)
    sys.stderr.flush()
    time.sleep(30)
elif mode == 'invalid':
    print('invalid JSON')
elif mode == 'invalid-utf8':
    sys.stdout.buffer.write(b'\xff\n')
elif mode == 'extra-line':
    print('{}\n')
elif mode == 'unterminated':
    sys.stdout.write('{}')
elif mode == 'identity':
    print(json.dumps(dict(protocolVersion=1, caseId='wrong', operation=request['operation'], outcome='success')))
else:
    # Valid envelope for adversarial operation payloads, emitted by an actual child.
    response = dict(protocolVersion=1, caseId=request['caseId'], operation=request['operation'],
                    engine=dict(name='fixture', version='1', runtime='python', build='test'),
                    outcome='success', diagnostics=[], events=[])
    start = dict(kind='start', namespaceUri='', localName='r', attributes=[])
    end = dict(kind='end', namespaceUri='', localName='r')
    text = dict(kind='text', value='')
    attribute = dict(namespaceUri='', localName='a', value='')
    if mode == 'version-overflow':
        response['protocolVersion'] = 2147483648
    elif mode == 'version-exponent':
        response['protocolVersion'] = 1e100
    elif mode == 'diagnostic-number':
        response['diagnostics'] = [42]
    elif mode == 'diagnostic-missing-category':
        response['diagnostics'] = [dict(message='bad')]
    elif mode == 'diagnostic-message-number':
        response['diagnostics'] = [dict(category='xml', message=42)]
    elif mode == 'diagnostic-category-null':
        response['diagnostics'] = [dict(category=None, message='bad')]
    elif mode == 'event-number':
        response['events'] = [42]
    elif mode == 'start-missing-names':
        response['events'] = [dict(kind='start')]
    elif mode == 'start-namespace-null':
        response['events'] = [dict(start, namespaceUri=None)]
    elif mode == 'start-name-number':
        response['events'] = [dict(start, localName=42)]
    elif mode == 'start-attributes-missing':
        del start['attributes']
        response['events'] = [start]
    elif mode == 'attribute-number':
        response['events'] = [dict(start, attributes=[42])]
    elif mode == 'attribute-missing-value':
        del attribute['value']
        response['events'] = [dict(start, attributes=[attribute])]
    elif mode == 'attribute-value-number':
        response['events'] = [dict(start, attributes=[dict(attribute, value=42)])]
    elif mode == 'text-missing-value':
        response['events'] = [dict(kind='text')]
    elif mode == 'text-value-number':
        response['events'] = [dict(text, value=42)]
    elif mode == 'end-missing-namespace':
        del end['namespaceUri']
        response['events'] = [end]
    elif mode == 'end-attributes':
        response['events'] = [dict(end, attributes=[])]
    elif mode == 'start-value':
        response['events'] = [dict(start, value='bad')]
    elif mode == 'text-namespace':
        response['events'] = [dict(text, namespaceUri='')]
    elif mode == 'unknown-kind':
        response['events'] = [dict(kind='unknown')]
    elif mode == 'failure-events':
        response['outcome'] = 'parse-rejected'
    elif mode == 'additive-fields':
        response['future'] = dict(optional=True)
        response['diagnostics'] = [dict(category='note', message='', future=42)]
        response['events'] = [dict(start, future=True, attributes=[dict(attribute, future=42)]),
                              dict(text, future=42), dict(end, future=42)]
    elif mode in ('custom-payload', 'custom-invalid'):
        del response['events']
        response['fixtureValue'] = 'checked' if mode == 'custom-payload' else 42
    else:
        raise ValueError('Unknown child mode: ' + mode)
    print(json.dumps(response))
