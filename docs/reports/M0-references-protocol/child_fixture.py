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
