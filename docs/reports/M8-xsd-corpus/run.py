#!/usr/bin/env python3
"""Sequential bounded corpus execution; raw journals stay in ignored artifacts."""
import argparse, collections, hashlib, json, os, signal, subprocess
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
D='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
DLL=ROOT/'artifacts/M8-xsd-corpus/runner-bin/Release/net10.0/XsdCorpus.dll'
CORPUS=ROOT/'tests/corpora/xsd';OUT=ROOT/'artifacts/M8-xsd-corpus'
def verify(rows,expected,code):
 assert {r['id'] for r in rows}=={r['id'] for r in expected},'case ID coverage differs'
 assert len(rows)==len({r['id'] for r in rows}),'duplicate observations'
 return code==0

def select_sets(selected,inventory):
 sets=[selected] if selected else inventory['testSets']
 assert all(s in inventory['testSets'] for s in sets),'unknown selected test set'
 return sets

def main():
 p=argparse.ArgumentParser();p.add_argument('--engine',choices=['product','bcl'],required=True);p.add_argument('--set');a=p.parse_args();OUT.mkdir(parents=True,exist_ok=True)
 inventory=json.loads((CORPUS/'inventory.json').read_text());revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip();outdir=OUT/revision;outdir.mkdir(exist_ok=True);sets=select_sets(a.set,inventory);process_failures=0;total=collections.Counter()
 for testset in sets:
  name=testset.replace('/','_');journal=outdir/f'{a.engine}-{name}.jsonl';log=outdir/f'{a.engine}-{name}.log';journal.unlink(missing_ok=True)
  with log.open('w') as output:
   command=[D,'--fx-version','10.0.11',str(DLL),str(CORPUS/'upstream'),str(CORPUS/'inventory.json'),testset,a.engine,str(journal)]
   child=subprocess.Popen(command,cwd=ROOT,stdout=output,stderr=subprocess.STDOUT,start_new_session=True)
   try:code=child.wait(timeout=300)
   except subprocess.TimeoutExpired:
    os.killpg(child.pid,signal.SIGTERM)
    try:child.wait(timeout=10)
    except subprocess.TimeoutExpired:os.killpg(child.pid,signal.SIGKILL);child.wait()
    code='timeout'
  rows=[json.loads(r) for r in journal.read_text().splitlines()] if journal.exists() else []
  seen={r['id'] for r in rows};expected=[r for g in inventory['groups'] if g['metadata']==testset for r in ([g['schema']] if g['schema'] else [])+g['instances']+g['supplementalInstances']]
  assert len(seen)==len(rows),'duplicate observations'
  if len(rows)!=len(expected):
   with journal.open('a') as out:
    for row in expected:
     if row['id'] in seen:continue
     actual='harness-process-failure' if row['classification']=='applicable' else row['classification']
     obs={'id':row['id'],'engine':a.engine,'classification':row['classification'],'kind':row['kind'],'expected':row['expected'],'actual':actual,'detail':f'process exit={code}; original case not completed','codes':[],'resources':[],'passed':False if row['classification']=='applicable' else None};rows.append(obs);out.write(json.dumps(obs)+'\n')
  if not verify(rows,expected,code):process_failures+=1
  counts=collections.Counter(('excluded' if r['classification']=='excluded' else 'unresolved' if r['classification']=='unresolved' else 'pass' if r['passed'] is True else 'fail' if r['passed'] is False else 'indeterminate-observed') for r in rows)
  total.update(counts);print(a.engine,testset,'exit',code,dict(counts),flush=True)
 print('TOTAL',dict(total),'process_failures',process_failures,flush=True)
 return 1 if total['fail'] or total['unresolved'] or process_failures else 0
if __name__=='__main__':raise SystemExit(main())
