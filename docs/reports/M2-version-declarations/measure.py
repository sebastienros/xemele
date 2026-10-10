"""One-shot committed measurement protocol; requires a fresh explicit exclusive grant."""
import argparse, hashlib, json, math, os, platform, re, shutil, signal, subprocess, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
REPORT=ROOT/'docs/reports/M2-version-declarations'
RAW=REPORT/'.measurement'

class ProtocolError(RuntimeError): pass

def require(value, message):
    if not value: raise ProtocolError(message)
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def snapshot(directory):
    return {str(p.relative_to(directory)):digest(p) for p in sorted(directory.rglob('*')) if p.is_file()}
def generated_inputs(directory):
    return {p.name:digest(p) for p in sorted(directory.iterdir()) if p.is_file() and p.suffix in ('.cs','.notcs','.csproj','.sh','.bat','.config')}
def expected_cases():
    return {('DeclarationVersionBenchmarks','ParseDocument',c,None) for c in ['1.0','1.10','long','invalid']} | {('TextDeclarationVersionBenchmarks','FrameOwned',c,b) for c in ['1.0','1.10','long','absent-version','invalid'] for b in [False,True]}
def case_id(record):
    require(set(record)=={'type','method','corpus','chunked'}, 'Case identity field set differs.')
    require(record['chunked'] is None or type(record['chunked']) is bool, 'Chunked identity must be bool/null.')
    return tuple(record[k] for k in ['type','method','corpus','chunked'])
def validate_variant(known, actual_closure, actual_inputs, metadata, revision, evidence=None):
    record={'expectedRevision':known['revision'],'actualRevision':revision,'expectedClosure':known['childClosure'],'actualClosure':actual_closure,'expectedInputs':known['generatedInputIdentities'],'actualInputs':actual_inputs,'expectedMetadata':known['childMetadata'],'actualMetadata':metadata}
    if evidence is not None:evidence.update(record)
    require(known['revision']==revision,'Generated reference revision differs.')
    require(len(known['childClosure'])==len(actual_closure)==47,'Expected exact complete 47-file child closure.')
    require(actual_closure==known['childClosure'],'Actual generated child raw closure differs from independently built current-source reference.')
    require(actual_inputs==known['generatedInputIdentities'],'Actual generated project/code/script inputs differ.')
    require(metadata==known['childMetadata'],'Actual generated child metadata differs.')
    for name,value in metadata.items():
        require(value['FullVersion']=='1.0.0+'+revision,'Actual generated full informational version differs.')
        require(value['Sha256']==actual_closure[name],'Actual generated metadata hash differs from raw file.')
    require({case_id(c) for c in known['caseIdentities']}==expected_cases() and len(known['caseIdentities'])==14,'Prepared case identities differ.')

def terminate_group(process, summary):
    errors=[]
    for sig in [signal.SIGTERM,signal.SIGKILL]:
        try:os.killpg(process.pid,sig)
        except ProcessLookupError:pass
        except Exception as error:errors.append(str(error))
        try:process.wait(timeout=5)
        except subprocess.TimeoutExpired:pass
        except Exception as error:errors.append(str(error))
    empty=False
    for _ in range(100):
        try:os.killpg(process.pid,0)
        except ProcessLookupError:empty=True;break
        except Exception as error:errors.append(str(error));break
        time.sleep(.05)
    summary['terminatedProcessGroup']=empty
    summary.setdefault('terminatedGroups',[]).append({'pid':process.pid,'empty':empty})
    if not empty:errors.append('Process group survives bounded termination.')
    if errors:summary['terminationErrors']=errors

def require_group_empty(process, summary, timeout_seconds=1):
    deadline=time.monotonic()+timeout_seconds
    while True:
        try:os.killpg(process.pid,0)
        except ProcessLookupError:
            summary.setdefault('processGroups',[]).append({'pid':process.pid,'emptyAfterReap':True})
            return
        except Exception as error:
            summary.setdefault('processGroups',[]).append({'pid':process.pid,'emptyAfterReap':False,'inspectionError':str(error)})
            terminate_group(process,summary)
            raise ProtocolError('Cannot establish empty process group after parent exit.') from error
        if time.monotonic()>=deadline:
            summary.setdefault('processGroups',[]).append({'pid':process.pid,'emptyAfterReap':False,'residualDescendants':True})
            terminate_group(process,summary)
            raise ProtocolError('Zero-exit parent left residual descendants; terminated process group.')
        time.sleep(.05)

