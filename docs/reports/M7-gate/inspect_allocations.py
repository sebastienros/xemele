#!/usr/bin/env python3
"""Inspect inherited child provenance against the released gate's frozen closure."""
import json,pathlib,xml.etree.ElementTree as ET
root=pathlib.Path(__file__).resolve().parents[3]
manifest=json.loads((root/'artifacts/M7-gate/prepared.json').read_text());rows=[]
for path in sorted((root/'artifacts').rglob('*.request.json')):
 request=json.loads(path.read_text());report=json.loads(pathlib.Path(request['ReportPath']).read_text())
 assert request['Version']==report['Version']==1
 assert request['Mode']==report['Mode'] and request['Token']==report['Token']
 actual=report['Actual'];assert request['Expected']==actual
 checks=[('Executable','ExecutableSha256','runtime'),('TestAssembly','TestSha256','binary'),('ProductAssembly','ProductSha256','binary'),('CoreLibrary','CoreLibrarySha256','runtime')]
 if 'NativeGcLibrary' in actual:checks.append(('NativeGcLibrary','NativeGcSha256','runtime'))
 for path_key,hash_key,domain in checks:assert manifest[domain][actual[path_key]]==actual[hash_key].lower()
 assert manifest['runtime'][str(pathlib.Path(actual['CoreLibrary']).parent/'libcoreclr.dylib')]==actual['NativeRuntimeSha256'].lower()
 assert manifest['binary'][str(pathlib.Path(actual['TestAssembly']).with_suffix('.runtimeconfig.json'))]==actual['RuntimeConfigSha256'].lower()
 assert actual['RuntimeVersion']=='10.0.11'
 if 'RuntimeOverrides' in actual:
  overrides=json.loads(actual['RuntimeOverrides'])
  assert all(overrides[k]=='0' for k in ['DOTNET_TieredCompilation','DOTNET_TieredPGO','DOTNET_ReadyToRun'])
 if 'Architecture' in actual:assert actual['Architecture']=='Arm64'
 measurements=report.get('Labels',[{k:v for k,v in report.items() if k not in ['Actual','Token','ProcessId']}])
 for measurement in measurements:
  if request['Mode']=='real':assert measurement['AllocatedBytes']==0 and measurement['EscapedObjectLength']==0
  else:assert measurement['AllocatedBytes']>0 and measurement['EscapedObjectLength']>0
 assemblies=list(ET.parse(request['XmlPath']).getroot().iter('assembly'))
 assert assemblies and all(int(a.get('failed','0'))==int(a.get('errors','0'))==0 and int(a.get('passed','0'))>0 for a in assemblies)
 rows.append(dict(probe=path.parent.parent.parent.name,mode=request['Mode'],measurements=measurements,requestReportExact=True,leasedBinaryRuntimeHashesExact=True,childXmlPassed=True,reportedIdentityFields=sorted(actual)))
assert len(rows)==12
output=dict(pairs=len(rows),rows=rows,allMeasuredRealLanesZero=True,allPositiveControlsDetected=True)
(root/'artifacts/M7-gate/allocation-children.json').write_text(json.dumps(output,indent=2)+'\n')
print('12 request/report pairs match frozen binaries/runtime; all real measured lanes zero, all positives detected.')
