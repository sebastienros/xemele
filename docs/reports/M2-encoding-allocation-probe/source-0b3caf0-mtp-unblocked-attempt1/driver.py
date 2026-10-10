import os,subprocess,json,gzip,re,hashlib,xml.etree.ElementTree as E
from pathlib import Path
root=Path('/Users/sebastienros/.codex/worktrees/eebd/xemele');report=root/'docs/reports/M2-encoding-allocation-probe/source-0b3caf0-mtp-unblocked-attempt1';report.mkdir()
source='0b3caf048aea839b2d998306752dc75fcaa57686';assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()==source
initial=json.loads((report.parent/'source-0b3caf0-attempt1/summary.json').read_text());assert set(initial['failedChecks'])=={'focused-mtp','cumulative-mtp','child-provenance'}
assert all(hashlib.sha256((root/p).read_bytes()).hexdigest()==h for p,h in initial['sourceHashes'].items())
dotnet='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet';env=dict(os.environ,DOTNET_ROOT=str(Path(dotnet).parent),XEMELE_DOTNET=dotnet,DOTNET_CLI_USE_MSBUILD_SERVER='0',XEMELE_CORPUS_RESOURCES='/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/',XEMELE_CORPUS_RESULTS=str(root/'artifacts/M2-encoding-allocation-probe/source-0b3caf0-attempt1/case-results'),XEMELE_BCL_RUNNER=str(root/'artifacts/M2-encoding-allocation-probe/source-0b3caf0-attempt1/pinned-reference/BclReference.dll'))
childroot=root/'artifacts/M2-encoding-allocation-probe/child-runs';before=set(childroot.iterdir());logs=[];summary={'sourceRevision':source,'result':'failed','reason':'Only MTP SDK named-pipe binding was sandbox-blocked before executing any tests; no repeated completed comparison or direct suite','checks':{}}
try:
 for name,cmd,count in [('focused-mtp',[dotnet,'test','--project','tests/Xemele.Tests','-c','Release','--no-build','--no-restore','--filter-method','*WarmLegacyConversionAndDeclarationMatchingAllocateZero*'],1),('cumulative-mtp',[dotnet,'test','--solution','Xemele.slnx','-c','Release','--no-build','--no-restore'],852)]:
  p=subprocess.run(cmd,cwd=root,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=300);logs.append(json.dumps(cmd)+'\n'+p.stdout+'\nexit='+str(p.returncode));print(p.stdout,flush=True);summary['checks'][name]={'exitCode':p.returncode,'expectedCount':count,'result':'passed' if p.returncode==0 and re.search(r'total:\s*'+str(count)+r'\b',p.stdout) else 'failed'}
 directories=sorted(set(childroot.iterdir())-before);assert len(directories)==2
 allpairs=json.loads((report.parent/'source-0b3caf0-attempt1/child-provenance.json').read_text())
 for directory in directories:
  dest=report/'child-runs'/directory.name;dest.mkdir(parents=True)
  for f in directory.iterdir():
   if f.is_file():(dest/(f.name+'.gz')).write_bytes(gzip.compress(f.read_bytes(),mtime=0))
  allpairs.append({'directory':directory.name,'reports':{mode:json.loads((directory/(mode+'.report.json')).read_text()) for mode in ['real','positive']}})
 assert len(allpairs)==3
 for pair in allpairs:
  directory=childroot/pair['directory']
  for mode,data in pair['reports'].items():
   assert source in data['Actual']['TestVersion'] and source in data['Actual']['ProductVersion'] and data['Actual']['RuntimeVersion']=='10.0.11'
   assert json.loads((directory/(mode+'.request.json')).read_text())['Expected']==data['Actual']
   assert json.loads((directory/(mode+'.exit.json')).read_text())['exitCode']==0
   tests=list(E.parse(directory/(mode+'.xml')).iter('test'));assert len(tests)==1 and tests[0].get('result')=='Pass' and tests[0].get('name')=='Xemele.Tests.EncodingCompatibility.EncodingTests.WarmLegacyConversionAndDeclarationMatchingAllocateZero'
   assert len(data['Labels'])==3
   for item,label,encoding,length,scalars in zip(data['Labels'],['US-ASCII','ISO-8859-1','Windows-1252'],['Ascii','Latin1','Windows1252'],[6,4,5],['abcd\r\n','\u0080é\r\n','€—é\r\n']):
    assert item['Label']==label and item['Encoding']==encoding and item['InputLength']==length and item['Scalars']==scalars and item['WarmCalls']==10000 and item['MeasuredCalls']==10000 and item['WarmConsumed']==10000*length and item['MeasuredConsumed']==10000*length and item['WarmWritten']==10000*length and item['MeasuredWritten']==10000*length and item['FirstOffset']==4+10000*length and item['LastOffset']==3+20000*length
    assert item['AllocatedBytes']==(0 if mode=='real' else 640000) and item['EscapedObjectLength']==(0 if mode=='real' else 37)
 summary['checks']['combined-three-child-pairs']={'result':'passed','pairs':len(allpairs)}
 (report/'child-provenance.json').write_text(json.dumps(allpairs,indent=2)+'\n')
 summary['result']='passed' if all(x['result']=='passed' for x in summary['checks'].values()) else 'failed'
finally:
 (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0));(report/'summary.json').write_text(json.dumps(summary,indent=2)+'\n');(report/'driver.py').write_bytes(Path(__file__).read_bytes())
assert summary['result']=='passed',summary