def bounded_wait(process, logfile, summary, total_seconds, child_seconds, build_seconds=None):
    start=time.monotonic();child_start=start if build_seconds is None else None;read=0;carry='';build_done=build_seconds is None
    summary['deadlinesSeconds']={'total':total_seconds,'build':build_seconds,'child':child_seconds}
    try:
        while process.poll() is None:
            with logfile.open() as stream:
                stream.seek(read);carry+=stream.read();read=stream.tell()
            lines=carry.split('\n');carry=lines.pop()
            for line in lines:
                if line.startswith('// VersionFrozenChild:'):build_done=True
                if line.startswith('// VersionCaseStart:'):child_start=time.monotonic()
                if line.startswith('// VersionCaseResult:'):child_start=None
            now=time.monotonic();elapsed=now-start
            stage='total' if elapsed>total_seconds else 'build' if not build_done and elapsed>build_seconds else 'benchmark-child' if child_start is not None and now-child_start>child_seconds else None
            if stage:
                summary['timeout']={'stage':stage,'elapsedSeconds':elapsed};raise TimeoutError('Bounded '+stage+' deadline exceeded.')
            time.sleep(.2)
        status=process.wait(timeout=5)
        summary.setdefault('parentExits',[]).append({'pid':process.pid,'exit':status})
        # A nonzero parent may leave children alive even when its own process is reaped.
        if status!=0:terminate_group(process,summary)
        else:require_group_empty(process,summary)
        return status
    except BaseException:
        if not any(g['pid']==process.pid and g['empty'] for g in summary.get('terminatedGroups',[])):terminate_group(process,summary)
        raise

def observations(output, retained=None):
    blocks=re.split(r'^// VersionCaseStart: ',output,flags=re.MULTILINE)[1:]
    rows=[] if retained is None else retained;scales={'ns':1,'us':1000,'μs':1000,'ms':1000000,'s':1000000000}
    for block in blocks:
        row={'actual':[],'result':[],'gc':[],'results':[]};rows.append(row)
        try:row['identity']=json.loads(block.splitlines()[0])
        except Exception as error:
            row['identityParseFailure']={'type':type(error).__name__,'message':str(error),'identityLineSha256':hashlib.sha256(block.splitlines()[0].encode()).hexdigest()}
            raise
        for stage,key in [('Actual','actual'),('Result','result')]:
            pattern=r'^Workload'+stage+r'\s+(\d+):\s+(\d+) op,\s+([\d.eE+-]+)\s+(ns|us|μs|ms|s),\s+([\d.eE+-]+)\s+(ns|us|μs|ms|s)/op\s*$'
            for m in re.finditer(pattern,block,re.MULTILINE):
                row[key].append({'iteration':int(m[1]),'operations':int(m[2]),'durationNs':float(m[3])*scales[m[4]],'nsPerOperation':float(m[5])*scales[m[6]]})
        for m in re.finditer(r'^// GC:\s+(\d+) (\d+) (\d+) (\d+) (\d+)\s*$',block,re.MULTILINE):
            row['gc'].append(dict(zip(['gen0','gen1','gen2','allocatedBytes','operations'],map(int,m.groups()))))
        row['childExits']=[(int(pid),int(code)) for pid,code in re.findall(r'^// Benchmark Process (\d+) has exited with code (-?\d+)\.',block,re.MULTILINE)]
        row['results']=[json.loads(m[1]) for m in re.finditer(r'^// VersionCaseResult: (.+)$',block,re.MULTILINE)]
        row['environment']=[line for line in block.splitlines() if line.startswith(('// Runtime=','// GC=','// HardwareIntrinsics='))]
    return rows

