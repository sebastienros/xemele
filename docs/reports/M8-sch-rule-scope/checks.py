import subprocess, xml.etree.ElementTree as E
from pathlib import Path
subprocess.run(['python3','docs/reports/M8-sch-report/checks.py'],check=True)
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
p=Path('artifacts/M8-sch-rule-scope');p.mkdir(parents=True,exist_ok=True)
subprocess.run([net,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-class','Xemele.Tests.M8SchRuleScope.RuleScopeTests','-result-xml',str(p/'focused.xml')],check=True)
a=E.parse(p/'focused.xml').getroot().find('assembly');assert int(a.get('passed'))==11 and all(int(a.get(k))==0 for k in ['errors','failed','skipped','not-run']),a.attrib
print('PASS:11 repair +546 preserved tests, frozen3/9/154, public1403, normal/trueXML-only consumers; no measurement')
