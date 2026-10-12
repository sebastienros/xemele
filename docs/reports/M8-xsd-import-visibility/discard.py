"""Root-dispatched terminal discard of single-use prepared outputs; no .NET job."""
import sys
sys.dont_write_bytecode=True
import os
from pathlib import Path
import bindings as b

if __name__=='__main__':
    assert len(sys.argv)==4 and sys.argv[1]=='--root'
    assert os.environ.get('XEMELE_IMPORT_OWNED_REGISTRIES'),'Enclosing registered root required'
    b.claim_prepared(Path(sys.argv[2]),sys.argv[3],os.environ['XEMELE_IMPORT_REVIEW_SHA256'])