def validate_benchmarks(output):
    rows=observations(output)
    require(len(rows)==14 and {case_id(r['identity']) for r in rows}==expected_cases(),'Exactly 14 expected unique cases required.')
    require(len(re.findall(r'^WorkloadActual\s+\d+:',output,re.MULTILINE))==42,'Exactly 42 raw Actual records required.')
    require(len(re.findall(r'^WorkloadResult\s+\d+:',output,re.MULTILINE))==42,'Exactly 42 raw Result records required.')
    require(len(re.findall(r'^// GC:',output,re.MULTILINE))==14,'Exactly 14 raw GC records required.')
    build_exits=[int(c) for c in re.findall(r'^// command took .* and exited with (-?\d+)',output,re.MULTILINE)]
    require(len(build_exits)==2 and all(c==0 for c in build_exits),'Restore/build exits must both exist and succeed.')
    frozen=[json.loads(m[1]) for m in re.finditer(r'^// VersionFrozenChild: (.+)$',output,re.MULTILINE)]
    require(len(frozen)==1 and frozen[0]['files']==47,'Exactly one complete frozen child binding required.')
    require(re.findall(r'^// VersionExecutedCases: (\d+)\s*$',output,re.MULTILINE)==['14'],'Exact executed-case total required.')
    pids=[]
    for row in rows:
        require(len(row['actual'])==len(row['result'])==3 and len(row['gc'])==1,'Each case requires 3 Actual, 3 Result and 1 GC.')
        require([m['iteration'] for m in row['actual']]==[1,2,3] and [m['iteration'] for m in row['result']]==[1,2,3],'Exact iteration identities required.')
        require(len(row['results'])==len(row['childExits'])==1,'Exactly one actual child result and exit required.')
        result=row['results'][0];pid,code=row['childExits'][0]
        require(case_id(result['identity'])==case_id(row['identity']),'Actual child case identity differs.')
        require(result['exit']==code==0 and result['process']==pid and result['success'] is True and result['executableFound'] is True and result['errors']==[],'Actual child result/exit failed or differs.')
        pids.append(pid)
        require(any('.NET 10.0.11' in line and 'Arm64' in line for line in row['environment']),'Actual benchmark runtime/architecture differs.')
        gc=row['gc'][0];require(gc['operations']>0 and gc['allocatedBytes']>0,'Lifecycle benchmark must record actual positive owned/setup allocation totals.')
        for m in row['actual']+row['result']:
            require(m['operations']==gc['operations'] and m['operations']>0 and math.isfinite(m['durationNs']) and m['durationNs']>0 and math.isfinite(m['nsPerOperation']) and m['nsPerOperation']>0,'Nonfinite or inconsistent workload observations.')
        row['gc'][0]['bytesPerOperation']=gc['allocatedBytes']/gc['operations']
    require(len(set(pids))==14,'Each declared case requires its own actual child process.')
    return {'cases':rows,'actual':42,'result':42,'gc':14,'buildExits':build_exits,'frozenBinding':frozen[0]}

def identity_records(output):
    result={'identityChecks':[],'metadataChecks':[],'parseFailures':[]}
    for line in output.splitlines():
        for marker,key in [('// VersionIdentityCheck: ','identityChecks'),('// VersionMetadataCheck: ','metadataChecks')]:
            if line.startswith(marker):
                try:result[key].append(json.loads(line[len(marker):]))
                except Exception as error:result['parseFailures'].append({'marker':marker,'type':type(error).__name__,'message':str(error)[:300]})
    # Full mismatch maps survive cleanup; repeated successful maps need only counts.
    result['successfulIdentityChecks']=sum(r.get('equal') is True for r in result['identityChecks'])
    result['identityChecks']=[r for r in result['identityChecks'] if r.get('equal') is not True]
    return result

