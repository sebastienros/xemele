#!/usr/bin/env python3
"""Contract checks only. Fakes and future vectors are not runtime XPath evidence."""
import argparse
import copy
import json
import re
import subprocess
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
AXES = 'child descendant parent ancestor following-sibling preceding-sibling following preceding attribute namespace self descendant-or-self ancestor-or-self'.split()
OPERATORS = 'or and = != < <= > >= + - * div mod | unary-minus'.split()
FUNCTIONS = {'last':(0,0),'position':(0,0),'count':(1,1),'id':(1,1),'local-name':(0,1),'namespace-uri':(0,1),'name':(0,1),'string':(0,1),'concat':(2,None),'starts-with':(2,2),'contains':(2,2),'substring-before':(2,2),'substring-after':(2,2),'substring':(2,3),'string-length':(0,1),'normalize-space':(0,1),'translate':(3,3),'boolean':(1,1),'not':(1,1),'true':(0,0),'false':(0,0),'lang':(1,1),'number':(0,1),'sum':(1,1),'floor':(1,1),'ceiling':(1,1),'round':(1,1)}

def validate_inventory(data):
    assert data['schema_version'] == 1
    for section, names in [('axes',AXES),('operators',OPERATORS),('functions',list(FUNCTIONS))]:
        rows = data[section]
        assert len(rows) == len(names) and {r['name'] for r in rows} == set(names), section
    ids = []
    for section in ['axes','operators','functions','cases']:
        for row in data[section]:
            assert row['id'].startswith('M4-contracts-A2-')
            assert row['expression'] and 'expected' in row and row['expected'] is not None
            ids.append(row['id'])
    assert len(ids) == len(set(ids))
    for row in data['functions']:
        assert (row['min_arguments'],row['max_arguments']) == FUNCTIONS[row['name']], row['name']
    assert data['documents'] and data['context'] and data['namespaces']['p'] == 'urn:p'
    model = (HERE/'Model.cs.fixture').read_text()
    def members(name):
        return {x.strip() for x in re.search(r'enum '+name+r' \{([^}]+)\}', model)[1].split(',')}
    assert members('XPathAxis') == {r['enum'] for r in data['axes']}
    assert members('XPathBinaryOperator') == {r['enum'] for r in data['operators'] if r['enum']}
    return len(ids)

def controls(data):
    mutations=[]
    for section in ['axes','operators','functions']:
        d=copy.deepcopy(data);d[section].pop();mutations.append(d)
    d=copy.deepcopy(data);d['functions'][0]['min_arguments']=1;mutations.append(d)
    d=copy.deepcopy(data);d['cases'][0]['id']=d['axes'][0]['id'];mutations.append(d)
    d=copy.deepcopy(data);del d['cases'][0]['expected'];mutations.append(d)
    d=copy.deepcopy(data);d['operators'][0]['name']='xor';mutations.append(d)
    for d in mutations:
        try: validate_inventory(d)
        except (AssertionError,KeyError): pass
        else: raise AssertionError('inventory negative control accepted')
    return len(mutations)

