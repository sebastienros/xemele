using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using InputSpike;

if (args.Length == 0 || args[0] is "--help" or "help")
{
    Console.WriteLine("Input architecture spike (not an XML parser). Commands: check, allocations, lowering, benchmark [BDN options].");
    return;
}
switch (args[0])
{
    case "check": Checks.Run(); break;
    case "allocations": InputSpike.Diagnostics.Allocations(); break;
    case "lowering": InputSpike.Diagnostics.Lowering(); break;
    case "benchmark":
        // BDN 0.15.8 gives a configured default precedence over --job.
        IConfig config = args[1..].Any(IsJobOption) ? DefaultConfig.Instance
            : DefaultConfig.Instance.AddJob(Job.ShortRun.AsDefault());
        BenchmarkSwitcher.FromAssembly(typeof(InputBenchmarks).Assembly).Run(args[1..], config);
        break;
    default: throw new ArgumentException("Unknown command.");
}

static bool IsJobOption(string argument) =>
    argument.Equals("--job", StringComparison.OrdinalIgnoreCase) ||
    argument.StartsWith("--job=", StringComparison.OrdinalIgnoreCase) ||
    argument.StartsWith("-j", StringComparison.OrdinalIgnoreCase);