def validate_runtime(expected, actual, evidence=None):
    if evidence is not None:evidence.update({'expected':expected,'actual':actual})
    require(actual==expected,'Actual selected runtime complete payload/loaded identity differs.')
    require(actual['runtime']=='10.0.11' and actual['architecture']=='Arm64','Actual executing runtime/architecture differs.')
    core=actual['coreLib'];directory=Path(actual['runtimeDirectory'])
    require(Path(core['path']).parent==directory and Path(core['path']).name=='System.Private.CoreLib.dll','Loaded CoreLib path does not bind selected runtime directory.')
    require(directory.name=='10.0.11' and core['sha256']==actual['closure']['System.Private.CoreLib.dll'],'Loaded CoreLib identity/payload hash differs.')
    require(core['fullName'] and core['fullVersion'] and core['mvid'] and len(actual['closure'])>100,'Complete selected runtime payload and loaded CoreLib metadata required.')
    require(any(n.endswith(('.dylib','.so')) for n in actual['closure']) and any(n.endswith('.json') for n in actual['closure']),'Selected native/BCL/runtime metadata payload missing.')
    return True

def validate_allocations(output, positive, identity):
    rows=[json.loads(line) for line in output.splitlines() if line.startswith('{')]
    expected=['positive','ordinary-positive'] if positive else ['real','ordinary-real']
    require(len(rows)==2 and [r.get('Mode') for r in rows]==expected,'Exactly two requested allocation boundaries required.')
    long,ordinary=rows
    require(all(type(long[k]) is int for k in ['CoreBytes','DigitBytes','OwnedBytes','VersionLength','WarmCoreConsumed','MeasuredCoreConsumed','WarmFrameConsumed','MeasuredFrameConsumed','CoreCallbacks']),'Long allocation counters must be literal integers.')
    require(all(type(ordinary[k]) is int for k in ['CoreBytes','OwnedBytes','Callbacks']),'Ordinary allocation counters must be literal integers.')
    for row in rows:
        require(row['Product']==identity['Product'] and row['Probe']==identity['Probe'] and row['Runtime']=='10.0.11','Actual allocation child source/binary/runtime differs.')
        require(type(row['ProcessId']) is int and row['ProcessId']>0,'Actual allocation child PID required.')
    require(long['ProcessId']==ordinary['ProcessId'],'Both allocation boundaries must use the same child.')
    require(long['VersionLength']==60002 and long['WarmCoreConsumed']==long['WarmFrameConsumed']==50017 and long['MeasuredCoreConsumed']==10003 and long['MeasuredFrameConsumed']==10000,'Exact long boundary counts differ.')
    require(long['CoreCallbacks']==ordinary['Callbacks']==6 and long['RetainedVersion'] is True,'Complete callback/retention oracle differs.')
    require(long['OwnedBytes']==120064,'Exact long owned version + encoding allocation total differs.')
    if positive:
        require(long['CoreBytes']==long['DigitBytes']==ordinary['CoreBytes']==64 and ordinary['OwnedBytes']==96,'Exact escaping byte[37] positive allocation totals differ.')
    else:
        require(long['CoreBytes']==long['DigitBytes']==ordinary['CoreBytes']==0 and ordinary['OwnedBytes']==32,'Borrowed guard/digit or exact ordinary owned allocation total differs.')
    return rows

def validate_lifecycle(output, positive, known):
    rows=[json.loads(line) for line in output.splitlines() if line.startswith('{')]
    require(len(rows)==1,'Exactly one lifecycle payload required.')
    value=rows[0]
    require(value['mode']==('lifecycle-positive' if positive else 'lifecycle-real'),'Requested lifecycle mode differs.')
    require(value['product']==known['childMetadata']['Xemele.dll'] and value['benchmark']==known['childMetadata']['Xemele.Benchmarks.dll'] and value['probe']==known['generatorIdentity'],'Actual lifecycle generated/source/probe binary metadata differs.')
    require(value['runtime']=='10.0.11' and value['architecture']=='Arm64' and type(value['processId']) is int and value['processId']>0,'Actual lifecycle runtime/architecture/PID differs.')
    cases=value['cases']
    require(len(cases)==14 and {case_id(c['identity']) for c in cases}==expected_cases(),'Exactly 14 actual lifecycle identities required.')
    for row in cases:
        t,m,corpus,chunked=case_id(row['identity'])
        length=4098 if corpus=='long' else len(corpus)
        expected=(22 if corpus=='invalid' else length+28) if t=='DeclarationVersionBenchmarks' else (16 if corpus=='invalid' else 24 if corpus=='absent-version' else length+35)
        require(all(type(row[k]) is int for k in ['iterations','expectedValue','consumed','allocatedBytes','bytesPerOperation']),'Lifecycle counters must be literal integers.')
        require(row['iterations']==512 and row['expectedValue']==expected and row['consumed']==512*expected,'Independent complete consumed/callback lifecycle oracle differs.')
        require(row['allocatedBytes']==512*row['bytesPerOperation'] and row['bytesPerOperation']>0,'Strict actual lifecycle allocation totals differ.')
    return value

