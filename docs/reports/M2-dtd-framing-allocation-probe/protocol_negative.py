#!/usr/bin/env python3
"""Bounded rejection controls against the actual known compiled Fact; no rebuild/retries."""
import argparse
import copy
import gzip
import json
import os
from pathlib import Path
import shlex
import subprocess
import uuid
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[3]
REPORT=Path(__file__).resolve().parent
FACT='Xemele.Tests.FramingTests.WarmWhitespacePrimitiveHasNoAllocations'
KEYS=['XEMELE_FRAMING_CHILD_MODE','XEMELE_FRAMING_CHILD_REQUEST','XEMELE_FRAMING_CHILD_TOKEN']

def run_case(command, env, directory, destination, logs, name):
    """Persist the current case even when subprocess fails or times out."""
    stdout, stderr, exit_code, timed_out = '', '', None, False
    (directory/'invocation.json').write_text(json.dumps(dict(command=command, extraEnvironment={k:v for k,v in env.items() if k in KEYS or k=='XEMELE_DOTNET'}),indent=2)+'\n')
    try:
        process=subprocess.run(command,cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=40)
        stdout,stderr,exit_code=process.stdout,process.stderr,process.returncode
        return process
    except subprocess.TimeoutExpired as error:
        timed_out=True
        def decode(value): return value.decode(errors='replace') if isinstance(value,bytes) else value or ''
        stdout,stderr=decode(error.stdout),decode(error.stderr)
        raise
    finally:
        (directory/'stdout.txt').write_text(stdout);(directory/'stderr.txt').write_text(stderr)
        (directory/'exit.json').write_text(json.dumps(dict(exitCode=exit_code,timedOut=timed_out))+'\n')
        logs.append(name+'\n'+shlex.join(command)+'\nexit='+str(exit_code)+' timedOut='+str(timed_out)+'\n'+stdout+'\n'+stderr)
        destination.mkdir(parents=True,exist_ok=True)
        for file in directory.iterdir():
            if file.is_file():(destination/(file.name+'.gz')).write_bytes(gzip.compress(file.read_bytes(),mtime=0))

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--report-prefix',required=True);parser.add_argument('--known-request',required=True);parser.add_argument('--source-revision',required=True);args=parser.parse_args()
    report=REPORT/args.report_prefix;report.mkdir();known=json.loads(Path(args.known_request).read_text())
    revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    # Negative controls use the same compiled source as the full checks; no rebuild/retries.
    assert not subprocess.check_output(['git','diff','--name-only',args.source_revision,'HEAD','--','tests/Xemele.Tests','src'],cwd=ROOT,text=True).strip()
    assert args.source_revision in known['Expected']['TestVersion'] and args.source_revision in known['Expected']['ProductVersion']
    raw=ROOT/'artifacts/M2-dtd-framing-allocation-probe/child-runs'/('negative-'+uuid.uuid4().hex);raw.mkdir(parents=True)
    env=dict(os.environ)
    for key in KEYS:env.pop(key,None)
    cases=[('unknown-mode','guard','bogus','Invalid internal Framing child guard'),
           ('incomplete-mode','guard','real','Invalid internal Framing child guard'),
           ('missing-required-field','request',None,'required'),
           ('malformed-json','request',None,None),
           ('unknown-json-member','request',None,'could not be mapped'),
           ('provenance-mismatch','request',None,'provenance mismatch'),
           ('host-mismatch','host',None,'Configured dotnet differs'),
           ('token-mismatch','request',None,'Mismatched child capability'),
           ('version-mismatch','request',None,'Mismatched child capability'),
           ('parent-mismatch','request',None,'Invalid child parent identity'),
           ('output-path-mismatch','request',None,'Invalid child output paths'),
           ('nested-unknown-member','request',None,'could not be mapped'),
           ('helper-mismatch','request',None,'provenance mismatch'),
           ('native-mismatch','request',None,'provenance mismatch'),
           ('config-mismatch','request',None,'provenance mismatch'),
           ('gc-mismatch','request',None,'provenance mismatch'),
           ('native-gc-mismatch','request',None,'provenance mismatch'),
           ('gc-configuration-mismatch','request',None,'provenance mismatch'),
           ('test-source-mismatch','request',None,'provenance mismatch'),
           ('product-source-mismatch','request',None,'provenance mismatch')]
    logs=[];summary={'driverRevision':revision,'compiledProvenance':known['Expected'],'checks':{}}
    try:
        for name,kind,mode,message in cases:
            directory=raw/name;directory.mkdir();token=uuid.uuid4().hex
            request=copy.deepcopy(known);request.update(Mode='real',Token=token,ParentPid=os.getpid(),Directory=str(directory),XmlPath=str(directory/'real.xml'),ReportPath=str(directory/'real.report.json'))
            request_path=directory/'real.request.json';extra={}
            if kind=='guard':extra['XEMELE_FRAMING_CHILD_MODE']=mode
            elif kind=='host':extra['XEMELE_DOTNET']='/usr/bin/true'
            else:
                if name=='missing-required-field':del request['Expected']
                if name=='unknown-json-member':request['UnexpectedProtocolField']=True
                if name=='provenance-mismatch':request['Expected']['TestSha256']='0'*64
                if name=='token-mismatch':request['Token']='0'*32
                if name=='version-mismatch':request['Version']=2
                if name=='parent-mismatch':request['ParentPid']=-1
                if name=='output-path-mismatch':request['XmlPath']=str(directory/'other.xml')
                if name=='nested-unknown-member':request['Expected']['UnknownNative']=True
                if name=='helper-mismatch':request['Expected']['HelperIlSha256']='0'*64
                if name=='native-mismatch':request['Expected']['NativeRuntimeSha256']='0'*64
                if name=='config-mismatch':request['Expected']['RuntimeConfigSha256']='0'*64
                if name=='gc-mismatch':request['Expected']['ServerGc']=not request['Expected']['ServerGc']
                if name=='native-gc-mismatch':request['Expected']['NativeGcSha256']='0'*64
                if name=='gc-configuration-mismatch':request['Expected']['GcConfiguration']='{}'
                if name=='test-source-mismatch':request['Expected']['TestVersion']='1.0.0+'+'0'*40
                if name=='product-source-mismatch':request['Expected']['ProductVersion']='1.0.0+'+'0'*40
                request_path.write_text('{broken' if name=='malformed-json' else json.dumps(request))
                extra=dict(XEMELE_FRAMING_CHILD_MODE='real',XEMELE_FRAMING_CHILD_REQUEST=str(request_path),XEMELE_FRAMING_CHILD_TOKEN=token)
            executable=known['Expected']['Executable'];command=[executable]
            if Path(executable).stem=='dotnet':command.append(known['Expected']['TestAssembly'])
            command+=['-noColor','-method',FACT,'-result-xml',str(directory/'real.xml')]
            p=run_case(command,dict(env,**extra),directory,raw/"retained"/name,logs,name)
            tests=list(ET.parse(directory/'real.xml').iter('test'));failure=tests[0].findtext('failure/message') if tests else None
            accepted=p.returncode!=0 and len(tests)==1 and tests[0].get('name')==FACT and tests[0].get('result')=='Fail' and (message is None or message.lower() in (failure or '').lower())
            summary['checks'][name]=dict(result='passed' if accepted else 'failed',actualExit=p.returncode,total=len(tests),failure=failure)
            print(name,summary['checks'][name],flush=True)
            if not accepted:raise AssertionError('Actual negative control failed: '+name)
        return int(any(c['result']!='passed' for c in summary['checks'].values()))
    except Exception as error:
        summary['failure']=repr(error)
        raise
    finally:
        (raw/'checks.txt').write_text('\n'.join(logs));(report/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')

if __name__=='__main__':raise SystemExit(main())
