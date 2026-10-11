#!/usr/bin/env python3
"""Compile isolated M5 declarations; never claim DTD/C14N implementation evidence."""
import argparse, json, subprocess, tempfile
from pathlib import Path
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]

def run(args):
    p=subprocess.run(args,cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    return p.returncode,p.stdout

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--dotnet',required=True);parser.add_argument('--emit',action='store_true');a=parser.parse_args()
    code,out=run([a.dotnet,'--version']);assert code==0 and out.strip()=='10.0.400',out
    lanes=['DtdConsumer','C14NConsumer','ApiCheck']
    for lane in lanes:
        project=HERE/lane/(lane+'.csproj')
        code,out=run([a.dotnet,'restore',str(project),'--source',str(HERE),'-p:NuGetAudit=false','--verbosity','quiet']);assert code==0,out
        code,out=run([a.dotnet,'build',str(project),'-c','Release','--no-restore','--verbosity','quiet']);assert code==0,out
    checker=HERE/'ApiCheck/bin/Release/net10.0/ApiCheck.dll'
    for lane in lanes[:2]:
        assembly=HERE/lane/('bin/Release/net10.0/'+lane+'.dll')
        code,out=run([a.dotnet,'--fx-version','10.0.11',str(assembly)]);assert code==0 and '.NET 10.0.11' in out,out
        print(out.strip(),flush=True)
        baseline=HERE/(lane+'.golden.txt')
        code,out=run([a.dotnet,'--fx-version','10.0.11',str(checker),'emit' if a.emit else 'compare',str(assembly),str(baseline)]);assert code==0,out
        print(lane+': '+out.strip(),flush=True)
        rows=baseline.read_text().splitlines();assert len(rows)>1 and rows[0]=='xemele-api-v1'
        assert not any(x in baseline.read_text() for x in ['Xemele.Schema','Xemele.XInclude','Xemele.Catalog'])
        for changed in [rows[:-1],[rows[0]]+[x.replace('Canonicalize(', 'ChangedCanonicalize(').replace('Validate(', 'ChangedValidate(') for x in rows[1:]]]:
            with tempfile.TemporaryDirectory(prefix='m5-contract-negative-') as directory:
                bad=Path(directory)/'bad.txt';bad.write_text('\n'.join(changed)+'\n')
                code,out=run([a.dotnet,'--fx-version','10.0.11',str(checker),'compare',str(assembly),str(bad)])
                assert code==1 and 'API mismatch' in out,out
        print(f'PASS {lane}: exact compiled baseline and missing/changed member negative controls',flush=True)
    for lane in lanes[:2]:
        project=(HERE/lane/(lane+'.csproj')).read_text()
        assert '<IsPackable>false</IsPackable>' in project
        assert 'ProjectReference' in project
        assert ('../Dtd.cs.fixture' in project) != ('../Canonicalization.cs.fixture' in project)
    code,out=run(['git','diff','--name-only','0f772c8da2e2f68d1a0f1b987ec95c03c89288fe','--']);assert code==0
    code,untracked=run(['git','ls-files','--others','--exclude-standard']);assert code==0
    owned=lambda p:p in ['docs/tasks/M5-contracts.json','docs/specs/dtd-c14n.md'] or any(p.startswith(x) for x in ['tests/Contracts/Fixtures/M5/','docs/decisions/M5/','docs/reports/M5-contracts/'])
    assert all(owned(p) for p in (out+untracked).splitlines()),out+untracked
    print('PASS isolated nonpackaged declarations; no source/build/prior contract edits or later feature references',flush=True)
if __name__=='__main__':main()
