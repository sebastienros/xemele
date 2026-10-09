"""Reproduce the pinned compiler's escape checks without editing shared files."""
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
with tempfile.TemporaryDirectory(prefix="escape-", dir=root / "experiments/Input") as folder:
    path = Path(folder)
    (path / "Escape.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"/>\n')
    (path / "Escape.cs").write_text('''using System;
internal static class Escape
{
    private static readonly byte Value = 0xEF;
    internal static ReadOnlySpan<byte> Runtime => [Value, 0xBB, 0xBF];
    internal static Span<byte> Mutable => [0xEF, 0xBB, 0xBF];
}
''')
    # Isolate generated diagnostic source from the experiment's default source glob.
    result = subprocess.run(["dotnet", "build", str(path / "Escape.csproj"), "-c", "Release"], cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    print(result.stdout)
    if result.returncode == 0 or result.stdout.count("error CS9203") < 2:
        raise SystemExit("FAIL: expected both returning collection expressions to reject with CS9203")
    print("PASS: both returned span collection expressions rejected with CS9203; temporary files removed.")
