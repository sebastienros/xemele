"""Five focused synthetic filesystem catalog controls; SOURCE ONLY until root release."""
import sys
sys.dont_write_bytecode=True
from pathlib import Path
import shutil,time,uuid
import package_inputs as subject
from recipes import ROOT

def run(deadline):
 scratch=ROOT/'artifacts/M8-gate'/('feed-controls-'+uuid.uuid4().hex)
 scratch.mkdir(parents=True,exist_ok=False)
 count=0
 def archive(cache,package_id,version,name,data):
  path=cache/package_id/version/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data);return path
 def refuses(cache,message):
  try:subject.preflight_catalog(cache,deadline)
  except AssertionError as error:assert message in str(error),str(error)
  else:raise AssertionError('Catalog control accepted invalid fixture')
 try:
  cache=scratch/'different-versions';cache.mkdir()
  for version,data in [('1.0.0',b'first-version'),('2.0.0',b'second-version')]:
   archive(cache,'tool',version,f'tool.{version}.nupkg',data);archive(cache,'tool',version,'tool.nupkg',data)
  catalog=subject.preflight_catalog(cache,deadline)
  assert catalog['canonicalCount']==2 and catalog['aliasCount']==2 and [row['target'] for row in catalog['entries']]==['tool.1.0.0.nupkg','tool.2.0.0.nupkg']
  assert catalog['entries'][0]['sha256']!=catalog['entries'][1]['sha256'];count+=1
  cache=scratch/'identical-alias';cache.mkdir()
  canonical=archive(cache,'tool','1.0.0','tool.1.0.0.nupkg',b'identical');alias=archive(cache,'tool','1.0.0','Tool.nupkg',b'identical')
  catalog=subject.preflight_catalog(cache,deadline)
  assert catalog['canonicalCount']==catalog['aliasCount']==1 and catalog['entries'][0]['canonicalSourcePath']==str(canonical.resolve())
  assert catalog['aliases'][0]['source']==str(alias.resolve()) and catalog['aliases'][0]['sha256']==catalog['entries'][0]['sha256'];count+=1
  cache=scratch/'conflicting-alias';cache.mkdir()
  archive(cache,'tool','1.0.0','tool.1.0.0.nupkg',b'canonical');archive(cache,'tool','1.0.0','tool.nupkg',b'conflicting')
  refuses(cache,'Conflicting alias archive bytes');count+=1
  cache=scratch/'missing-peer';cache.mkdir();archive(cache,'tool','1.0.0','tool.nupkg',b'only-alias')
  refuses(cache,'Missing/ambiguous canonical peer');count+=1
  cache=scratch/'identity-path-mismatch';cache.mkdir()
  # Directory claims tool/1.0.0, but its exact tool.1.0.0.nupkg peer is absent.
  archive(cache,'tool','1.0.0','other.1.0.0.nupkg',b'wrong-identity-path')
  refuses(cache,'Identity/path mismatch');count+=1
  assert count==5 and time.monotonic()<deadline
  return count
 finally:
  shutil.rmtree(scratch)
  assert not scratch.exists(),'Synthetic cache/raw fixtures must be removed'
