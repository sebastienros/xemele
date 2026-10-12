using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdMissingComponents;

internal static class Support
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static readonly Uri Basis = new("https://missing.test/main.xsd");
    internal static string Wrap(string body, string attributes = "") => $"<xs:schema xmlns:xs='{Xs}' {attributes}>{body}</xs:schema>";
    internal static void Missing(ValidationResult result)
    {
        Assert.False(result.IsComplete); Assert.False(result.IsValid);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("XSD.MissingComponent", diagnostic.Code); Assert.Equal(XmlDiagnosticSeverity.Fatal, diagnostic.Severity);
    }
    internal static (XsdCompiledSchema Schema, AvailabilityBuilder Builder, ISchemaOperation Operation) Compile(string body)
    {
        var context = new SchemaCompileContext(new() { BaseUri = Basis });
        var builder = new AvailabilityBuilder(context.Operation);
        var schema = new XmlSchemaCompiler().CompileAvailable(context.Parse(Wrap(body)).Document, context, builder);
        builder.Join(schema); return (schema, builder, context.Operation);
    }
    internal sealed class Resolver(Func<XmlResourceRequest, XmlResourceResolution> callback) : XmlResourceResolver
    {
        internal int Calls;
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); Calls++; return callback(request); }
    }
    internal sealed class OwnedStream(string text) : MemoryStream(Encoding.UTF8.GetBytes(text), false)
    {
        internal int Disposals;
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
    internal sealed class Counted : ISchemaOperation
    {
        internal long Work, Components, Polls;
        internal long? WorkCap, ComponentCap, CancelAt;
        internal readonly CancellationTokenSource Cancellation = new();
        public XmlResourceLimits Limits => new() { EvaluationWork = null };
        public CancellationToken CancellationToken => Cancellation.Token;
        public bool IsComplete { get; private set; } = true;
        public long? RemainingWork => WorkCap is { } cap ? cap - Work : null;
        public bool Charge(long count, XmlLocation at)
        {
            if (count == 0) Polls++;
            if (CancelAt is { } cancel && Work + count >= cancel) Cancellation.Cancel();
            CancellationToken.ThrowIfCancellationRequested();
            if (!IsComplete) return false;
            if (WorkCap is { } cap && count > cap - Work) { IsComplete = false; return false; }
            Work += count; return true;
        }
        public bool ChargeComponents(int count, XmlLocation at)
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (!IsComplete) return false;
            if (ComponentCap is { } cap && count > cap - Components) { IsComplete = false; return false; }
            Components += count; return true;
        }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => true;
        public void Refuse(string code, string message, XmlLocation at) => IsComplete = false;
    }
}
