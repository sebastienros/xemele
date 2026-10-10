"""Strict raw identity of an independently generated complete known-source benchmark variant."""
import hashlib
from pathlib import Path
def snapshot(directory):
 directory=Path(directory)
 return {str(path.relative_to(directory)):hashlib.sha256(path.read_bytes()).hexdigest() for path in directory.rglob('*') if path.is_file()}
def validate(expected, actual_closure, actual_inputs, metadata, revision):
 if expected['sourceRevision']!=revision:raise ValueError('Expected variant is not from current source revision.')
 if len(expected['closureIdentities'])!=44:raise ValueError('Incomplete independently generated closure.')
 if actual_closure!=expected['closureIdentities']:raise ValueError('Generated closure has missing, extra or changed raw payload.')
 if actual_inputs!=expected['generatedInputIdentities']:raise ValueError('Generated project/code identity differs.')
 if len(metadata)!=2:raise ValueError('Expected executable and production-containing benchmark metadata.')
 for assembly in metadata:
  name=Path(assembly['path']).name
  if assembly['sha256']!=expected['closureIdentities'].get(name):raise ValueError('Metadata file identity differs from raw closure.')
  if assembly['sourceVersion']!='1.0.0+'+revision:raise ValueError('Generated executable/production source version differs.')
  if assembly['runtime']!='10.0.11' or assembly['architecture']!='Arm64':raise ValueError('Child metadata probe host/runtime differs.')
 if metadata[1]['sourceVersion']!='1.0.0+'+revision:raise ValueError('Generated production/benchmark source version differs.')
