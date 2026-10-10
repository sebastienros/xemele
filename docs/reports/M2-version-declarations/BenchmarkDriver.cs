using BenchmarkDotNet.Running;
if (args.Length == 1 && args[0] == "--oracle")
{
    foreach (string corpus in new[] { "1.0", "1.10", "long", "invalid" })
    { var document = new DeclarationVersionBenchmarks { Corpus = corpus }; document.Setup(); document.ParseDocument(); }
    foreach (string corpus in new[] { "1.0", "1.10", "long", "absent-version", "invalid" })
    foreach (bool chunked in new[] { false, true })
    { var text = new TextDeclarationVersionBenchmarks { Corpus = corpus, Chunked = chunked }; text.Setup(); text.FrameOwned(); }
    Console.WriteLine("Passed all 14 actual benchmark-method full document/framing fixture oracles.");
}
else BenchmarkSwitcher.FromAssembly(typeof(DeclarationVersionBenchmarks).Assembly).Run(args);
