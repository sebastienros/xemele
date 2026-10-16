using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Xemele.Validation;

namespace Xemele.Benchmarks.M5DtdValidation;

[MemoryDiagnoser, ShortRunJob]
public class DtdValidationBenchmarks
{
    [Params("ascii", "unicode", "prefixed", "ids", "invalid", "quota")]
    public string Scenario { get; set; } = "ascii";
    [Params(8, 128)]
    public int Count
    {
        get; set;
    }
    private DtdSchema schema = null!;
    private XmlDocument document = null!;
    private ValidationOptions options = null!;
    [GlobalSetup]
    public void Setup()
    {
        var child = Scenario == "unicode" ? "名" : Scenario == "prefixed" ? "p:a" : "a";
        var ns = Scenario == "prefixed" ? " xmlns:p='urn:p'" : "";
        var declarations = Scenario == "prefixed" ? "<!ATTLIST r xmlns:p CDATA #IMPLIED>" : "";
        var attributes = Scenario == "ids" ? "<!ATTLIST a id ID #REQUIRED refs IDREFS #IMPLIED>" : "<!ATTLIST " + child + " kind (one|two) #IMPLIED>";
        schema = DtdSchema.Compile("<!ELEMENT r (" + child + "*)><!ELEMENT " + child + " EMPTY>" + declarations + attributes);
        var xml = new StringBuilder("<r" + ns + ">");
        for (var i = 0; i < Count; i++)
        {
            xml.Append('<').Append(child).Append(Scenario == "ids" ? $" id='id{i}' refs='id{(i + 1) % Count}'" : Scenario == "invalid" ? " kind='bad'" : " kind='one'").Append("/>");
        }

        xml.Append("</r>");
        document = XmlDocument.Parse(xml.ToString());
        options = Scenario == "quota" ? new()
        {
            MaxOperations = 10
        } : new();
        var control = schema.Validate(document, options);
        if (control.IsValid != (Scenario is not ("invalid" or "quota")) || control.IsComplete != (Scenario != "quota"))
        {
            throw new InvalidOperationException("Benchmark oracle failed.");
        }
    }
    [Benchmark] public ValidationResult ValidateCurrentDom() => schema.Validate(document, options);
}

[MemoryDiagnoser, ShortRunJob]
public class DtdSchemaCompilationBenchmarks
{
    [Params("ascii", "unicode")] public string Scenario { get; set; } = "ascii";
    private string source = "";
    [GlobalSetup]
    public void Setup()
    {
        var child = Scenario == "ascii" ? "a" : "名";
        source = "<!ELEMENT r (" + child + "*)><!ELEMENT " + child + " EMPTY><!ATTLIST " + child + " id ID #IMPLIED>";
        _ = DtdSchema.Compile(source);
    }
    [Benchmark] public DtdSchema CompileOwnedSchema() => DtdSchema.Compile(source);
}
