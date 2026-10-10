"""Thin scoped adapter over verified correctness/API/package support."""
from pathlib import Path
import hashlib
import types

ROOT = Path(__file__).resolve().parents[3]
support = ROOT / 'docs/reports/M2-undeclared-content/verify.py'
engine = types.ModuleType('grammar_verification_support')
engine.__file__ = str(support)
# Retain all inherited consumer/contract checks and held allocation identities.
# Only route reports and the branch guard to the current task.
exec(compile(support.read_text().replace("'codex/m2-undeclared-content'", "'codex/m2-conformance-grammar'").replace("Xemele.Tests.UndeclaredReferences.", "Xemele.Tests.ConformanceGrammar."), str(support), 'exec'), engine.__dict__)
engine.OWN = Path(__file__).resolve().parent
if __name__ == '__main__':
    committed = engine.subprocess.check_output(['git', 'show', 'HEAD:' + str(support.relative_to(ROOT))], cwd=ROOT)
    assert hashlib.sha256(committed).digest() == hashlib.sha256(support.read_bytes()).digest()
    engine.main()
