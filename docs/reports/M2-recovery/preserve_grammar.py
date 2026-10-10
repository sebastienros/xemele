"""Route the verified full-grammar checker to this task's evidence directory."""
import importlib.util
from pathlib import Path
import sys
own = Path(__file__).resolve().parent
support = own.parent / 'M2-conformance-grammar/full_grammar.py'
sys.path.insert(0, str(support.parent))
spec = importlib.util.spec_from_file_location('recovery_strict_grammar_support', support)
engine = importlib.util.module_from_spec(spec)
spec.loader.exec_module(engine)
engine.OWN = own
if __name__ == '__main__':
    engine.main()
