"""Exact inherited test identity digests with only the authorized foundation transition and new owned entity tests."""
import hashlib,json,sys
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
raw=Path(sys.argv[1]);report=Path(sys.argv[2])
baseline=json.loads((ROOT/'docs/reports/M2-dtd-framing-allocation-probe/history/verified-cc4770b/summary.json').read_text())
s={'result':'failed','baselineSource':'verified cancellation identities plus integrated framing protocol Fact','assemblies':{}}
for assembly in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']:
 rows=list(ET.parse(raw/(assembly+'.xml')).iter('test'))
 identities={(r.get('type'),r.get('method'),r.get('name')) for r in rows};assert len(identities)==len(rows)
 entity={x for x in identities if x[0].startswith('Xemele.Tests.Entities.')}
 protocol={x for x in identities if x[0]=='Xemele.Tests.FramingAllocationProtocolTests'}
 inherited=identities-entity-protocol
 if assembly=='Xemele.Tests':
  transition={x for x in inherited if x[1]=='DefaultProhibitionAndNoUnrequestedResolverWorkRemain'};assert len(transition)==2
  inherited-=transition
  for mode in ['Prohibit','Parse']:
   inherited.add(('Xemele.Tests.EntitiesFoundation.FoundationTests','FoundationDoesNotEnableProductionDtdOrEntityParsing','Xemele.Tests.EntitiesFoundation.FoundationTests.FoundationDoesNotEnableProductionDtdOrEntityParsing(mode: '+mode+')'))
  assert len(protocol)==1 and len([r for r in rows if r.get('type')=='Xemele.Tests.FramingTests'])==1243
 else:assert not entity and not protocol
 digest=hashlib.sha256(json.dumps(sorted(inherited),ensure_ascii=True,separators=(',',':')).encode()).hexdigest()
 assert len(inherited)==baseline[assembly]['count'] and digest==baseline[assembly]['identitySha256']
 s['assemblies'][assembly]={'prior':len(inherited)+len(protocol),'current':len(rows),'inheritedIdentitySha256':digest,'authorizedFoundationTransitions':2 if assembly=='Xemele.Tests' else 0,'newEntityTests':len(entity),'framingProtocolFacts':len(protocol)}
s['result']='passed';(report/'preservation.json').write_text(json.dumps(s,indent=2)+'\n');print(json.dumps(s))
