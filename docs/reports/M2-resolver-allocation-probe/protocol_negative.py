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
FACT='Xemele.Tests.Resolution.ResolverTests.WarmBorrowedReadsAllocateNothing'
KEYS=['XEMELE_RESOLVER_CHILD_MODE','XEMELE_RESOLVER_CHILD_REQUEST','XEMELE_RESOLVER_CHILD_TOKEN']

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--report-prefix',required=True);parser.add_argument('--known-request',required=True);parser.add_argument('--source-revision',required=True);args=parser.parse_args()
    report=REPORT/args.report_prefix;report.mkdir();known=json.loads(Path(args.known_request).read_text())
    revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    # Negative controls use the same compiled source as the full checks; no rebuild/retries.
    assert not subprocess.check_output(['git','diff','--name-only',args.source_revision,'HEAD','--','tests/Xemele.Tests','src'],cwd=ROOT,text=True).strip()
    assert args.source_revision in known['Expected']['TestVersion'] and args.source_revision in known['Expected']['ProductVersion']
    raw=ROOT/'artifacts/M2-resolver-allocation-probe/child-runs'/('negative-'+uuid.uuid4().hex);raw.mkdir(parents=True)
    env=dict(os.environ)
    for key in KEYS:env.pop(key,None)
    cases=[('unknown-mode','guard','bogus','Invalid internal Resolver child guard'),
           ('incomplete-mode','guard','real','Invalid internal Resolver child guard'),
           ('missing-required-field','request',None,'required'),
           ('malformed-json','request',None,None),
           ('unknown-json-member','request',None,'could not be mapped'),
           ('provenance-mismatch','request',None,'provenance mismatch'),
           ('host-mismatch','host',None,'Configured dotnet differs'),
           ('token-mismatch','request',None,'Mismatched child capability'),
           ('pid-mismatch','request',None,'Invalid child parent identity'),
           ('outside-artifact-root','request',None,'outside owned artifacts'),
           ('extra-runner-argument','request',None,'exact standalone Fact invocation'),
           ('symlink-root-escape','request',None,'outside owned artifacts')]
    logs=[];summary={'driverRevision':revision,'compiledProvenance':known['Expected'],'checks':{}}
    try:
        for name,kind,mode,message in cases:
            directory=raw/name;directory.mkdir();token=uuid.uuid4().hex
            request=copy.deepcopy(known);request.update(Mode='real',Token=token,ParentPid=os.getpid(),Directory=str(directory),XmlPath=str(directory/'real.xml'),ReportPath=str(directory/'real.report.json'))
            request_path=directory/'real.request.json';extra={}
            if kind=='guard':extra['XEMELE_RESOLVER_CHILD_MODE']=mode
            elif kind=='host':extra['XEMELE_DOTNET']='/usr/bin/true'
            else:
                if name=='missing-required-field':del request['Expected']
                if name=='unknown-json-member':request['UnexpectedProtocolField']=True
                if name=='token-mismatch':request['Token']=uuid.uuid4().hex
                if name=='pid-mismatch':request['ParentPid']=0
                if name=='outside-artifact-root':request['Directory']='/private/tmp'
                if name=='symlink-root-escape':
                    (directory/'escape').symlink_to(ROOT,target_is_directory=True)
                    request['Directory']=str(directory/'escape'/'docs')
                if name=='provenance-mismatch':request['Expected']['TestSha256']='0'*64
                request_path.write_text('{broken' if name=='malformed-json' else json.dumps(request))
                extra=dict(XEMELE_RESOLVER_CHILD_MODE='real',XEMELE_RESOLVER_CHILD_REQUEST=str(request_path),XEMELE_RESOLVER_CHILD_TOKEN=token)
            executable=known['Expected']['Executable'];command=[executable]
            if Path(executable).stem=='dotnet':command.append(known['Expected']['TestAssembly'])
            command+=['-noColor','-method',FACT,'-result-xml',str(directory/'real.xml')]
            if name=='extra-runner-argument':command.append('-noLogo')
            p=subprocess.run(command,cwd=ROOT,env=dict(env,**extra),text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=40)
            (directory/'stdout.txt').write_text(p.stdout);(directory/'stderr.txt').write_text(p.stderr)
            logs.append(name+'\n'+shlex.join(command)+'\nextra='+repr(extra)+'\nexit='+str(p.returncode)+'\n'+p.stdout+'\n'+p.stderr)
            for file in directory.iterdir():
                if file.is_file():
                    dest=report/name;dest.mkdir(exist_ok=True);(dest/(file.name+'.gz')).write_bytes(gzip.compress(file.read_bytes(),mtime=0))
            tests=list(ET.parse(directory/'real.xml').iter('test'));failure=tests[0].findtext('failure/message') if tests else None
            accepted=p.returncode!=0 and len(tests)==1 and tests[0].get('name')==FACT and tests[0].get('result')=='Fail' and (message is None or message.lower() in (failure or '').lower())
            summary['checks'][name]=dict(result='passed' if accepted else 'failed',actualExit=p.returncode,total=len(tests),failure=failure)
            print(name,summary['checks'][name],flush=True)
        return int(any(c['result']!='passed' for c in summary['checks'].values()))
    finally:
        (report/'checks.txt.gz').write_bytes(gzip.compress('\n'.join(logs).encode(),mtime=0));(report/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')

if __name__=='__main__':raise SystemExit(main())
