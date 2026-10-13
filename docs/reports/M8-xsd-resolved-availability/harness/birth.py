"""PID/birth identity inspection; no process observation or native load on import.
Darwin layout is attributed to the installed MacOSX.sdk sys/proc_info.h
proc_bsdinfo/PROC_PIDTBSDINFO (3), not a timestamp rounded by ps.
"""
import ctypes
import os
import sys
from pathlib import Path


def identity(pid):
    if pid <= 1: raise ValueError('Invalid owned PID')
    if sys.platform == 'darwin':
        class BsdInfo(ctypes.Structure):
            _fields_ = [(name, ctypes.c_uint32) for name in ['flags','status','xstatus','pid','ppid','uid','gid','ruid','rgid','svuid','svgid','reserved']]
            _fields_ += [('comm', ctypes.c_char * 16), ('name', ctypes.c_char * 32)]
            _fields_ += [(name, ctypes.c_uint32) for name in ['nfiles','pgid','jobc','tdev','tpgid']]
            _fields_ += [('nice', ctypes.c_int32), ('startSeconds', ctypes.c_uint64), ('startMicroseconds', ctypes.c_uint64)]
        library = ctypes.CDLL('/usr/lib/libproc.dylib', use_errno=True)
        method = library.proc_pidinfo
        method.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_uint64, ctypes.c_void_p, ctypes.c_int]
        method.restype = ctypes.c_int
        record = BsdInfo(); size = ctypes.sizeof(record)
        actual = method(pid, 3, 0, ctypes.byref(record), size)
        if actual == 0:
            try: os.kill(pid, 0)
            except ProcessLookupError: return None
            raise RuntimeError('Live process has no complete birth identity')
        if actual != size or record.pid != pid: raise RuntimeError('Incomplete Darwin process identity')
        return {'pid': pid, 'startSeconds': record.startSeconds, 'startMicroseconds': record.startMicroseconds, 'platform': 'darwin'}
    if sys.platform.startswith('linux'):
        try: text = Path('/proc') .joinpath(str(pid), 'stat').read_text()
        except FileNotFoundError: return None
        fields = text[text.rfind(')') + 2:].split()
        return {'pid': pid, 'startTicks': int(fields[19]), 'platform': 'linux'}
    raise RuntimeError('Unsupported process birth identity platform')


def assert_same_or_absent(expected):
    if not expected: raise RuntimeError('Missing registered birth identity')
    current = identity(expected['pid'])
    if current is not None and current != expected: raise RuntimeError('PID was reused; signaling/deletion refused')
    return current
