"""Actual prepared-input positive/unknown-env-negative/final-positive identity controls, no counters."""
import sys
sys.dont_write_bytecode=True
import importlib.util,json,os,time,uuid
from pathlib import Path
from recipes import ROOT,HERE,PHASES

# One controller clock includes module preparation, five pairs, inspection and terminal cleanup.
started=time.monotonic()
controller_reserve=40
hard_cap=min(started+3840,float(os.environ['XEMELE_GATE_HARD_DEADLINE']))
end=min(started+3800,float(os.environ['XEMELE_GATE_WORK_DEADLINE']),hard_cap-controller_reserve)
hard=min(end+controller_reserve,hard_cap)

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
supervisor=module('gate_identity_supervision',ROOT/'docs/reports/M8-xsd-corpus/supervision.py')
nested=module('gate_identity_nested',HERE/'nested_ownership.py')
assert len(sys.argv)==2
raw=Path(os.environ['XEMELE_GATE_RUN_DIR'])/('identity-'+uuid.uuid4().hex);raw.mkdir()
supervisor.ROOT=ROOT;supervisor.ARTIFACTS=raw
rows=[]
with nested.interruptions():
 try:
  for index,(label,expected) in enumerate([('positive',0),('missing-packet-negative',1),('changed-packet-negative',1),('unknown-environment-negative',1),('final-positive',0)]):
   pair_started=time.monotonic()
   # Give the inner identity phase its full40-second terminal reserve. The enclosing
   # supervisor waits through that hard deadline, plus5 seconds launch allowance;
   # its separate10-second cleanup window remains inside the controller total.
   inner_work=min(pair_started+PHASES['identity']['work'],end-55)
   inner_hard=inner_work+PHASES['identity']['reserve']
   outer_work=inner_hard+5
   outer_hard=outer_work+10
   assert pair_started<inner_work and outer_hard<=end, 'Controller pair reserve exhausted'
   env=os.environ.copy()
   env['XEMELE_GATE_WORK_DEADLINE']=str(inner_work)
   env['XEMELE_GATE_HARD_DEADLINE']=str(inner_hard)
   if label=='missing-packet-negative':env.pop('XEMELE_GATE_PACKET_SHA256',None)
   elif label=='changed-packet-negative':env['XEMELE_GATE_PACKET_SHA256']='0'*64
   elif label=='unknown-environment-negative':env['XEMELE_GATE_IDENTITY_NEGATIVE']='must-reject'
   record=nested.supervise_family_command(supervisor,[sys.executable,'-B','docs/reports/M8-gate/checks.py','identity',sys.argv[1]],index,env,outer_work,outer_hard)
   record.update(pairStarted=pair_started,innerWorkDeadline=inner_work,innerHardDeadline=inner_hard,
                 outerWorkDeadline=outer_work,outerHardDeadline=outer_hard,
                 innerWorkMaximumSeconds=700,innerReserveSeconds=40,launchAllowanceSeconds=5,outerCleanupSeconds=10)
   nested.durable(raw/f'terminal-{index}.json',record)
   assert record['directChildReaped'] and record['ownedGroupAbsent'] and record['logClosed'] and record['nestedOwnership']['allOwnedGroupsAbsent']
   output=(raw/f'command-{index}.log').read_text(errors='replace')
   assert record['exit']==expected and record['reason']==('completed' if expected==0 else 'failed'),(label,output[-3000:])
   if label=='unknown-environment-negative':assert 'Unknown effective caller identity control refused' in output
   elif label=='missing-packet-negative':assert 'XEMELE_GATE_PACKET_SHA256' in output
   elif label=='changed-packet-negative':assert 'Root SOURCE release packet hash required' in output
   else:assert '"passed": true' in output
   rows.append({'name':label,'actualExit':record['exit'],'expectedExit':expected,'registeredFamiliesAbsent':True,
                'innerWorkDeadline':inner_work,'innerHardDeadline':inner_hard,'outerWorkDeadline':outer_work,'outerHardDeadline':outer_hard})
  assert time.monotonic()<hard
  print('PASS actual identity controls '+json.dumps({'controls':rows,'tests':0,'counters':False,'controllerWorkDeadline':end,'controllerHardDeadline':hard,'controllerReserveSeconds':40}),flush=True)
 finally:
  for registry in raw.glob('nested-*'):
   if not (registry/'cleanup.json').exists():nested.drain(supervisor,registry,hard-2)
# Enclosing check family persists receipts, inspects this raw subtree and deletes it after terminal custody.
