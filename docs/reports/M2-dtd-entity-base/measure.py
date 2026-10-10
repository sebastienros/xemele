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
PROJECT = OWN / 'Xemele.EntityBase.Measurements.csproj'
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


def generated_build_properties(text, generated, summary=None):
    commands = [shlex.split(line[len('// start '):]) for line in text.splitlines() if line.startswith('// start ')]
    context = {'observedCommands': commands}
    if summary is not None:
        summary['generatedBuilderContext'] = context
    builds = [command for command in commands if len(command) > 1 and command[1] == 'build']
    require(len(builds) == 1, 'Exact observed generated Builder command required')
    command = builds[0]
    require(command[command.index('-c') + 1] == 'Release', 'Generated Builder configuration differs')
    properties = {}
    for token in command:
        if token.startswith('/p:'):
            name, value = token[3:].split('=', 1)
            require(name not in properties, 'Duplicate observed Builder property: ' + name)
            properties[name] = value
    expected = {'UseSharedCompilation': 'false', 'Deterministic': 'true', 'Optimize': 'true',
                'ArtifactsPath': generated['buildRoot'] + '/', 'OutDir': generated['proof']['directory'] + '/',
                'OutputPath': generated['proof']['directory'] + '/', 'PublishDir': generated['buildRoot'] + '/publish/'}
    context.update(observedArgv=command, properties=dict(properties, Configuration='Release'), expectedProperties=dict(expected, Configuration='Release'))
    require(properties == expected, 'Observed pinned Builder properties differ from generated artifact paths')
    require(command[-2:] == ['--output', expected['OutDir']], 'Observed generated Builder output differs')
    return context


def inventory_query(command, env, record):
    record['argv'] = command
    process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
    try:
        stdout, stderr = process.communicate(timeout=60)
        record.update(exit=process.returncode, stdoutSha256=hashlib.sha256(stdout).hexdigest(), stderrSha256=hashlib.sha256(stderr).hexdigest(),
                      diagnostics=diagnostic_facts(stdout.decode(errors='replace') + '\n' + stderr.decode(errors='replace')))
        try:
            os.killpg(process.pid, 0)
        except ProcessLookupError:
            record['processGroupEmpty'] = True
        else:
            raise RuntimeError('Inventory parent exited while process group remains active')
    except BaseException as error:
        record.update(exit=process.poll(), failure=type(error).__name__ + ': ' + str(error)[:500])
        if isinstance(error, subprocess.TimeoutExpired):
            record['diagnostics'] = diagnostic_facts((error.stdout or b'').decode(errors='replace') + '\n' + (error.stderr or b'').decode(errors='replace'))
        try:
            stop_group(process)
            record['terminatedProcessGroup'] = True
        except Exception as cleanup:
            record['processCleanupFailure'] = str(cleanup)[:500]
        raise
    require(process.returncode == 0, 'Inventory query nonzero exit: ' + str(process.returncode))
    require(len(stdout) + len(stderr) <= 64 * 1024 * 1024, 'Inventory output size exceeded')
    return json.loads(stdout)


def source_inventory(dotnet, env, projects=None, properties=None, summary=None):
    # Evaluate the generated variant with the SAME global properties that its
    # independently observed pinned Builder used; defaults describe another build.
    names = set()
    imported = set()
    properties = properties or {'Configuration': 'Release'}
    for project in projects or [PROJECT, PRODUCT]:
        record = {'project': str(project), 'globalProperties': properties}
        if summary is not None:
            summary.setdefault('inventoryQueries', []).append(record)
        requested = ['MSBuildAllProjects', 'ProjectAssetsFile', 'MSBuildProjectExtensionsPath', *properties]
        command = [str(dotnet), 'msbuild', str(project), *['-p:' + k + '=' + v for k, v in properties.items()],
                   '-target:ResolveReferences;GenerateGlobalUsings;GenerateAssemblyInfo',
                   '-getItem:Compile,ReferencePath,Analyzer', '-getProperty:' + ','.join(requested)]
        result = inventory_query(command, env, record)
        evaluated = result['Properties']
        record['evaluatedProperties'] = evaluated
        require(all(evaluated.get(name) == value for name, value in properties.items()), 'Evaluated build global properties differ')
        for kind in ['Compile', 'ReferencePath', 'Analyzer']:
            names.update(Path(item['FullPath']).resolve() for item in result['Items'][kind])
        def evaluated_path(name):
            value = evaluated.get(name)
            require(value, 'Missing evaluated MSBuild property: ' + name)
            path = Path(value)
            return (path if path.is_absolute() else project.parent / path).resolve()
        assets = evaluated_path('ProjectAssetsFile')
        extensions = evaluated_path('MSBuildProjectExtensionsPath')
        require(assets.is_file(), 'Missing actual evaluated assets: ' + str(assets))
        require(extensions.is_dir(), 'Missing actual evaluated NuGet extensions: ' + str(extensions))
        names.add(assets)
        names.update(p.resolve() for p in extensions.glob('*.nuget.*') if p.is_file())
        imported.add(project.resolve())
        imported.update(Path(p).resolve() for p in evaluated['MSBuildAllProjects'].split(';') if p)
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


