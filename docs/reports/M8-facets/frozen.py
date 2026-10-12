"""Compare every frozen datatype consumer member against the real implementation."""
import json, subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[3]
output=root/'artifacts/M8-facets/correctness/runtime-internal.txt'
output.parent.mkdir(parents=True,exist_ok=True)
subprocess.run(['/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet','--fx-version','10.0.11',str(root/'docs/reports/M8-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll'),'emit',str(root/'src/Xemele/bin/Release/net10.0/Xemele.dll'),str(output)],check=True)
def rows(path): return {r['id']:r for r in (json.loads(line) for line in path.read_text().splitlines()[1:])}
expected=rows(root/'tests/Contracts/Fixtures/M8/Facets.golden.txt'); actual=rows(output)
missing=[key for key in expected if key not in actual]
changed=[key for key in expected if key in actual and expected[key]!=actual[key]]
assert not missing and not changed,(missing,changed)
print(f'PASS: {len(expected)} frozen public/internal facet contract members match actual assembly.')
