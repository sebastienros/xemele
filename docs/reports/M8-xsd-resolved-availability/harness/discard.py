"""Exclusive claim only; enclosing coordinator reaps then deletes prepared bytes."""
import sys
sys.dont_write_bytecode=True
import os
import bindings as b
from family import nested
if __name__=='__main__':
    assert len(sys.argv)==4 and sys.argv[1]=='--root'
    assert nested.registries(os.environ),'Registered coordinator parent required'
    b.claim_prepared(sys.argv[2],sys.argv[3],os.environ['XEMELE_MISSING_REVIEW_SHA256'])
