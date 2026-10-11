using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;

namespace Xemele.Validation;

// Share aggregate scalar accounting across principal input and external documents.
// Byte/character decoding remains the verified M2 path; count its actual accepted
// scalar charges, rather than UTF-16 units, bytes, or the entire allowance.
internal sealed class SchemaParsing(ISchemaOperation operation)
{
    private long characters, expandedCharacters;
    private OperationBudget Budget()
    {
        if (!operation.Charge(0, default)) throw SchemaSnapshots.Failure(operation);
        long remaining = (operation.Limits.DocumentCharacters ?? long.MaxValue) - characters;
        long allowance = operation.RemainingWork is long work ? Math.Min(work, remaining) : remaining;
        if (allowance <= 0)
        { operation.Refuse("SCHEMA.Limit", "Schema parsing allowance exhausted.", default); throw SchemaSnapshots.Failure(operation); }
        var budget = new OperationBudget(new() { Limits = operation.Limits with { DocumentCharacters = allowance }, DtdProcessing = XmlDtdProcessing.Prohibit }, operation.CancellationToken);
        // Seeding the original positive limit permits no-expansion input at the
        // aggregate cap, while rejecting the next expansion before its growth.
        if (!budget.TryCharge(Quota.ExpandedCharacters, expandedCharacters)) throw new InvalidOperationException("Invalid expansion ledger.");
        return budget;
    }
    internal XmlDocument Parse(string input, Uri? sourceUri)
    {
        ArgumentNullException.ThrowIfNull(input);
        var budget = Budget(); Exception? primary = null; long debited = 0;
        try
        {
            using var core = new XmlParserCore(budget.Options, sourceUri, operationBudget: budget);
            var sink = new DomSink(budget.Options, cancellationToken: operation.CancellationToken);
            var result = core.Append(input.AsSpan(), [], true, sink, operation.CancellationToken);
            Debit(budget, ref debited, null);
            if ((sink.Diagnostic ?? result.Diagnostic) is { } diagnostic) Reject(diagnostic);
            operation.CancellationToken.ThrowIfCancellationRequested(); return sink.Document;
        }
        catch (Exception error) { primary = error; throw; }
        finally { Debit(budget, ref debited, primary); }
    }
    internal XmlDocument Parse(Stream input, Uri? sourceUri, SchemaResourceLoader? external = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead) throw new ArgumentException("Readable stream required.", nameof(input));
        var budget = Budget(); Exception? primary = null; long debited = 0;
        try
        {
            using var parser = new ByteDomAdapter(budget.Options, operation.CancellationToken, budget, sourceUri);
            byte[] buffer = new byte[4096];
            while (parser.Diagnostic is null)
            {
                operation.CancellationToken.ThrowIfCancellationRequested();
                int count = external is null ? input.Read(buffer.AsSpan()) : external.Read(input, buffer.AsSpan(), sourceUri);
                if ((uint)count > buffer.Length) throw new IOException("Invalid stream read count.");
                if (count == 0) { parser.Complete(); Debit(budget, ref debited, null); break; }
                parser.Append(buffer.AsSpan(0, count));
                Debit(budget, ref debited, null);
            }
            if (parser.Diagnostic is { } diagnostic) Reject(diagnostic);
            operation.CancellationToken.ThrowIfCancellationRequested(); return parser.Document;
        }
        catch (Exception error) { primary = error; throw; }
        finally { Debit(budget, ref debited, primary); }
    }
    private void Reject(XmlDiagnostic diagnostic)
    {
        if (diagnostic.Code.Contains("Limit", StringComparison.OrdinalIgnoreCase)) operation.Refuse("SCHEMA.Limit", diagnostic.Message, diagnostic.Location);
        else operation.AddDiagnostic(diagnostic with { Code = "SCHEMA.Syntax" });
        throw SchemaSnapshots.Failure(operation);
    }
    private void Debit(OperationBudget budget, ref long debited, Exception? primary)
    {
        expandedCharacters = budget.Count(Quota.ExpandedCharacters);
        long total = budget.Count(Quota.DocumentCharacters);
        long used = total - debited; debited = total;
        characters = checked(characters + used);
        try { if (!operation.Charge(used, default) && primary is null) throw SchemaSnapshots.Failure(operation); }
        catch when (primary is not null) { }
    }
}