def parent_directories():
    return {p for p in PARENT.iterdir() if p.is_dir()} if PARENT.exists() else set()


def cleanup_outputs(outputs):
    errors = []
    for path in outputs:
        try:
            shutil.rmtree(path)
        except Exception as error:
            errors.append(str(error)[:500])
    return errors


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


def diagnostic_facts(text):
    # Retain concise infrastructure diagnostics before raw cleanup, including
    # generator/builder exceptions that precede every structured proof record.
    diagnostics = [line for line in text.splitlines()
                   if re.search(r'\b(?:error|exception|timeout|timed out|Unhandled)\b', line, re.IGNORECASE)]
    return {
        'diagnostics': [line[:1000] for line in diagnostics[:20]],
        'diagnosticLineCount': len(diagnostics),
        'diagnosticsTruncated': len(diagnostics) > 20 or any(len(line) > 1000 for line in diagnostics),
    }


def output_facts(log):
    text = log.read_text(encoding='utf-8', errors='strict')
    return {
        **diagnostic_facts(text),
        'generatedBuildExits': [int(v) for v in re.findall(r'// command took .*?exited with (-?\d+)', text)],
        'childExits': [int(v) for v in re.findall(r'// Benchmark Process \d+ has exited with code (-?\d+)', text)],
        'cases': re.findall(r'^// Benchmark: EntityBaseBenchmarks\.(\w+):', text, re.MULTILINE),
        'actualObservationCount': len(re.findall(r'^WorkloadActual\s+\d+:', text, re.MULTILINE)),
        'resultObservationCount': len(re.findall(r'^WorkloadResult\s+\d+:', text, re.MULTILINE)),
        'gcRecords': [list(map(int, values)) for values in re.findall(r'^// GC:\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$', text, re.MULTILINE)],
        'provenanceRecordCount': len(re.findall(r'^ENTITY_BASE_PROVENANCE ', text, re.MULTILINE)),
        'infrastructureFailureFlags': [v for v in ['There are not any results runs', 'Generate Exception', 'Build Error', 'No Workload Results'] if v in text],
        'provenanceRecords': records(text, 'ENTITY_BASE_PROVENANCE'),
        'gateFailures': records(text, 'ENTITY_BASE_GATE_FAILURE'),
        'buildOnlyRecords': records(text, 'ENTITY_BASE_BUILD_ONLY'),
        'projectBindings': records(text, 'ENTITY_BASE_PROJECT_BINDING'),
    }


def records(text, prefix):
    values = []
    for line in re.findall(r'^' + prefix + r' (.+)$', text, re.MULTILINE):
        try:
            values.append(json.loads(line))
        except (ValueError, TypeError) as error:
            values.append({'recordParseFailure': type(error).__name__, 'recordSha256': hashlib.sha256(line.encode()).hexdigest()})
    return values


def mismatch(expected, actual):
    return {'missing': sorted(set(expected) - set(actual)), 'extra': sorted(set(actual) - set(expected)),
            'changed': [{'file': name, 'expected': expected[name], 'actual': actual[name]}
                        for name in sorted(set(expected) & set(actual)) if expected[name] != actual[name]]}


