"""Reuse integrated affected/API/consumer driver, then exact owned inventory."""
import subprocess
from pathlib import Path
from assert_results import assert_owned
root=Path(__file__).resolve().parents[3]
subprocess.run(['python3','docs/reports/M8-xsd-schema-syntax/check.py'],cwd=root,check=True)
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
out=root/'artifacts/M8-xsd-counted-matcher/owned.xml';out.parent.mkdir(parents=True,exist_ok=True)
subprocess.run([net,'--fx-version','10.0.11','tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll','-namespace','Xemele.Tests.M8XsdCountedMatcher','-namespace','Xemele.Tests.M8XsdAttributeFixed','-namespace','Xemele.Tests.M8Datatypes','-method-','*WarmBorrowed*','-parallelMode','none','-noLogo','-noColor','-result-xml',str(out)],cwd=root,check=True)
assert assert_owned(out)==135
import xml.etree.ElementTree as ET
from collections import Counter
tests=list(ET.parse(out).getroot().iter('test'));names=[t.attrib['name'] for t in tests]
assert len(names)==len(set(names))==135
assert Counter(n.split('.')[2] for n in names)==Counter({'M8XsdCountedMatcher':13,'M8XsdAttributeFixed':45,'M8Datatypes':77})
