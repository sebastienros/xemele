"""Thin task adapter over the verified entity-base preparation/execution guards."""
from pathlib import Path
import types
import json

ROOT = Path(__file__).resolve().parents[3]
support = ROOT / 'docs/reports/M2-dtd-entity-base/measure.py'
engine = types.ModuleType('entity_base_measurement_support')
engine.__file__ = str(support)
# Task-owned artifact routing and declared class labels; reused guards remain intact.
exec(compile(support.read_text().replace("'artifacts/M2-dtd-entity-base'", "'artifacts/M2-conformance-grammar'").replace("EntityBaseBenchmarks", "StandaloneGrammarBenchmarks"), str(support), 'exec'), engine.__dict__)
engine.OWN = Path(__file__).resolve().parent
engine.PROJECT = engine.OWN / 'Xemele.EntityBase.Measurements.csproj'
engine.PARENT = engine.OWN / 'bin/Release/net10.0'
engine.DRIVER = engine.PARENT / 'Xemele.Benchmarks.dll'
original_inventory = engine.source_inventory

def inventory(*args, **kwargs):
    result = original_inventory(*args, **kwargs)
    relative = str(support.relative_to(ROOT))
    committed = engine.git('show', 'HEAD:' + relative)
    engine.require(engine.digest(support) == engine.hashlib.sha256(committed).hexdigest(), 'Reusable support differs from Git')
    result[relative] = {'sha256': engine.digest(support), 'gitBlob': engine.git('rev-parse', 'HEAD:' + relative).decode().strip()}
    return result

engine.source_inventory = inventory
original_run = engine.run

def run(command, *args, **kwargs):
    command = ['*StandaloneGrammarBenchmarks*' if value == '*EntityBaseBenchmarks*' else value for value in command]
    output = original_run(command, *args, **kwargs)
    if '--allocation' in command:
        lines = output.splitlines()
        borrowed = [json.loads(line.removeprefix('GRAMMAR_BORROWED ')) for line in lines if line.startswith('GRAMMAR_BORROWED ')]
        owned = '\n'.join(line for line in lines if not line.startswith('GRAMMAR_BORROWED '))
        summary = kwargs.get('summary', args[2] if len(args) >= 3 else None)
        engine.require(summary is not None, 'Durable allocation summary required')
        # Capture both observed owners before refusal, without changing the
        # inherited exact five-key owned-metric acceptance protocol.
        summary['allocationBytes'] = json.loads(owned)
        summary['borrowedAllocation'] = {'records': borrowed, 'validation': 'failed'}
        engine.require(len(borrowed) == 1 and borrowed[0]['bytes'] == 0 and borrowed[0]['iterations'] == 1024, 'Borrowed allocation proof missing')
        summary['borrowedAllocation']['validation'] = 'passed'
        return owned
    return output

engine.run = run
original_benchmark_results = engine.benchmark_results

def benchmark_results(text, raw, revision, known, summary=None):
    # A reporting-only commit does not change the independently compiled child.
    # Keep actual Git HEAD guards in reused main; its version check must compare
    # the frozen binary compilation revision explicitly recorded by the rebind.
    runtime_revision = known.get('runtimeRevision', revision)
    if runtime_revision != revision:
        binding = known.get('reportingRebind', {})
        engine.require(binding.get('runtimeHead') == runtime_revision and binding.get('reportingHead') == revision,
                       'Reporting-only compilation revision binding missing')
    return original_benchmark_results(text, raw, runtime_revision, known, summary)

engine.benchmark_results = benchmark_results
if __name__ == '__main__':
    engine.main()
