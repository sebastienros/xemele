using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    Console.WriteLine("Xemele benchmarks");
    Console.WriteLine("Usage: dotnet run -c Release --project benchmarks/Xemele.Benchmarks -- [BenchmarkDotNet options]");
    Console.WriteLine("Benchmark cases are added by feature tasks.");
    return 0;
}

// BDN 0.15.8 gives a configured default precedence over --job.
IConfig config = args.Any(IsJobOption) ? DefaultConfig.Instance
    : DefaultConfig.Instance.AddJob(Job.ShortRun.AsDefault());
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
return 0;

static bool IsJobOption(string argument) =>
    argument.Equals("--job", StringComparison.OrdinalIgnoreCase) ||
    argument.StartsWith("--job=", StringComparison.OrdinalIgnoreCase) ||
    argument.StartsWith("-j", StringComparison.OrdinalIgnoreCase);
