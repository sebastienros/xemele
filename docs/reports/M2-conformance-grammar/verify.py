"""Thin scoped adapter over verified correctness/API/package support."""
from pathlib import Path
import hashlib
import types
import zipfile

ROOT = Path(__file__).resolve().parents[3]
support = ROOT / 'docs/reports/M2-undeclared-content/verify.py'
engine = types.ModuleType('grammar_verification_support')
engine.__file__ = str(support)
# Retain inherited consumers/contracts and held allocation identities. Route only
# report/branch labels, and inspect package byte identities before raw cleanup.
source = support.read_text().replace("'codex/m2-undeclared-content'", "'codex/m2-conformance-grammar'").replace("Xemele.Tests.UndeclaredReferences.", "Xemele.Tests.ConformanceGrammar.")
anchor = "assert source==summary['packageConsumer']"
assert source.count(anchor) == 1
addition = """
                with zipfile.ZipFile(package) as archive:
                    packaged = archive.read('lib/net10.0/Xemele.dll')
                identities = {'sourceAssembly': digest(runtime), 'packageAssembly': hashlib.sha256(packaged).hexdigest(),
                              'sourceConsumerAssembly': digest(sample.parent/'Xemele.dll'),
                              'freshPackageConsumerAssembly': digest(package_sample.parent/'Xemele.dll')}
                cached = list((temporary/'packages').rglob('Xemele.dll'))
                assert len(cached) == 1, 'Exact fresh cache assembly required'
                identities['freshCacheAssembly'] = digest(cached[0])
                summary['sourcePackageAssemblyIdentities'] = identities
                assert len(set(identities.values())) == 1, 'Source/feed/cache/consumer assembly bytes differ'
"""
source = source.replace(anchor, anchor + addition)
exec(compile(source, str(support), 'exec'), engine.__dict__)
engine.zipfile = zipfile
engine.OWN = Path(__file__).resolve().parent
if __name__ == '__main__':
    committed = engine.subprocess.check_output(['git', 'show', 'HEAD:' + str(support.relative_to(ROOT))], cwd=ROOT)
    assert hashlib.sha256(committed).digest() == hashlib.sha256(support.read_bytes()).digest()
    engine.main()
