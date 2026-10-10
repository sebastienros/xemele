"""Thin task adapter over the verified entity-base preparation/execution guards."""
from pathlib import Path
import types
import json

ROOT = Path(__file__).resolve().parents[3]
support = ROOT / 'docs/reports/M2-dtd-entity-base/measure.py'
engine = types.ModuleType('entity_base_measurement_support')
engine.__file__ = str(support)
# Only task-owned transient artifact routing differs; all guards/control flow are reused.
exec(compile(support.read_text().replace("'artifacts/M2-dtd-entity-base'", "'artifacts/M2-dtd-nonvalidating'").replace("EntityBaseBenchmarks", "NonvalidatingBenchmarks"), str(support), 'exec'), engine.__dict__)
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
    command = ['*NonvalidatingBenchmarks*' if value == '*EntityBaseBenchmarks*' else value for value in command]
    output = original_run(command, *args, **kwargs)
    if '--allocation' in command:
        lines = output.splitlines()
        borrowed = [json.loads(line.removeprefix('NONVALIDATING_BORROWED ')) for line in lines if line.startswith('NONVALIDATING_BORROWED ')]
        engine.require(len(borrowed) == 1 and borrowed[0]['bytes'] == 0 and borrowed[0]['iterations'] == 1024, 'Borrowed allocation proof missing')
        owned = json.loads('\n'.join(line for line in lines if not line.startswith('NONVALIDATING_BORROWED ')))
        owned['borrowed'] = borrowed[0]
        return json.dumps(owned)
    return output

engine.run = run
if __name__ == '__main__':
    engine.main()
