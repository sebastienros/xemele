"""Ten original children, finite policy; deadlines are correctness bounds only."""
import json,os,sys,time
from pathlib import Path
from supervisor import supervise
import supervisor
root=Path(__file__).resolve().parents[3]
mode=sys.argv[1];assert mode in ['baseline','repaired']
out=root/'artifacts/M8-xsd-counted-matcher'/mode;out.mkdir(parents=True,exist_ok=False)
supervisor.ARTIFACTS=out
net='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
records=[]
for i in range(10):
 record={'index':i};start=time.monotonic()
 argv=[net,'--fx-version','10.0.11',str(root/'samples/M8-xsd-counted-matcher/bin/Release/net10.0/Corpus.Consumer.dll'),mode,str(i),str(root/'tests/fixtures/M8-xsd-counted-matcher'),str(out/f'{i}.json')]
 supervise(argv,os.environ.copy(),start+30,start+40,record,f'{i}.log')
 records.append(record);(out/'lifecycle.json').write_text(json.dumps(records,indent=2))
 assert record['exit']==0 and record.get('directChildReaped') and record.get('ownedGroupAbsent') and record.get('logClosed'),record
 print((out/f'{i}.log').read_text().strip(),flush=True)
