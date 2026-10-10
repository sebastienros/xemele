"""Compare direct-test identities to the integrated cancellation prerequisite; allow only the authorized foundation transition."""
import gzip,hashlib,json,sys
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
report=ROOT/'docs/reports/M2-entities'/sys.argv[1]
baseline=ROOT/'docs/reports/M2-resolver-cancellation/integration-attempt1'
def rows(path):return {(r.get('type'),r.get('method'),r.get('name')) for r in ET.fromstring(gzip.decompress(path.read_bytes())).iter('test')}
s={'result':'failed','baselineSource':'integrated M2-resolver-cancellation direct XML','assemblies':{}}
for assembly in ['Xemele.Tests','Xemele.Conformance.Tests','Xemele.Differential.Tests']:
 old=rows(baseline/(assembly+'.xml.gz'));new=rows(report/(assembly+'.xml.gz'));removed=old-new;added=new-old
 if assembly=='Xemele.Tests':
  assert len(removed)==2 and all(x[1]=='FoundationDoesNotEnableProductionDtdOrEntityParsing' for x in removed)
  transition={x for x in added if x[1]=='DefaultProhibitionAndNoUnrequestedResolverWorkRemain'}
  assert len(transition)==2
  assert all(x[0].startswith('Xemele.Tests.Entities.') for x in added-transition)
 else:assert not removed and not added
 s['assemblies'][assembly]={'prior':len(old),'current':len(new),'preserved':len(old&new),'removed':sorted(removed),'added':sorted(added),'baselineXmlSha256':hashlib.sha256((baseline/(assembly+'.xml.gz')).read_bytes()).hexdigest()}
s['result']='passed';(report/'preservation.json').write_text(json.dumps(s,indent=2)+'\n');print(json.dumps({k:{n:v for n,v in x.items() if n in ('prior','current','preserved')} for k,x in s['assemblies'].items()}))
