using System.Text.Json;
using System.Xml.Linq;
using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.Results;
using BenchmarkDotNet.Validators;
using Xemele.Benchmarks;

internal static class EntityBaseProjectBinding
{
    internal static IToolchain Toolchain()
    {
        string project = Environment.GetEnvironmentVariable("XEMELE_ENTITY_BASE_PROJECT")
            ?? throw new InvalidOperationException("Explicit owned measurement project required.");
        return new BoundToolchain(Path.GetFullPath(project), Environment.GetEnvironmentVariable("XEMELE_ENTITY_BASE_PREPARATION") is not null);
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

    internal static void ValidatePreparedProof(JsonElement expected)
    {
        var actual = EntityBaseProvenance.Capture(expected.GetProperty("directory").GetString()!,
            expected.GetProperty("entry").GetProperty("path").GetString()!, expected.GetProperty("projectDirectory").GetString()!,
            expected.GetProperty("driver").GetProperty("path").GetString()!, expected.GetProperty("product").GetProperty("path").GetString()!);
        if (!EntityBaseProvenance.Equal(expected, actual)) throw new InvalidDataException("Frozen prepared child changed before Executor reuse.");
    }

    // BDN's existing generator/builder interfaces return the independently built
    // result; its pinned Executor and result processing run normally. No compiler,
    // restore or code generation is invoked in the measurement phase.
    private sealed class PreparedBuild(string project) : IGenerator, IBuilder
    {
        private static (JsonDocument Manifest, ArtifactsPaths Paths, string Hash) Read()
        {
            string file = Environment.GetEnvironmentVariable("XEMELE_ENTITY_BASE_PREPARATION")!;
            string hash = EntityBaseProvenance.Hash(file);
            if (hash != Environment.GetEnvironmentVariable("XEMELE_ENTITY_BASE_PREPARATION_SHA256"))
                throw new InvalidDataException("Approved prepared manifest changed.");
            var manifest = JsonDocument.Parse(File.ReadAllBytes(file));
            var generated = manifest.RootElement.GetProperty("generated");
            ValidatePreparedProof(generated.GetProperty("proof"));
            return (manifest, generated.GetProperty("artifactsPaths").Deserialize<ArtifactsPaths>()!, hash);
        }

        public GenerateResult GenerateProject(BuildPartition partition, ILogger logger, string rootArtifactsFolderPath)
        {
            var prepared = Read(); using var manifest = prepared.Manifest;
            if (partition.Benchmarks.Length != 2 || prepared.Paths.RootArtifactsFolderPath != rootArtifactsFolderPath)
                throw new InvalidDataException("Prepared partition/artifact binding differs.");
            Check(prepared.Paths.ProjectFilePath, project);
            return GenerateResult.Success(prepared.Paths, [prepared.Paths.BuildArtifactsDirectoryPath]);
        }

        public BuildResult Build(GenerateResult generated, BuildPartition partition, ILogger logger)
        {
            var prepared = Read(); using var manifest = prepared.Manifest;
            if (generated.ArtifactsPaths.ExecutablePath != prepared.Paths.ExecutablePath)
                throw new InvalidDataException("Prepared executable binding differs.");
            Console.WriteLine("ENTITY_BASE_PREBUILT " + JsonSerializer.Serialize(new
            {
                reused = true, generated = false, restored = false, built = false,
                entry = prepared.Paths.ExecutablePath, manifestSha256 = prepared.Hash
            }));
            return BuildResult.Success(generated);
        }
    }

    private sealed class BoundToolchain : IToolchain
    {
        private readonly IToolchain pinned = CsProjCoreToolchain.NetCoreApp10_0;
        public BoundToolchain(string project, bool reuse)
        {
            if (reuse) { var prepared = new PreparedBuild(project); Generator = prepared; Builder = prepared; }
            else { Generator = new BoundGenerator(project); Builder = pinned.Builder; }
        }
        public string Name => pinned.Name;
        public IGenerator Generator { get; }
        public IBuilder Builder { get; }
        public IExecutor Executor => pinned.Executor;
        public bool IsInProcess => pinned.IsInProcess;
        public IEnumerable<ValidationError> Validate(BenchmarkCase benchmarkCase, IResolver resolver)
            => pinned.Validate(benchmarkCase, resolver);
    }
}
