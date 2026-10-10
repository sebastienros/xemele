using BenchmarkDotNet.Running;

if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    Console.WriteLine("Xemele benchmarks");
    Console.WriteLine("Usage: dotnet run -c Release --project benchmarks/Xemele.Benchmarks -- [BenchmarkDotNet options]");
    Console.WriteLine("Benchmark cases are added by feature tasks.");
    return 0;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;
