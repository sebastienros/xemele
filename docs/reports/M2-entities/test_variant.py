import copy,hashlib,tempfile,unittest
from pathlib import Path
import variant
class VariantGuards(unittest.TestCase):
 def fixture(self,root):
  for i in range(44):(root/f'file{i}.dll').write_bytes(b'known-source runtime payload'+bytes([i]))
  closure=variant.snapshot(root);revision='a'*40;metadata=[{'path':str(root/f'file{i}.dll'),'sha256':closure[f'file{i}.dll'],'runtime':'10.0.11','architecture':'Arm64','sourceVersion':'1.0.0+'+revision} for i in [0,1]]
  return {'sourceRevision':revision,'closureIdentities':closure,'generatedInputIdentities':{'project':'p','code':'c'}},metadata,revision
 def test_changed_literal_or_static_payload_byte_rejected_without_normalization(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);expected,metadata,revision=self.fixture(root);variant.validate(expected,variant.snapshot(root),{'project':'p','code':'c'},metadata,revision)
   path=root/'file1.dll';data=bytearray(path.read_bytes());data[0]^=1;path.write_bytes(data)
   with self.assertRaisesRegex(ValueError,'changed raw payload'):variant.validate(expected,variant.snapshot(root),{'project':'p','code':'c'},metadata,revision)
 def test_missing_extra_native_config_or_dependency_rejected(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);expected,metadata,revision=self.fixture(root)
   for kind in ['missing','extra']:
    actual=copy.copy(expected['closureIdentities'])
    if kind=='missing':del actual['file43.dll']
    else:actual['native-extra.dylib']=hashlib.sha256(b'extra').hexdigest()
    with self.subTest(kind=kind),self.assertRaises(ValueError):variant.validate(expected,actual,{'project':'p','code':'c'},metadata,revision)
 def test_stale_source_codegen_and_metadata_mismatch_rejected(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);expected,metadata,revision=self.fixture(root)
   for changes in [('source','b'*40),('code','changed'),('runtime','10.0.12'),('sha','not expected')]:
    actual_metadata=copy.deepcopy(metadata);actual_inputs={'project':'p','code':'c'};actual_revision=revision
    if changes[0]=='source':actual_revision=changes[1]
    elif changes[0]=='code':actual_inputs['code']=changes[1]
    elif changes[0]=='runtime':actual_metadata[1]['runtime']=changes[1]
    else:actual_metadata[1]['sha256']=changes[1]
    with self.subTest(change=changes[0]),self.assertRaises(ValueError):variant.validate(expected,expected['closureIdentities'],actual_inputs,actual_metadata,actual_revision)
if __name__=='__main__':unittest.main()
