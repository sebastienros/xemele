using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8RngValidate;

public sealed class DatatypeTests
{
    private static RelaxNgSchema Schema(string pattern, string namespaces = "") => EvaluationTests.Schema(pattern, "datatypeLibrary='" + EvaluationTests.Xsd + "' " + namespaces);
    [Theory]
    [InlineData("int", "42", "2147483648")]
    [InlineData("decimal", "+12.500", "1e2")]
    [InlineData("boolean", "1", "yes")]
    [InlineData("date", "2024-02-29Z", "2023-02-29")]
    [InlineData("duration", "P1Y2M", "1Y")]
    [InlineData("base64Binary", "YQ==", "YQ=")]
    [InlineData("hexBinary", "aB01", "z0")]
    [InlineData("language", "en-US", "en_Us")]
    [InlineData("NMTOKENS", "a b:c", "")]
    [InlineData("positiveInteger", "1", "0")]
    public void AdvertisedSharedLibraryIsExecutable(string type, string valid, string invalid)
    {
        var schema = Schema("<data type='" + type + "'/>");
        Assert.True(schema.Validate("<r>" + valid + "</r>").IsValid);
        var rejected = schema.Validate("<r>" + invalid + "</r>");
        Assert.True(rejected.IsComplete);
        Assert.False(rejected.IsValid);
    }
    [Theory]
    [InlineData("int", "01", "+1", true)]
    [InlineData("decimal", "1.0", "1.00", true)]
    [InlineData("boolean", "true", "1", true)]
    [InlineData("hexBinary", "ab", "AB", true)]
    [InlineData("double", "NaN", "NaN", true)]
    [InlineData("int", "1", "2", false)]
    public void ValuesCompareValueSpaceInsteadOfLexicalStrings(string type, string authored, string actual, bool expected)
    {
        Assert.Equal(expected, Schema("<value type='" + type + "'>" + authored + "</value>").Validate("<r>" + actual + "</r>").IsValid);
    }
    [Fact]
    public void QNameValueUsesAuthoredRngNsAndInstancePrefixContext()
    {
        var schema = Schema("<value type='QName' ns='urn:value'>x</value>", "xmlns:q='urn:other'");
        Assert.True(schema.Validate("<r xmlns:p='urn:value'>p:x</r>").IsValid);
        Assert.False(schema.Validate("<r xmlns:p='urn:other'>p:x</r>").IsValid);
        Assert.False(schema.Validate("<r xmlns='urn:value'>x</r>").IsValid); // element name remains unqualified
        var data = Schema("<data type='QName'/>");
        Assert.True(data.Validate("<r xmlns:p='urn:value'>p:x</r>").IsValid);
        Assert.False(data.Validate("<r>p:x</r>").IsValid);
    }
    [Fact]
    public void ParametersHaveConjunctivePatternsAndOccurrenceIdentity()
    {
        var schema = Schema("<data type='string'><param name='pattern'>a.*</param><param name='pattern'>.*b</param><param name='minLength'> 2 </param></data>");
        Assert.True(schema.Validate("<r>ab</r>").IsValid);
        Assert.False(schema.Validate("<r>aa</r>").IsValid);
        Assert.False(schema.Validate("<r>bb</r>").IsValid);
        var distinct = Schema("<choice><data type='int'><param name='minInclusive'>10</param></data><data type='int'><param name='maxInclusive'>0</param></data></choice>");
        Assert.True(distinct.Validate("<r>10</r>").IsValid);
        Assert.True(distinct.Validate("<r>0</r>").IsValid);
        Assert.False(distinct.Validate("<r>5</r>").IsValid);
        var list = Schema("<data type='NMTOKENS'><param name='length'>2</param></data>");
        Assert.True(list.Validate("<r>a b</r>").IsValid);
        Assert.False(list.Validate("<r>a</r>").IsValid);
    }
    [Theory]
    [InlineData("<data type='int'><param name='maxInclusive'>bad</param></data>")]
    [InlineData("<data type='string'><param name='pattern'>[</param></data>")]
    [InlineData("<data type='int'><param name='length'>1</param></data>")]
    [InlineData("<data type='int'><param name='minInclusive'>10</param><param name='maxInclusive'>2</param></data>")]
    [InlineData("<data type='string'><param name='length'>1</param><param name='length'>2</param></data>")]
    [InlineData("<data type='string'><param name='whiteSpace'>collapse</param></data>")]
    [InlineData("<data type='string'><param name='enumeration'>x</param></data>")]
    [InlineData("<data type='string'><param name='Length'>1</param></data>")]
    [InlineData("<data type='unknown'/>")]
    [InlineData("<value type='int'>bad</value>")]
    public void AllDatatypeErrorsFailDuringPublicCompileIncludingUnreachableDefinitions(string invalid)
    {
        void Check(string source)
        {
            var error = Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(source));
            Assert.Contains(error.Diagnostics, d => d.Code == "RNG.Datatype" && d.Severity == XmlDiagnosticSeverity.Error);
        }
        Check(EvaluationTests.Element(invalid, "datatypeLibrary='" + EvaluationTests.Xsd + "'"));
        Check("<grammar xmlns='" + EvaluationTests.Ns + "' datatypeLibrary='" + EvaluationTests.Xsd + "'><start><element name='r'><empty/></element></start><define name='unused'>" + invalid + "</define></grammar>");
        Check(EvaluationTests.Element("<choice><notAllowed/>" + invalid + "</choice>", "datatypeLibrary='" + EvaluationTests.Xsd + "'"));
    }
    [Fact]
    public void EmptyLibraryRejectsParametersAndUnknownLibraries()
    {
        foreach (var pattern in new[] { "<data type='token'><param name='pattern'>a</param></data>", "<data datatypeLibrary='urn:unknown' type='string'/>" })
        {
            Assert.Contains(Assert.Throws<SchemaCompileException>(() => EvaluationTests.Schema(pattern)).Diagnostics, d => d.Code == "RNG.Datatype" || d.Code == "RNG.SchemaRestriction");
        }
    }
}