def allocation_baselines(summary, known, preparation_sha):
    require(summary['result']=='passed' and summary['mode']=='allocations' and summary['revision']==known['revision'] and summary['preparationSha256']==preparation_sha,'A successful matching one-shot allocation phase is required before timing.')
    payloads=summary['lifecycleAllocations'];require(len(payloads)==2,'Real and escaping-positive lifecycle children required.')
    require(payloads[0]['mode']=='lifecycle-real' and payloads[1]['mode']=='lifecycle-positive' and payloads[0]['processId']!=payloads[1]['processId'],'Lifecycle child modes/PIDs differ.')
    # Revalidate the durable metrics rather than accepting a result label alone.
    for payload in payloads:validate_lifecycle(json.dumps(payload),payload['mode']=='lifecycle-positive',known)
    real={case_id(r['identity']):r['bytesPerOperation'] for r in payloads[0]['cases']}
    positive={case_id(r['identity']):r['bytesPerOperation'] for r in payloads[1]['cases']}
    require(all(positive[k]==real[k]+64 for k in expected_cases()),'Every lifecycle positive control must escape exactly one 64-byte byte[37] per operation.')
    require(summary['before']==summary['after'],'Allocation source/binary/runtime before-after freeze differs.')
    return real

def validate_gc_totals(benchmarks, baselines):
    for row in benchmarks['cases']:
        gc=row['gc'][0];expected=baselines[case_id(row['identity'])]
        require(gc['allocatedBytes']==gc['operations']*expected,'Actual timed GC total differs from directly measured generated-method allocation oracle.')
    return True

def cleanup(summary, paths, remover=shutil.rmtree):
    errors=[]
    for path in paths:
        try:
            if path.exists():remover(path)
        except Exception as error:errors.append({'path':str(path),'type':type(error).__name__,'message':str(error)})
    summary['cleanupErrors']=errors
    summary['temporaryOutputsRemoved']=not errors and all(not p.exists() for p in paths)
    if errors:
        summary.setdefault('failure',{'stage':'cleanup','type':'CleanupError','message':'Transient output cleanup failed.'});summary['result']='failed'

