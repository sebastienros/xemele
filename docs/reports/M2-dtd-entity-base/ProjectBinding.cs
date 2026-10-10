using System.Text.Json;
using System.Xml.Linq;
using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Validators;
using Xemele.Benchmarks;

internal static class EntityBaseProjectBinding
{
    internal static IToolchain Toolchain()
    {
        string project = Environment.GetEnvironmentVariable("XEMELE_ENTITY_BASE_PROJECT")
            ?? throw new InvalidOperationException("Explicit owned measurement project required.");
        return new BoundToolchain(Path.GetFullPath(project));
    }

    internal static void Check(string generatedProject, string expected)
    {
        expected = Path.GetFullPath(expected);
        var references = XDocument.Load(generatedProject).Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => Path.GetFullPath(e.Attribute("Include")?.Value
                ?? throw new InvalidDataException("ProjectReference Include missing."), Path.GetDirectoryName(generatedProject)!)).ToArray();
        bool passed = references.Length == 1 && references[0] == expected;
        Console.WriteLine("ENTITY_BASE_PROJECT_BINDING " + JsonSerializer.Serialize(new
        {
            expected, actual = references, generatedProject = Path.GetFullPath(generatedProject),
            generatedProjectSha256 = EntityBaseProvenance.Hash(generatedProject), passed
        }));
        if (!passed) throw new InvalidDataException("Generated project must reference exactly the owned measurement project before build.");
    }

    private sealed class BoundGenerator(string project) : CsProjGenerator("net10.0", null!, null!, null!)
    {
        protected override FileInfo GetProjectFilePath(Type benchmarkTarget, ILogger logger)
        {
            var file = new FileInfo(project);
            if (!file.Exists || file.Name != "Xemele.EntityBase.Measurements.csproj" ||
                benchmarkTarget.Assembly.GetName().Name != "Xemele.Benchmarks")
                throw new InvalidDataException("Owned project/assembly binding differs.");
            return file;
        }

        protected override void GenerateProject(BuildPartition partition, ArtifactsPaths paths, ILogger logger)
        {
            base.GenerateProject(partition, paths, logger);
            Check(paths.ProjectFilePath, project);
        }
    }

    private sealed class BoundToolchain(string project) : IToolchain
    {
        private readonly IToolchain pinned = CsProjCoreToolchain.NetCoreApp10_0;
        public string Name => pinned.Name;
        public IGenerator Generator { get; } = new BoundGenerator(project);
        public IBuilder Builder => pinned.Builder;
        public IExecutor Executor => pinned.Executor;
        public bool IsInProcess => pinned.IsInProcess;
        public IEnumerable<ValidationError> Validate(BenchmarkCase benchmarkCase, IResolver resolver)
            => pinned.Validate(benchmarkCase, resolver);
    }
}
