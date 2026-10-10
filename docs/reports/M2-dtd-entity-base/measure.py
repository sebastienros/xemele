"""Source-owned preparation and exclusive measurement; no retries or job overrides.

Prepare only after HOST RELEASE. Measurement additionally requires a fresh explicit
coordinator grant. Raw logs/generated projects are temporary; summaries retain
complete hashes, selected observations and exact failures, never payload archives.
"""
import argparse
import csv
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shlex
import shutil
import signal
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
OWN = ROOT / 'docs/reports/M2-dtd-entity-base'
PROJECT = OWN / 'Measurements.csproj'
PRODUCT = ROOT / 'src/Xemele/Xemele.csproj'
PARENT = OWN / 'bin/Release/net10.0'
DRIVER = PARENT / 'Xemele.Benchmarks.dll'
FLAGS = ['-c', 'Release', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false']
EXPECTED = {'InternalNestedModel', 'RedirectedComposition'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def closure(root):
    root = Path(root)
    require(root.is_dir(), 'Missing binary/runtime closure: ' + str(root))
    return {str(p.relative_to(root)): digest(p) for p in sorted(root.rglob('*')) if p.is_file()}


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, timeout=30)


def clean_source_tree(revision):
    require(not git('diff', '--name-only', 'HEAD').strip(), 'Tracked source tree is not clean')
    for name in git('ls-files', '--others', '--exclude-standard', '-z').decode().split('\0'):
        if not name:
            continue
        path = ROOT / name
        require(path.parent == OWN and path.suffix == '.json', 'Untracked file outside owned evidence: ' + name)
        value = json.loads(path.read_text())
        require('head' in value and ('sources' in value or 'mode' in value and 'result' in value), 'Unrecognized owned evidence file')


def tracked_inputs(identities):
    return {name: value['gitBlob'] for name, value in identities.items() if 'gitBlob' in value}


def reject_unchanged_success(mode, known):
    for path in OWN.glob('*.json'):
        previous = json.loads(path.read_text())
        if previous.get('mode') != mode or previous.get('result') != 'passed':
            continue
        inputs = previous.get('preparedIdentities', {}).get('sources')
        require(inputs is not None, 'Previous successful report lacks source identity')
        require(tracked_inputs(inputs) != tracked_inputs(known['sources']), 'Unchanged successful observation may not be repeated')


def source_inventory(dotnet, env):
    # MSBuild returns the actual SDK Compile item set, including generated assembly
    # identity sources. Project references are explicitly enumerated for this driver.
    names = set()
    imported = set()
    for project in [PROJECT, PRODUCT]:
        raw = subprocess.check_output([str(dotnet), 'msbuild', str(project), '-p:Configuration=Release',
                                       '-target:ResolveReferences;GenerateGlobalUsings;GenerateAssemblyInfo',
                                       '-getItem:Compile,ReferencePath,Analyzer', '-getProperty:MSBuildAllProjects'],
                                      cwd=ROOT, env=env, timeout=60)
        result = json.loads(raw)
        for item in result['Items']['Compile']:
            path = Path(item['FullPath']).resolve()
            names.add(path)
        for kind in ['ReferencePath', 'Analyzer']:
            names.update(Path(item['FullPath']).resolve() for item in result['Items'][kind])
        obj = project.parent / 'obj'
        names.update(p.resolve() for p in obj.glob('*.nuget.*') if p.is_file())
        names.update(p.resolve() for p in obj.glob('project.assets.json'))
        imported.add(project.resolve())
        imported.update(Path(p).resolve() for p in result['Properties']['MSBuildAllProjects'].split(';') if p)
    names.update(imported)
    names.update(p.resolve() for p in OWN.glob('*.py'))
    names.update((ROOT / n).resolve() for n in ['Directory.Build.props', 'Directory.Packages.props', 'global.json'])
    identities = {}
    for path in sorted(names):
        require(path.is_file(), 'Missing compiled/build input: ' + str(path))
        identity = {'sha256': digest(path)}
        try:
            relative = str(path.relative_to(ROOT))
        except ValueError:
            relative = str(path)
            identity['owner'] = 'sdk/package build import'
        else:
            tracked = subprocess.run(['git', 'ls-files', '--error-unmatch', '--', relative],
                                     cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30).returncode == 0
            if tracked:
                committed = git('show', 'HEAD:' + relative)
                require(hashlib.sha256(committed).hexdigest() == identity['sha256'], 'Source differs from Git: ' + relative)
                identity['gitBlob'] = git('rev-parse', 'HEAD:' + relative).decode().strip()
            else:
                require('/obj/' in '/' + relative or '/bin/' in '/' + relative, 'Untracked compiled input: ' + relative)
                identity['owner'] = 'generated MSBuild input'
        identities[relative] = identity
    return identities


def source_freeze(known):
    result = {}
    for name, identity in known.items():
        path = Path(name) if Path(name).is_absolute() else ROOT / name
        result[name] = dict(identity, sha256=digest(path))
    return result


def stop_group(process):
    for sig in [signal.SIGTERM, signal.SIGKILL]:
        try:
            os.killpg(process.pid, sig)
        except ProcessLookupError:
            pass
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            pass
    for _ in range(100):
        try:
            os.killpg(process.pid, 0)
        except ProcessLookupError:
            return
        time.sleep(.05)
    raise RuntimeError('Process group survived termination deadline')


def run(command, env, raw, summary, total=900, child=180):
    log = raw / ('command-' + str(len(summary.setdefault('commands', []))) + '.log')
    record = {'argv': [str(v) for v in command], 'totalDeadlineSeconds': total, 'childDeadlineSeconds': child}
    summary['commands'].append(record)
    with log.open('w') as output:
        process = subprocess.Popen(record['argv'], cwd=ROOT, env=env, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
        started = time.monotonic()
        child_started = None
        build_started = None
        offset = 0
        carry = ''
        try:
            while process.poll() is None:
                with log.open() as data:
                    data.seek(offset)
                    carry += data.read()
                    offset = data.tell()
                lines = carry.split('\n')
                carry = lines.pop()
                for line in lines:
                    if line.startswith('// start '):
                        build_started = time.monotonic()
                    if '// command took ' in line:
                        build_started = None
                    if line.startswith('// Execute:'):
                        child_started = time.monotonic()
                    if re.match(r'// Benchmark Process \d+ has exited', line):
                        child_started = None
                elapsed = time.monotonic() - started
                require(elapsed <= total, 'Total process deadline exceeded')
                require(child_started is None or time.monotonic() - child_started <= child, 'Benchmark child deadline exceeded')
                require(build_started is None or time.monotonic() - build_started <= 240, 'Generated build deadline exceeded')
                time.sleep(.1)
            record['exit'] = process.wait(timeout=5)
            # Parent exit alone does not establish child/process-group cleanup.
            try:
                os.killpg(process.pid, 0)
            except ProcessLookupError:
                record['processGroupEmpty'] = True
            else:
                raise RuntimeError('Parent exited while process group remains active')
        except BaseException:
            stop_group(process)
            record['terminatedProcessGroup'] = True
            raise
    text = log.read_text(encoding='utf-8', errors='strict')
    require(record['exit'] == 0, 'Nonzero process exit: ' + str(record['exit']))
    return text


def benchmark_results(text, raw, revision, known):
    forbidden = ['There are not any results runs', 'Generate Exception', 'Build Error', 'No Workload Results']
    require(not any(s in text for s in forbidden), 'Benchmark infrastructure failure')
    builds = [int(n) for n in re.findall(r'// command took .*?exited with (-?\d+)', text)]
    require(builds == [0, 0], 'Expected exactly one successful generated restore/build')
    require(re.search(r'Global total time:.*executed benchmarks: 2', text), 'Executed benchmark count mismatch')
    blocks = re.split(r'^// Benchmark: ', text, flags=re.MULTILINE)[1:]
    require(len(blocks) == 2, 'Expected two exact benchmark cases')
    observations = []
    for block in blocks:
        title = block.splitlines()[0]
        found = re.match(r'EntityBaseBenchmarks\.(\w+):', title)
        require(found is not None and found[1] in EXPECTED, 'Unexpected benchmark case')
        case = found[1]
        actual = []
        result = []
        scales = {'ns': 1, 'us': 1000, 'μs': 1000, 'ms': 1000000, 's': 1000000000}
        for stage, target in [('Actual', actual), ('Result', result)]:
            pattern = r'^Workload' + stage + r'\s+(\d+):\s+(\d+) op,\s+([\d.eE+-]+) (ns|us|μs|ms|s),\s+([\d.eE+-]+) (ns|us|μs|ms|s)/op'
            for row in re.finditer(pattern, block, re.MULTILINE):
                value = {'iteration': int(row[1]), 'operations': int(row[2]), 'durationNs': float(row[3]) * scales[row[4]], 'nsPerOperation': float(row[5]) * scales[row[6]]}
                require(value['operations'] > 0 and all(math.isfinite(value[k]) and value[k] > 0 for k in ['durationNs', 'nsPerOperation']), 'Invalid timing observation')
                target.append(value)
        require(len(actual) == len(result) == 3, 'Exact declared observation count mismatch')
        gc = [list(map(int, row)) for row in re.findall(r'^// GC:\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$', block, re.MULTILINE)]
        require(len(gc) == 1 and gc[0][-1] > 0 and all(v['operations'] == gc[0][-1] for v in actual + result), 'GC/operation accounting mismatch')
        exits = [int(v) for v in re.findall(r'^// Benchmark Process \d+ has exited with code (-?\d+)\.', block, re.MULTILINE)]
        require(exits == [0], 'Expected one successful benchmark child')
        proofs = [json.loads(v) for v in re.findall(r'^ENTITY_BASE_PROVENANCE (.+)$', block, re.MULTILINE)]
        require(len(proofs) == 2 and [v['stage'] for v in proofs] == ['before', 'after'], 'Missing exact child before/after proof')
        before, after = proofs
        require({k: v for k, v in before.items() if k != 'stage'} == {k: v for k, v in after.items() if k != 'stage'}, 'Generated child closure changed')
        for field in ['entry', 'driver', 'product']:
            item = before[field]
            require(item['informationalVersion'] == '1.0.0+' + revision, 'Stale generated ' + field + ' source version')
            relative = str(Path(item['path']).relative_to(Path(before['directory'])))
            require(before['closure'].get(relative) == item['sha256'], 'Generated metadata/closure mismatch')
        require(before['runtime']['sha256'] == known['identity']['coreLibrarySha256'], 'Child runtime differs from prepared runtime')
        require(before['driver']['sha256'] == known['parentClosure']['Xemele.Benchmarks.dll'], 'Child driver differs from prepared driver')
        require(before['product']['sha256'] == known['parentClosure']['Xemele.dll'], 'Child production differs from prepared product')
        require(before['generatedInputs'] and any(n.endswith('.notcs') for n in before['generatedInputs']) and any(n.endswith('.csproj') for n in before['generatedInputs']), 'Missing generated source/project inputs')
        execution = re.search(r'^// Execute: (.+) in (.+)$', block, re.MULTILINE)
        require(execution is not None, 'Missing actual execution command')
        args = shlex.split(execution[1])
        executable = next(v for v in args if v.endswith('.dll'))
        actual_path = (Path(execution[2].strip()) / executable).resolve()
        require(actual_path == Path(before['entry']['path']).resolve(), 'Actual executed child identity differs')
        require(closure(before['directory']) == before['closure'], 'Actual generated child files changed after execution')
        observations.append({'case': case, 'actual': actual, 'result': result, 'gc': gc[0], 'childExit': exits[0], 'childProof': before,
                             'environment': [line for line in block.splitlines() if line.startswith(('// Runtime=', '// GC=', '// HardwareIntrinsics='))]})
    require({v['case'] for v in observations} == EXPECTED, 'Duplicate or missing benchmark case')
    rows = []
    for path in (raw / 'bdn').rglob('*-report.csv'):
        with path.open() as data:
            rows.extend(csv.DictReader(data))
    require(len(rows) == 2 and {v['Method'] for v in rows} == EXPECTED, 'CSV exact case mismatch')
    for row in rows:
        require(row.get('Mean') not in [None, '', 'NA', '?'] and row.get('Allocated') not in [None, '', 'NA', '?'], 'Missing timing/allocation result')
    return {'buildExits': builds, 'observations': observations,
            'rows': [{k: v.get(k) for k in ['Method', 'Mean', 'Error', 'StdDev', 'Allocated', 'Gen0', 'Gen1', 'Gen2']} for v in rows]}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--mode', choices=['prepare', 'allocation', 'benchmark'], required=True)
    parser.add_argument('--report', required=True)
    parser.add_argument('--exclusive-grant')
    args = parser.parse_args()
    require(re.fullmatch(r'[a-z0-9-]+', args.report) is not None, 'Invalid report name')
    require(args.mode == 'prepare' or args.exclusive_grant, 'Fresh explicit exclusive coordinator grant required')
    report = OWN / (args.report + '.json')
    require(not report.exists(), 'Report already exists; unchanged successful observations may not be repeated')
    raw = ROOT / 'artifacts/M2-dtd-entity-base' / args.report
    require(not raw.exists(), 'Transient directory already exists')
    raw.mkdir(parents=True)
    summary = {'result': 'failed', 'stage': 'initial', 'mode': args.mode, 'exclusiveGrant': args.exclusive_grant}
    original_directories = {p for p in PARENT.iterdir() if p.is_dir()} if PARENT.exists() else set()
    failure = None
    known = None
    try:
        env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0')
        overrides = {k: v for k, v in env.items() if k.startswith(('DOTNET_', 'COMPlus_'))}
        require(not any(k.startswith('COMPlus_') or any(t in k.upper() for t in ['GC', 'TIERED', 'READYTORUN']) for k in overrides), 'Runtime policy override not permitted')
        summary['runtimeOverrides'] = overrides
        dotnet = Path(shutil.which('dotnet')).resolve(strict=True)
        revision = git('rev-parse', 'HEAD').decode().strip()
        clean_source_tree(revision)
        summary.update(head=revision, dotnet=str(dotnet), dotnetSha256=digest(dotnet))
        preparation = OWN / 'preparation.json'
        if args.mode == 'prepare':
            require(not preparation.exists(), 'Existing preparation requires coordinator review before replacement')
            summary['stage'] = 'build-and-counters-disabled-oracles'
            run([dotnet, 'build', PROJECT, *FLAGS], env, raw, summary, total=240)
            identity = json.loads(run([dotnet, DRIVER, '--identity'], env, raw, summary, total=30))
            require(identity['driver'] == identity['product'] == '1.0.0+' + revision, 'Prepared assembly source version mismatch')
            require(identity['runtime'] == '.NET 10.0.11' and identity['architecture'] == 'Arm64', 'Unexpected pinned runtime/architecture')
            summary['correctnessOutput'] = run([dotnet, DRIVER, '--correctness'], env, raw, summary, total=60).strip()
            require(summary['correctnessOutput'] == 'Complete authored model and exact redirected resolver/output oracles passed; counters disabled.', 'Correctness output mismatch')
            source = source_inventory(dotnet, env)
            runtime_root = Path(identity['coreLibrary']).parent
            known = {'head': revision, 'identity': identity, 'sources': source, 'parentClosure': closure(PARENT),
                     'runtimeDirectory': str(runtime_root), 'runtimeClosure': closure(runtime_root), 'dotnetSha256': digest(dotnet),
                     'sdk': run([dotnet, '--version'], env, raw, summary, total=30).strip()}
            require(known['sdk'] == '10.0.400', 'Unexpected pinned SDK')
        else:
            summary['preparationSha256Before'] = digest(preparation)
            known = json.loads(preparation.read_text())
            require(known['head'] == revision and known['dotnetSha256'] == digest(dotnet), 'Stale preparation/head/host')
            require(source_freeze(known['sources']) == known['sources'], 'Prepared source inputs changed')
            require(closure(PARENT) == known['parentClosure'] and closure(known['runtimeDirectory']) == known['runtimeClosure'], 'Prepared binary/runtime closure changed')
            reject_unchanged_success(args.mode, known)
            summary['stage'] = 'exclusive-execution'
            command = [dotnet, DRIVER, '--allocation'] if args.mode == 'allocation' else [dotnet, DRIVER, '--filter', '*EntityBaseBenchmarks*', '--artifacts', raw / 'bdn', '--keepFiles', '--buildTimeout', '240', '--stopOnFirstError']
            text = run(command, env, raw, summary)
            summary['stage'] = 'strict-result-validation'
            if args.mode == 'allocation':
                values = json.loads(text)
                require(set(values) == {'iterations', 'coldSetupAndOracles', 'ownedModels', 'ownedDocuments', 'positiveControl'}, 'Allocation output shape differs')
                require(values['iterations'] == 1024 and all(type(v) is int and v > 0 for v in values.values()), 'Allocation controls/boundaries failed')
                summary['allocationBytes'] = values
            else:
                summary['benchmarks'] = benchmark_results(text, raw, revision, known)
        summary['stage'] = 'final-freeze'
        summary['sourceIdentitiesAfter'] = source_freeze(known['sources'])
        summary['parentClosureAfter'] = closure(PARENT)
        # Generated directories are new descendants of the parent output folder;
        # compare the original parent payload separately from those transient dirs.
        stable = {k: v for k, v in summary['parentClosureAfter'].items() if k in known['parentClosure']}
        require(summary['sourceIdentitiesAfter'] == known['sources'] and stable == known['parentClosure'], 'Source/parent freeze changed')
        new_directories = ({p for p in PARENT.iterdir() if p.is_dir()} - original_directories)
        extra = set(summary['parentClosureAfter']) - set(known['parentClosure'])
        require(all(any((PARENT / name).is_relative_to(directory) for directory in new_directories) for name in extra), 'Unexpected new parent closure payload')
        require(closure(known['runtimeDirectory']) == known['runtimeClosure'] and digest(dotnet) == known['dotnetSha256'], 'Runtime/host freeze changed')
        require(git('rev-parse', 'HEAD').decode().strip() == revision, 'Git revision changed during execution')
        clean_source_tree(revision)
        summary['preparedIdentities'] = known
        if args.mode != 'prepare':
            summary['preparationSha256After'] = digest(preparation)
            require(summary['preparationSha256After'] == summary['preparationSha256Before'], 'Preparation changed')
        summary['result'] = 'passed'
    except BaseException as error:
        failure = error
        summary['failure'] = {'stage': summary['stage'], 'type': type(error).__name__, 'message': str(error)[:1000]}
        if known:
            try:
                summary['sourceIdentitiesAfter'] = source_freeze(known['sources'])
                summary['parentClosureAfter'] = closure(PARENT)
            except Exception as capture:
                summary['identityCaptureFailure'] = str(capture)[:500]
    finally:
        errors = []
        generated = ({p for p in PARENT.iterdir() if p.is_dir()} - original_directories) if PARENT.exists() else set()
        for path in [raw, *generated]:
            try:
                shutil.rmtree(path)
            except Exception as error:
                errors.append(str(error)[:500])
        summary['temporaryOutputsRemoved'] = not raw.exists() and all(not p.exists() for p in generated)
        if errors:
            summary.update(result='failed', cleanupErrors=errors)
        if summary['result'] == 'passed' and args.mode == 'prepare':
            (OWN / 'preparation.json').write_text(json.dumps(known, indent=2) + '\n')
        report.write_text(json.dumps(summary, indent=2) + '\n')
    if failure is not None:
        raise failure
    require(summary['result'] == 'passed', 'Cleanup failed; see summary')


if __name__ == '__main__':
    main()
