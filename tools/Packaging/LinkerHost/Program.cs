using System.Diagnostics;

string? host = Environment.GetEnvironmentVariable("XEMELE_PINNED_DOTNET");
string? linker = Environment.GetEnvironmentVariable("XEMELE_PINNED_LINKER_DLL");
if (host is null || linker is null || !File.Exists(host) || !File.Exists(linker) ||
    args.Length == 0 || !string.Equals(Path.GetFullPath(args[0]), Path.GetFullPath(linker),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
{
    Console.Error.WriteLine("Pinned linker host requires the exact installed host and private ILLink DLL.");
    return 2;
}
var invocation = new ProcessStartInfo(host) { UseShellExecute = false };
invocation.ArgumentList.Add("--fx-version");
invocation.ArgumentList.Add("10.0.11");
foreach (string argument in args) invocation.ArgumentList.Add(argument);
using var child = Process.Start(invocation) ?? throw new InvalidOperationException("Could not start pinned linker.");
child.WaitForExit();
return child.ExitCode;
