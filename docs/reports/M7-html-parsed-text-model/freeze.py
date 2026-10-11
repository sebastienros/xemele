#!/usr/bin/env python3
"""Extend verified model identity capture read-only with the complete PI closure."""
import ast, hashlib, json, os, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[3]
base=root/'docs/reports/M7-html-model/freeze.py'
tree=ast.parse(base.read_text(),str(base))
# Load declarations/imports only; do not execute the predecessor's CLI.
cli=next(i for i,n in enumerate(tree.body) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Tuple) for t in n.targets))
namespace={'__file__':str(base)}
exec(compile(ast.Module(tree.body[:cli],type_ignores=[]),str(base),'exec'),namespace)
namespace['probe']=root/'benchmarks/Xemele.Benchmarks/M7-html-parsed-text-model/bin/Release/net10.0'
actual=namespace['capture']()
plan=json.loads((root/'docs/reports/M7-html-parsed-text-model/measurement-plan.json').read_text())
effective=dict(os.environ);effective.update(plan['environment'])
actual['environment'].update(plan['environment'])
actual['effective_environment_sha256']=hashlib.sha256(json.dumps(effective,sort_keys=True,separators=(',',':')).encode()).hexdigest()
actual['effective_environment_key_sha256']={k:hashlib.sha256(v.encode()).hexdigest() for k,v in sorted(effective.items())}
paths=[root/'docs/tasks/M7-html-parsed-text-model.json']
for folder in ['benchmarks/Xemele.Benchmarks/M7-html-parsed-text-model','docs/reports/M7-html-parsed-text-model','tests/Xemele.Tests/M7-html-parsed-text-model','benchmarks/Xemele.Benchmarks/M7-html-parsed-text-model/obj']:
 paths.extend(p for p in (root/folder).rglob('*') if p.is_file() and 'bin' not in p.parts and '__pycache__' not in p.parts)
actual['source'].update({str(p):namespace['digest'](p) for p in sorted(set(paths))})
mode,path=sys.argv[1:];path=pathlib.Path(path)
if mode=='capture':path.write_text(json.dumps(actual,indent=2,sort_keys=True)+'\n')
elif mode=='check':assert json.loads(path.read_text())==actual,'Prepared source/binary/runtime identity changed'
else:raise ValueError('capture|check <manifest>')
print(f'{mode}: source={len(actual["source"])}, binary={len(actual["binary"])}, runtime={len(actual["runtime"])}; manifestSha256={namespace["digest"](path)}')