def run(command):
    p=subprocess.run(command,cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    return p.returncode,p.stdout

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--dotnet',required=True);a=parser.parse_args()
    code,version=run([a.dotnet,'--version']);assert code==0 and version.strip()=='10.0.400',version
    code,runtimes=run([a.dotnet,'--list-runtimes']);assert code==0 and 'Microsoft.NETCore.App 10.0.11 ' in runtimes,runtimes
    print('PASS: pinned SDK 10.0.400 / runtime 10.0.11',flush=True)
    # These declaration projects and verified runtime have no NuGet packages.
    # Restore from a local source to avoid network-dependent checks.
    for project in [HERE/lane/(lane+'.csproj') for lane in ['Consumer','Compiler','Navigator','Functions','InvalidNamespace','InvalidAttribute']] + [ROOT/'docs/reports/M4-contracts/ApiCheck/ApiCheck.csproj']:
        code,output=run([a.dotnet,'restore',str(project),'--source',str(HERE),'-p:NuGetAudit=false','--verbosity','quiet'])
        assert code==0,output
    print('PASS: seven standalone projects restored from local source without package downloads',flush=True)
    data=json.loads((HERE/'inventory.json').read_text());count=validate_inventory(data);negative=controls(data)
    print(f'PASS: inventory {len(AXES)} axes, {len(OPERATORS)} operators, {len(FUNCTIONS)} functions, {len(data["cases"])} additional cases; {count} unique acceptance vectors; {negative} rejecting mutation controls',flush=True)
    for lane in ['Consumer','Compiler','Navigator','Functions']:
        cmd=[a.dotnet,'run','--project',str(HERE/lane/(lane+'.csproj')),'-c','Release','--no-restore','--no-launch-profile','-p:NuGetAudit=false','--verbosity','quiet']
        code,output=run(cmd);print(output.strip(),flush=True)
        assert code == 0,(lane,code)
    for lane,diagnostic in [('InvalidNamespace','CS0117'),('InvalidAttribute','CS1503')]:
        code,output=run([a.dotnet,'build',str(HERE/lane/(lane+'.csproj')),'-c','Release','--no-restore','-p:NuGetAudit=false','--verbosity','quiet'])
        assert code != 0 and diagnostic in output,(lane,code,output)
        print(f'PASS: {lane} rejects with {diagnostic}',flush=True)
    checker_project=ROOT/'docs/reports/M4-contracts/ApiCheck/ApiCheck.csproj'
    code,output=run([a.dotnet,'build',str(checker_project),'-c','Release','--no-restore','-p:NuGetAudit=false','--verbosity','quiet'])
    assert code == 0,output
    checker=checker_project.parent/'bin/Release/net10.0/ApiCheck.dll'
    baseline=HERE/'Public.golden.txt'
    for lane in ['Consumer','Compiler','Navigator','Functions']:
        assembly=HERE/lane/('bin/Release/net10.0/'+lane+'.dll')
        code,output=run([a.dotnet,str(checker),'compare',str(assembly),str(baseline)])
        assert code==0,(lane,output)
        print(f'PASS: {lane} '+output.strip(),flush=True)
    lines=baseline.read_text().splitlines()
    assert len(lines)==178 and lines[0]=='xemele-api-v1'
    # Preserve immutable exact baseline; mutations exist only in private temp files.
    for changed in [lines[:-1], [lines[0]]+[line.replace('Evaluate(', 'EvaluateChanged(') for line in lines[1:]]]:
        with tempfile.TemporaryDirectory(prefix='m4-contract-api-') as directory:
            bad=Path(directory)/'bad.txt';bad.write_text('\n'.join(changed)+'\n')
            code,output=run([a.dotnet,str(checker),'compare',str(HERE/'Consumer/bin/Release/net10.0/Consumer.dll'),str(bad)])
            assert code==1 and 'API mismatch' in output,output
    print('PASS: compiled API rejects missing member and changed signature controls; 177 members include resolved XmlNode/XmlAttribute types',flush=True)
    # Compare to coordinated claim, including already committed changes.
    code,output=run(['git','diff','--name-only','8426569c66102e1897c4916783453ed7544f1233','--'])
    assert code==0
    owned=lambda p: p in ['docs/specs/xpath.md','docs/tasks/M4-contracts.json'] or any(p.startswith(x) for x in ['tests/Contracts/Fixtures/M4/','docs/decisions/M4/','docs/reports/M4-contracts/'])
    assert all(owned(p) for p in output.splitlines()),output
    code,untracked=run(['git','ls-files','--others','--exclude-standard']);assert code==0
    assert all(owned(p) for p in untracked.splitlines()),untracked
    assert not any('XPath' in str(p) for p in (ROOT/'src').rglob('*.cs'))
    for lane in ['Consumer','Compiler','Navigator','Functions','InvalidNamespace','InvalidAttribute']:
        text=(HERE/lane/(lane+'.csproj')).read_text();assert '<IsPackable>false</IsPackable>' in text
        assert 'ProjectReference' in text and '../Public.cs.fixture' in text and '../Model.cs.fixture' in text
    print('PASS: six nonpackaged isolated projects; owned diff/untracked only; earlier source/build/contract files preserved against claim; no runtime XPath stubs',flush=True)
if __name__ == '__main__': main()
