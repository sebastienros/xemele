"""Actual prepared-input positive/unknown-env-negative/final-positive identity controls, no counters."""
import sys
sys.dont_write_bytecode=True
import importlib.util,json,os,time,uuid
from pathlib import Path
from recipes import ROOT,HERE

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
supervisor=module('gate_identity_supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
nested=module('gate_identity_nested',HERE/'nested_ownership.py')
assert len(sys.argv)==2
started=time.monotonic();end=min(started+950,float(os.environ['XEMELE_GATE_WORK_DEADLINE'])-10);hard=min(end+10,float(os.environ['XEMELE_GATE_HARD_DEADLINE']))
raw=Path(os.environ['XEMELE_GATE_RUN_DIR'])/('identity-'+uuid.uuid4().hex);raw.mkdir()
supervisor.ROOT=ROOT;supervisor.ARTIFACTS=raw
rows=[]
with nested.interruptions():
 try:
  for index,(label,expected) in enumerate([('positive',0),('missing-packet-negative',1),('changed-packet-negative',1),('unknown-environment-negative',1),('final-positive',0)]):
   work=min(end,time.monotonic()+180);env=os.environ.copy()
   env['XEMELE_GATE_WORK_DEADLINE']=str(work);env['XEMELE_GATE_HARD_DEADLINE']=str(min(hard,work+10))
   if label=='missing-packet-negative':env.pop('XEMELE_GATE_PACKET_SHA256',None)
   elif label=='changed-packet-negative':env['XEMELE_GATE_PACKET_SHA256']='0'*64
   elif label=='unknown-environment-negative':env['XEMELE_GATE_IDENTITY_NEGATIVE']='must-reject'
   record=nested.supervise_family_command(supervisor,[sys.executable,'-B','docs/reports/M8-gate/checks.py','identity',sys.argv[1]],index,env,work,min(hard,work+10))
   nested.durable(raw/f'terminal-{index}.json',record)
   assert record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'] and record['nestedOwnership']['allOwnedGroupsAbsent']
   output=(raw/f'command-{index}.log').read_text(errors='replace')
   assert record['exit']==expected and record['reason']==('completed' if expected==0 else 'failed'),(label,output[-3000:])
   if label=='unknown-environment-negative':assert 'Unknown effective caller identity control refused' in output
   elif label=='missing-packet-negative':assert 'XEMELE_GATE_PACKET_SHA256' in output
   elif label=='changed-packet-negative':assert 'Root SOURCE release packet hash required' in output
   else:assert '"passed": true' in output
   rows.append({'name':label,'actualExit':record['exit'],'expectedExit':expected,'registeredFamiliesAbsent':True})
  assert time.monotonic()<hard
  print('PASS actual identity controls '+json.dumps({'controls':rows,'tests':0,'counters':False}),flush=True)
 finally:
  for registry in raw.glob('nested-*'):
   if not (registry/'cleanup.json').exists():nested.drain(supervisor,registry,hard-2)
# Enclosing check family persists receipts, inspects this raw subtree and deletes it after terminal custody.