def phase(summary, name):
    prior = summary['stage']
    if summary['phases'].get(prior) == 'running':
        summary['phases'][prior] = 'passed'
    summary['stage'] = name
    summary['phases'][name] = 'running'


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
                require(log.stat().st_size <= 64 * 1024 * 1024, 'Bounded output size exceeded')
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
            try:
                stop_group(process)
                record['terminatedProcessGroup'] = True
            except Exception as cleanup:
                record['processCleanupFailure'] = str(cleanup)[:500]
            record['exit'] = process.poll()
            try:
                record['outputFacts'] = output_facts(log)
            except Exception as capture:
                record['outputFactsFailure'] = str(capture)[:500]
            raise
    text = log.read_text(encoding='utf-8', errors='strict')
    record['outputFacts'] = output_facts(log)
    require(record['exit'] == 0, 'Nonzero process exit: ' + str(record['exit']))
    return text


def benchmark_results(text, raw, revision, known, summary=None):
    # Collect every available observation/proof BEFORE any acceptance guard. The
    # caller's summary remains populated if a later guard throws and raw is deleted.
    report = {'buildExits': [int(v) for v in re.findall(r'// command took .*?exited with (-?\d+)', text)],
              'observations': [], 'rows': [], 'validation': 'unrun',
              'projectBindings': records(text, 'ENTITY_BASE_PROJECT_BINDING'),
              'expectedGenerated': known.get('generated'), 'gateFailures': records(text, 'ENTITY_BASE_GATE_FAILURE')}
    if summary is not None:
        summary['benchmarks'] = report
    scales = {'ns': 1, 'us': 1000, 'μs': 1000, 'ms': 1000000, 's': 1000000000}
    for block in re.split(r'^// Benchmark: ', text, flags=re.MULTILINE)[1:]:
        found = re.match(r'EntityBaseBenchmarks\.(\w+):', block.splitlines()[0])
        row = {'case': found[1] if found else None, 'actual': [], 'result': [],
               'gc': [list(map(int, values)) for values in re.findall(r'^// GC:\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$', block, re.MULTILINE)],
               'childExits': [int(v) for v in re.findall(r'^// Benchmark Process \d+ has exited with code (-?\d+)\.', block, re.MULTILINE)],
               'proofs': records(block, 'ENTITY_BASE_PROVENANCE'), 'comparisons': [],
               'environment': [line for line in block.splitlines() if line.startswith(('// Runtime=', '// GC=', '// HardwareIntrinsics='))]}
        report['observations'].append(row)
        for stage in ['Actual', 'Result']:
            pattern = r'^Workload' + stage + r'\s+(\d+):\s+(\d+) op,\s+([\d.eE+-]+) (ns|us|μs|ms|s),\s+([\d.eE+-]+) (ns|us|μs|ms|s)/op'
            for match in re.finditer(pattern, block, re.MULTILINE):
                duration = float(match[3]) * scales[match[4]]
                per_operation = float(match[5]) * scales[match[6]]
                row[stage.lower()].append({'iteration': int(match[1]), 'operations': int(match[2]),
                                          'durationNs': duration if math.isfinite(duration) else 'nonfinite:' + match[3],
                                          'nsPerOperation': per_operation if math.isfinite(per_operation) else 'nonfinite:' + match[5]})
        execution = re.search(r'^// Execute: (.+) in (.+)$', block, re.MULTILINE)
        row['actualExecution'] = {'argv': shlex.split(execution[1]), 'cwd': execution[2].strip()} if execution else None
        expected = known.get('generated', {}).get('proof', {})
        for proof in row['proofs']:
            actual = proof.get('actual', {})
            comparisons = {field: {'expected': expected.get(field), 'actual': actual.get(field)}
                           for field in ['directory', 'projectDirectory', 'entry', 'driver', 'product', 'runtime']
                           if expected.get(field) != actual.get(field)}
            for field in ['closure', 'generatedInputs']:
                comparisons[field] = mismatch(expected.get(field, {}), actual.get(field, {}))
            row['comparisons'].append({'stage': proof.get('stage'), 'differences': comparisons})
    for path in (raw / 'bdn').rglob('*-report.csv'):
        with path.open() as data:
            report['rows'].extend({k: v.get(k) for k in ['Method', 'Mean', 'Error', 'StdDev', 'Allocated', 'Gen0', 'Gen1', 'Gen2']}
                                  for v in csv.DictReader(data))
    report['validation'] = 'in_progress'
    require(report['projectBindings'] == [known.get('generated', {}).get('projectBinding')], 'Actual generated project binding differs from independent preparation')
    require(not any(v in text for v in ['There are not any results runs', 'Generate Exception', 'Build Error', 'No Workload Results']), 'Benchmark infrastructure failure')
    require(report['buildExits'] == [0, 0], 'Expected exactly one successful generated restore/build')
    require(re.search(r'Global total time:.*executed benchmarks: 2', text), 'Executed benchmark count mismatch')
    require(len(report['observations']) == 2, 'Expected two exact benchmark cases')
    expected = known['generated']['proof']
    for row in report['observations']:
        require(row['case'] in EXPECTED, 'Unexpected benchmark case')
        require(len(row['actual']) == len(row['result']) == 3, 'Exact declared observation count mismatch')
        for value in row['actual'] + row['result']:
            require(value['operations'] > 0 and all(isinstance(value[k], (float, int)) and math.isfinite(value[k]) and value[k] > 0 for k in ['durationNs', 'nsPerOperation']), 'Invalid timing observation')
        require(len(row['gc']) == 1 and row['gc'][0][-1] > 0 and all(v['operations'] == row['gc'][0][-1] for v in row['actual'] + row['result']), 'GC/operation accounting mismatch')
        require(row['childExits'] == [0], 'Expected one successful benchmark child')
        require(len(row['proofs']) == 2 and [v.get('stage') for v in row['proofs']] == ['before', 'after'], 'Missing exact child before/after proof')
        before, after = row['proofs']
        require(before.get('gatePassed') is True and after.get('gatePassed') is True, 'Independent child gate failed before workload')
        require(before['manifestSha256'] == after['manifestSha256'] == known['preparationSha256'], 'Child manifest hash differs from approved preparation')
        require(before['actual'] == after['actual'] == expected, 'Independent generated child closure/input/metadata mismatch')
        expected_compiled = {name: value['sha256'] for name, value in known['generated']['compiledInputs'].items()}
        row['compiledInputMismatch'] = mismatch(expected_compiled, before.get('compiledInputs', {}))
        require(before.get('compiledInputs') == after.get('compiledInputs') == expected_compiled, 'Generated compiled source/build/reference inputs differ')
        for field in ['entry', 'driver', 'product']:
            item = before['actual'][field]
            require(item['informationalVersion'] == '1.0.0+' + revision, 'Stale generated ' + field + ' source version')
            relative = str(Path(item['path']).relative_to(Path(expected['directory'])))
            require(expected['closure'].get(relative) == item['sha256'], 'Generated metadata/closure mismatch')
        require(expected['runtime']['sha256'] == known['identity']['coreLibrarySha256'], 'Child runtime differs from prepared runtime')
        execution = row['actualExecution']
        require(execution is not None, 'Missing actual execution command')
        executable = next(v for v in execution['argv'] if v.endswith('.dll'))
        require((Path(execution['cwd']) / executable).resolve() == Path(expected['entry']['path']).resolve(), 'Actual executed child identity differs')
        actual_closure = closure(expected['directory'])
        row['finalChildClosure'] = actual_closure
        row['finalChildMismatch'] = mismatch(expected['closure'], actual_closure)
        require(actual_closure == expected['closure'], 'Actual generated child files changed after execution')
    require({v['case'] for v in report['observations']} == EXPECTED, 'Duplicate or missing benchmark case')
    require(len(report['rows']) == 2 and {v['Method'] for v in report['rows']} == EXPECTED, 'CSV exact case mismatch')
    for row in report['rows']:
        require(row['Mean'] not in [None, '', 'NA', '?'] and row['Allocated'] not in [None, '', 'NA', '?'], 'Missing timing/allocation result')
    report['validation'] = 'passed'
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--mode', choices=['prepare', 'allocation', 'benchmark'], required=True)
    parser.add_argument('--report', required=True)
    parser.add_argument('--exclusive-grant')
    parser.add_argument('--benchmark-report')
    args = parser.parse_args()
    require(re.fullmatch(r'[a-z0-9-]+', args.report) is not None, 'Invalid report name')
    require(args.mode == 'prepare' or args.exclusive_grant, 'Fresh explicit exclusive coordinator grant required')
    require(args.mode != 'prepare' or args.benchmark_report and re.fullmatch(r'[a-z0-9-]+', args.benchmark_report), 'Preparation requires exact intended benchmark report name')
    report = OWN / (args.report + '.json')
    require(not report.exists(), 'Report already exists; unchanged successful observations may not be repeated')
    raw = ROOT / 'artifacts/M2-dtd-entity-base' / args.report
    require(not raw.exists(), 'Transient directory already exists')
    raw.mkdir(parents=True)
    summary = {'result': 'failed', 'stage': 'initial', 'mode': args.mode, 'exclusiveGrant': args.exclusive_grant}
    phases = ['initial', 'build-and-counters-disabled-oracles', 'independent-generation-build-only', 'final-freeze'] if args.mode == 'prepare' else ['initial', 'exclusive-execution', 'strict-result-validation', 'final-freeze']
    summary['phases'] = {name: 'unrun' for name in phases}
    summary['phases']['initial'] = 'running'
    original_directories = parent_directories()
    failure = None
    known = None
    future_artifacts = None
    try:
        env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', XEMELE_ENTITY_BASE_PROJECT=str(PROJECT))
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
            phase(summary, 'build-and-counters-disabled-oracles')
            try:
                run([dotnet, 'build', PROJECT, *FLAGS], env, raw, summary, total=240)
            finally:
                original_directories = parent_directories()
            # Parent build may restore new dependency directories. They are
            # expected parent payload, not transient generated child outputs.
            identity = json.loads(run([dotnet, DRIVER, '--identity'], env, raw, summary, total=30))
            require(identity['driver'] == identity['product'] == '1.0.0+' + revision, 'Prepared assembly source version mismatch')
            require(identity['runtime'] == '.NET 10.0.11' and identity['architecture'] == 'Arm64', 'Unexpected pinned runtime/architecture')
            summary['correctnessOutput'] = run([dotnet, DRIVER, '--correctness'], env, raw, summary, total=60).strip()
            require(summary['correctnessOutput'] == 'Complete authored model and exact redirected resolver/output oracles passed; counters disabled.', 'Correctness output mismatch')
            source = source_inventory(dotnet, env, summary=summary)
            runtime_root = Path(identity['coreLibrary']).parent
            known = {'head': revision, 'repositoryRoot': str(ROOT), 'identity': identity, 'sources': source, 'parentClosure': closure(PARENT),
                     'runtimeDirectory': str(runtime_root), 'runtimeClosure': closure(runtime_root), 'dotnetSha256': digest(dotnet),
                     'sdk': run([dotnet, '--version'], env, raw, summary, total=30).strip()}
            # Preserve the independently captured parent/source/runtime expectation
            # even when generation fails before it can publish a child manifest.
            summary['preparedIdentities'] = known
            require(known['sdk'] == '10.0.400', 'Unexpected pinned SDK')
            phase(summary, 'independent-generation-build-only')
            future_artifacts = ROOT / 'artifacts/M2-dtd-entity-base' / args.benchmark_report
            require(not future_artifacts.exists(), 'Intended benchmark artifact path already exists')
            known['intendedBenchmarkReport'] = args.benchmark_report
            known['generatedArtifactsPath'] = str(future_artifacts / 'bdn')
            text = run([dotnet, DRIVER, '--generate-build-only', known['generatedArtifactsPath']], env, raw, summary, total=300)
            candidates = records(text, 'ENTITY_BASE_BUILD_ONLY')
            summary['actualGeneratedBuildOnly'] = candidates
            bindings = records(text, 'ENTITY_BASE_PROJECT_BINDING')
            summary['actualProjectBindings'] = bindings
            require(len(bindings) == 1 and bindings[0].get('passed') is True and bindings[0].get('expected') == str(PROJECT) and bindings[0].get('actual') == [str(PROJECT)], 'Generated owned project binding differs')
            require(len(candidates) == 1, 'Exact independent generated manifest record required')
            generated = candidates[0]
            known['generated'] = generated
            generated['projectBinding'] = bindings[0]
            require(generated.get('countersDisabled') is True and generated.get('executorInvoked') is False and set(generated.get('cases', [])) == EXPECTED, 'Preparation executed a workload or changed cases')
            for field in ['entry', 'driver', 'product']:
                require(generated['proof'][field]['informationalVersion'] == '1.0.0+' + revision, 'Independent generated source version differs: ' + field)
            for field in ['generatedProject', 'generatedCode', 'generatedScript']:
                path = Path(generated[field])
                require(path.parent == Path(generated['buildRoot']) and digest(path) == generated['proof']['generatedInputs'].get(path.name), 'Missing independent generated project/code/script')
            require(Path(generated['generatedCode']).suffix == '.notcs', 'Generated code must be independently captured .notcs input')
            generated['builderContext'] = generated_build_properties(text, generated, summary)
            require(generated['proof']['runtime']['informationalVersion'] == known['identity']['coreRuntimeBuild'], 'Independent child runtime full version differs from executing runtime')
            generated['compiledInputs'] = source_inventory(dotnet, env, [Path(generated['generatedProject']), PROJECT, PRODUCT], generated['builderContext']['properties'], summary)
            # Capture the complete independently built child then discard every
            # generated payload. Later timing regenerates; it cannot adopt its
            # own freshly observed bytes as its approved expected identity.
            require(Path(generated['buildRoot']).is_relative_to(PARENT), 'Generated cleanup directory outside parent output')
            shutil.rmtree(generated['buildRoot'])
        else:
            summary['preparationSha256Before'] = digest(preparation)
            known = json.loads(preparation.read_text())
            summary['preparedIdentities'] = known
            known['preparationSha256'] = summary['preparationSha256Before']
            require(known['head'] == revision and known['dotnetSha256'] == digest(dotnet), 'Stale preparation/head/host')
            require(source_freeze(known['sources']) == known['sources'], 'Prepared source inputs changed')
            require(closure(PARENT) == known['parentClosure'] and closure(known['runtimeDirectory']) == known['runtimeClosure'], 'Prepared binary/runtime closure changed')
            reject_unchanged_success(args.mode, known)
            if args.mode == 'benchmark':
                require(args.report == known['intendedBenchmarkReport'] and str(raw / 'bdn') == known['generatedArtifactsPath'], 'Benchmark path differs from approved generated preparation')
                env['XEMELE_ENTITY_BASE_PREPARATION'] = str(preparation)
                env['XEMELE_ENTITY_BASE_PREPARATION_SHA256'] = summary['preparationSha256Before']
            phase(summary, 'exclusive-execution')
            command = [dotnet, DRIVER, '--allocation'] if args.mode == 'allocation' else [dotnet, DRIVER, '--filter', '*EntityBaseBenchmarks*', '--artifacts', raw / 'bdn', '--keepFiles', '--buildTimeout', '240', '--stopOnFirstError']
            text = run(command, env, raw, summary)
            phase(summary, 'strict-result-validation')
            if args.mode == 'allocation':
                values = json.loads(text)
                summary['allocationBytes'] = values
                require(set(values) == {'iterations', 'coldSetupAndOracles', 'ownedModels', 'ownedDocuments', 'positiveControl'}, 'Allocation output shape differs')
                require(values['iterations'] == 1024 and all(type(v) is int and v > 0 for v in values.values()), 'Allocation controls/boundaries failed')
                summary['allocationBytes'] = values
            else:
                benchmark_results(text, raw, revision, known, summary)
        phase(summary, 'final-freeze')
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
        summary['phases']['final-freeze'] = 'passed'
    except BaseException as error:
        failure = error
        summary['failure'] = {'stage': summary['stage'], 'type': type(error).__name__, 'message': str(error)[:1000]}
        summary['phases'][summary['stage']] = 'failed'
        if summary.get('benchmarks', {}).get('validation') == 'in_progress':
            summary['benchmarks']['validation'] = 'failed'
        summary['unrunPhases'] = [name for name, status in summary['phases'].items() if status == 'unrun']
        if known:
            try:
                summary['sourceIdentitiesAfter'] = source_freeze(known['sources'])
                summary['parentClosureAfter'] = closure(PARENT)
            except Exception as capture:
                summary['identityCaptureFailure'] = str(capture)[:500]
    finally:
        generated = parent_directories() - original_directories
        outputs = [raw, *generated]
        if future_artifacts is not None and future_artifacts.exists():
            outputs.append(future_artifacts)
        errors = cleanup_outputs(outputs)
        summary['temporaryOutputsRemoved'] = all(not p.exists() for p in outputs)
        if known:
            try:
                actual = closure(PARENT)
                summary['parentClosureAfterCleanup'] = actual
                summary['parentCleanupDifferences'] = mismatch(known['parentClosure'], actual)
                require(actual == known['parentClosure'], 'Cleanup changed frozen parent closure')
            except Exception as error:
                errors.append(str(error)[:500])
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
