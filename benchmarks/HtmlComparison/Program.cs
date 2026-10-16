using BenchmarkDotNet.Running;
using HtmlComparison;

if (args.Contains("--qualify"))
    Qualification.Run();
else
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
