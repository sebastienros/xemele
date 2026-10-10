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
        BenchmarkSwitcher.FromAssembly(typeof(InputBenchmarks).Assembly).Run(args[1..]);
        break;
    default: throw new ArgumentException("Unknown command.");
}