def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--exclusive-grant',required=True)
    parser.add_argument('--mode',choices=['allocations','benchmarks'],required=True)
    args=parser.parse_args(argv)
    summary={'result':'failed','stage':'initial-provenance','exclusiveGrant':args.exclusive_grant,'mode':args.mode,'commands':[]}
    output_summary=REPORT/('measurement-'+args.mode+'-summary.json')
    deadline = time.monotonic() + (300 if args.mode == 'allocations' else 1800)
    # A durable prior attempt is an explicit no-retry boundary; never overwrite it.
    require(not output_summary.exists(),'This mode already has a durable attempt; no retry is allowed.')
    preparation=REPORT/'preparation-summary.json';known=None;logs=[];dotnet=None
    env=dict(os.environ,DOTNET_CLI_USE_MSBUILD_SERVER='0')
    def remaining():
        seconds=deadline-time.monotonic()
        if seconds <= 0:raise TimeoutError('Bounded total mode deadline exceeded.')
        return seconds
    def run(command, limit=30):
        child=subprocess.Popen(list(map(str,command)),cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,start_new_session=True)
        summary['activeCommand']={'command':list(map(str,command)),'pid':child.pid}
        try:stdout,_=child.communicate(timeout=min(limit,remaining()))
        except BaseException:terminate_group(child,summary);raise
        summary['commands'].append({'command':list(map(str,command)),'exit':child.returncode})
        if child.returncode!=0:
            terminate_group(child,summary)
            exception=re.search(r'^Unhandled exception\. (.+)$',stdout,re.MULTILINE)
            summary['commandFailure']={'command':list(map(str,command)),'exit':child.returncode,'primaryException':exception[1][:500] if exception else None}
        else:require_group_empty(child,summary)
        require(child.returncode==0,'Provenance/oracle command failed; exit '+str(child.returncode))
        return stdout
    def freeze():
        require(digest(preparation)==preparation_sha,'Preparation record changed.')
        revision=known['revision']
        require(run(['git','rev-parse','HEAD']).strip()==revision,'HEAD differs from prepared source revision.')
        require({n:digest(ROOT/n) for n in known['sourceIdentities']}==known['sourceIdentities'],'Frozen tracked sources differ.')
        for n in known['sourceIdentities']:
            require((ROOT/n).read_bytes()==subprocess.check_output(['git','show',revision+':'+n],cwd=ROOT,timeout=min(30,remaining())),'Tracked source differs from Git: '+n)
        require(digest(dotnet)==known['sdk']['hostSha256'] and run([dotnet,'--version']).strip()=='10.0.400' and run([dotnet,'--list-runtimes']).splitlines()==known['sdk']['runtimes'],'SDK host/runtime inventory differs.')
        primary_evidence={};summary.setdefault('primaryIdentityChecks',[]).append(primary_evidence)
        for name,expected in known['primaryClosures'].items():
            actual={n:h for n,h in snapshot(RAW/f'build/bin/{name}/release').items() if name != 'BenchmarkDriver' or not n.startswith(known['generatedSubtree']+'/')}
            primary_evidence[name]={'expected':expected,'actual':actual}
            require(actual==expected,'Primary binary closure differs: '+name)
        actual_primary=json.loads(run([dotnet,sample,'--identity']))
        summary.setdefault('primaryMetadataChecks',[]).append({'expected':known['sampleIdentity'],'actual':actual_primary})
        require(actual_primary==known['sampleIdentity'],'Actual primary source/full-version/runtime metadata differs.')
        evidence={'expectedRevision':known['revision'],'actualRevision':revision,'expectedClosure':known['childClosure'],'actualClosure':snapshot(child),'expectedInputs':known['generatedInputIdentities'],'actualInputs':generated_inputs(project),'expectedMetadata':known['childMetadata'],'actualMetadata':{}}
        summary.setdefault('variantIdentityChecks',[]).append(evidence)
        metadata=evidence['actualMetadata']
        for name in known['childMetadata']:
            try:metadata[name]=json.loads(run([dotnet,sample,'--file-identity',child/name]))
            except Exception as error:
                evidence.setdefault('metadataReadFailures',{})[name]={'type':type(error).__name__,'message':str(error)}
                raise
        validate_variant(known,evidence['actualClosure'],evidence['actualInputs'],metadata,revision,evidence)
        runtime_checks={};summary.setdefault('runtimeIdentityChecks',[]).append(runtime_checks)
        for name,binary in [('sample',sample),('generator',generator)]:
            actual_runtime=json.loads(run([dotnet,binary,'--runtime-identity']))
            runtime_checks[name]={'expected':known['runtimeIdentities'][name],'actual':actual_runtime}
            validate_runtime(known['runtimeIdentities'][name],actual_runtime,runtime_checks[name])
            require(actual_runtime['processSha256']==known['sdk']['hostSha256'],'Actual runtime host hash differs from pinned SDK host.')
        require(platform.machine()=='arm64' and known['architecture']=='arm64','Actual host architecture differs.')
        return {'sourceFreeze':True,'primaryClosures':True,'generatedClosure':True,'metadata':metadata,'runtime':'10.0.11','architecture':'arm64','runtimePayloads':{name:row['actual'] for name,row in runtime_checks.items()}}
    try:
        dotnet=Path(shutil.which('dotnet')).resolve()
        require(bool(args.exclusive_grant.strip()),'Explicit grant identifier must be nonempty.')
        known=json.loads(preparation.read_text());preparation_sha=digest(preparation)
        require(known['result']=='ready-and-parked' and known['measured'] is False,'Fresh ready preparation required.')
        require(all(k in known['sourceIdentities'] for k in ['docs/reports/M2-version-declarations/prepare.py','docs/reports/M2-version-declarations/verify.py','docs/reports/M2-version-declarations/measure.py']),'All three committed Python drivers must be frozen.')
        sample=RAW/'build/bin/Sample/release/Xemele.SyntaxSample.dll'
        generator=RAW/'build/bin/BuildOnly/release/VersionBuildOnly.dll'
        benchmark=RAW/'build/bin/BenchmarkDriver/release/Xemele.Benchmarks.dll'
        child=Path(known['generated']['binaries']);project=Path(known['generated']['project']).parent
        summary['revision']=known['revision'];summary['preparationSha256']=preparation_sha;summary['before']=freeze()
        overrides={k:v for k,v in os.environ.items() if k.startswith(('DOTNET_','COMPlus_'))}
        require(not any(k.startswith('COMPlus_') or any(x in k.upper() for x in ['TIERED','GC','READYTORUN']) for k in overrides),'GC/JIT overrides are forbidden.')
        summary['runtimeOverrides']=overrides
        summary['stage']='full-boundary-oracles'
        summary['oracles']=[run([dotnet,sample,'--oracle']),run([dotnet,sample,'--boundary-oracle']),run([dotnet,benchmark,'--oracle'])]
        summary['stage']='measurement'
        if args.mode=='allocations':
            summary['allocations']=[]
            for positive in [False,True]:
                command=[str(dotnet),str(sample),'--allocation']+(['positive'] if positive else [])
                logfile=RAW/('allocation-'+str(positive)+'.log');logs.append(logfile)
                with logfile.open('w') as stream:
                    process=subprocess.Popen(command,cwd=ROOT,env=env,stdout=stream,stderr=subprocess.STDOUT,start_new_session=True)
                    summary['activeCommand']={'command':command,'pid':process.pid}
                    status=bounded_wait(process,logfile,summary,total_seconds=min(120,remaining()),child_seconds=90)
                summary['commands'].append({'command':command,'exit':status});require(status==0,'Allocation child failed.')
                rows=validate_allocations(logfile.read_text(),positive,known['sampleIdentity'])
                require(rows[0]['ProcessId']==process.pid,'Allocation payload PID differs from actual child.')
                summary['allocations'].append(rows)
            require(summary['allocations'][0][0]['ProcessId']!=summary['allocations'][1][0]['ProcessId'],'Real/positive must use distinct fresh children.')
            summary['lifecycleAllocations']=[]
            for positive in [False,True]:
                command=[str(dotnet),str(generator),'--lifecycle',str(child/'Xemele.Benchmarks.dll')]+(['positive'] if positive else [])
                logfile=RAW/('lifecycle-'+str(positive)+'.log');logs.append(logfile)
                with logfile.open('w') as stream:
                    process=subprocess.Popen(command,cwd=ROOT,env=env,stdout=stream,stderr=subprocess.STDOUT,start_new_session=True)
                    summary['activeCommand']={'command':command,'pid':process.pid}
                    status=bounded_wait(process,logfile,summary,total_seconds=min(120,remaining()),child_seconds=90)
                summary['commands'].append({'command':command,'exit':status});require(status==0,'Generated lifecycle allocation child failed.')
                payload=validate_lifecycle(logfile.read_text(),positive,known)
                require(payload['processId']==process.pid,'Lifecycle payload PID differs from actual fresh child.')
                summary['lifecycleAllocations'].append(payload)
            real={case_id(c['identity']):c['bytesPerOperation'] for c in summary['lifecycleAllocations'][0]['cases']}
            positive={case_id(c['identity']):c['bytesPerOperation'] for c in summary['lifecycleAllocations'][1]['cases']}
            require(summary['lifecycleAllocations'][0]['processId']!=summary['lifecycleAllocations'][1]['processId'] and all(positive[k]==real[k]+64 for k in expected_cases()),'Distinct lifecycle children and exact positive deltas required.')
        else:
            allocation_path=REPORT/'measurement-allocations-summary.json'
            allocation_sha=digest(allocation_path)
            baselines=allocation_baselines(json.loads(allocation_path.read_text()),known,preparation_sha)
            summary['allocationOracleSha256']=allocation_sha
            command=[str(dotnet),str(generator),str(benchmark),str(RAW/'generated'),str(RAW/'measurement-generation.json'),'--measure',str(preparation)]
            logfile=RAW/'benchmark.log';logs.append(logfile)
            with logfile.open('w') as stream:
                process=subprocess.Popen(command,cwd=ROOT,env=env,stdout=stream,stderr=subprocess.STDOUT,start_new_session=True)
                summary['activeCommand']={'command':command,'pid':process.pid}
                status=bounded_wait(process,logfile,summary,total_seconds=remaining(),child_seconds=180,build_seconds=240)
            summary['commands'].append({'command':command,'exit':status});require(status==0,'Benchmark parent/build/child failed.')
            summary['streamedIdentityEvidence']=identity_records(logfile.read_text())
            summary['partialObservations']=[]
            observations(logfile.read_text(),summary['partialObservations'])
            summary['benchmarks']=validate_benchmarks(logfile.read_text())
            summary['exactGcAllocationTotals']=validate_gc_totals(summary['benchmarks'],baselines)
            require(digest(allocation_path)==allocation_sha,'Durable allocation oracle changed during timing.')
            binding=summary['benchmarks']['frozenBinding']
            require(binding['child']==str(child) and binding['project']==str(project) and binding['revision']==known['revision'],'Actual executed child binding differs.')
        summary['stage']='after-freeze';summary['after']=freeze();require(summary['after']==summary['before'],'Before/after actual runtime/source/binary freeze differs.')
        summary['result']='passed';summary['timingLimit']='Host-specific informational arm64 timings; no x64 release or speedup claim.'
    except BaseException as error:
        summary['failure']={'stage':summary['stage'],'type':type(error).__name__,'message':str(error)[:1000]}
        if 'process' in locals():summary['parentExitAfterFailure']=process.poll()
        if logs:
            try:
                partial=logs[-1].read_text();summary['streamedIdentityEvidence']=identity_records(partial)
                summary['partialObservations']=[]
                try:observations(partial,summary['partialObservations'])
                except Exception as parse_error:summary['caseParseFailure']={'type':type(parse_error).__name__,'message':str(parse_error)}
                exceptions=re.findall(r'^Unhandled exception\. (.+)$',partial,re.MULTILINE)
                if exceptions:summary['childPrimaryException']=exceptions[0][:500]
                summary['partialAllocationPayloads']=[json.loads(line) for line in partial.splitlines() if line.startswith('{')]
            except Exception as inspection:summary['inspectionError']={'type':type(inspection).__name__,'message':str(inspection)}
    finally:
        # Keep frozen preparation only between two successful one-shot modes.
        # Any failure removes all transient outputs; never retry that observation.
        final_mode=args.mode=='benchmarks' or summary['result']!='passed'
        if final_mode:cleanup(summary,[RAW])
        else:
            errors=[]
            for path in logs:
                try:path.unlink()
                except Exception as error:errors.append(str(error))
            summary['rawLogsRemoved']=not errors
            if errors:summary['cleanupErrors']=errors;summary['result']='failed';summary.setdefault('failure',{'stage':'cleanup','type':'CleanupError','message':'Allocation log cleanup failed.'});cleanup(summary,[RAW])
        try:output_summary.write_text(json.dumps(summary,indent=2)+'\n')
        except Exception as error:
            summary['summaryWriteError']={'type':type(error).__name__,'message':str(error)}
            summary.setdefault('failure',{'stage':'summary-write','type':type(error).__name__,'message':str(error)})
            summary['result']='failed'
    visible=summary if 'summaryWriteError' in summary else {k:summary[k] for k in ['result','mode','revision','failure','rawLogsRemoved','temporaryOutputsRemoved'] if k in summary}
    print(json.dumps(visible,indent=2),flush=True)
    return 0 if summary['result']=='passed' else 1
if __name__=='__main__':raise SystemExit(main())
