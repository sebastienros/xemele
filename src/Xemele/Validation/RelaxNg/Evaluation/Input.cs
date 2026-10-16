using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;

namespace Xemele.Validation;

// Structured parser output distinguishes owned syntax refusal from caller I/O exceptions.
internal static class RngInput
{
    private static OperationBudget? Budget(SchemaValidationOperation operation)
    {
        if (!operation.Charge(0, default))
        {
            return null;
        }

        var characters = operation.Limits.DocumentCharacters;
        if (operation.RemainingWork is long work)
        {
            if (work == 0)
            {
                operation.Refuse("VALIDATION.Limit", "Instance parsing allowance exhausted.", default);
                return null;
            }
            characters = characters is long cap ? Math.Min(cap, work) : work;
        }
        return new(new()
        {
            Limits = operation.Limits with
            {
                DocumentCharacters = characters
            },
            DtdProcessing = XmlDtdProcessing.Prohibit
        }, operation.CancellationToken);
    }
    internal static XmlDocument? Parse(string input, SchemaValidationOperation operation)
    {
        var budget = Budget(operation);
        if (budget is null)
        {
            return null;
        }

        long charged = 0;
        Exception? primary = null;
        try
        {
            using var core = new XmlParserCore(budget.Options, operationBudget: budget);
            var sink = new DomSink(budget.Options, cancellationToken: operation.CancellationToken);
            var result = core.Append(input.AsSpan(), [], true, sink, operation.CancellationToken);
            if (!Debit(budget, operation, ref charged))
            {
                return null;
            }

            return Finish(sink.Document, sink.Diagnostic ?? result.Diagnostic, operation);
        }
        catch (Exception error) { primary = error; throw; }
        finally { try { Debit(budget, operation, ref charged); } catch when (primary is not null) { } }
    }
    internal static XmlDocument? Parse(Stream input, SchemaValidationOperation operation)
    {
        var budget = Budget(operation);
        if (budget is null)
        {
            return null;
        }

        long charged = 0;
        Exception? primary = null;
        try
        {
            using var parser = new ByteDomAdapter(budget.Options, operation.CancellationToken, budget);
            // One bounded input buffer belongs to this operation. Caller streams stay open.
            var buffer = new byte[4096];
            while (parser.Diagnostic is null)
            {
                operation.CancellationToken.ThrowIfCancellationRequested();
                var count = input.Read(buffer.AsSpan());
                if ((uint)count > buffer.Length)
                {
                    throw new IOException("Invalid stream read count.");
                }

                if (count == 0)
                {
                    parser.Complete();
                }
                else
                {
                    parser.Append(buffer.AsSpan(0, count));
                }

                if (!Debit(budget, operation, ref charged))
                {
                    return null;
                }

                if (count == 0)
                {
                    break;
                }
            }
            return Finish(parser.Document, parser.Diagnostic, operation);
        }
        catch (Exception error) { primary = error; throw; }
        finally { try { Debit(budget, operation, ref charged); } catch when (primary is not null) { } }
    }
    private static bool Debit(OperationBudget budget, SchemaValidationOperation operation, ref long charged)
    {
        long actual = budget.Count(Quota.DocumentCharacters), used = actual - charged;
        charged = actual;
        return operation.Charge(used, default);
    }
    private static XmlDocument? Finish(XmlDocument document, XmlDiagnostic? diagnostic, SchemaValidationOperation operation)
    {
        operation.CancellationToken.ThrowIfCancellationRequested();
        if (diagnostic is null)
        {
            return document;
        }

        operation.Refuse(diagnostic.Code.Contains("Limit", StringComparison.OrdinalIgnoreCase) ? "VALIDATION.Limit" : "SCHEMA.Syntax", diagnostic.Message, diagnostic.Location);
        return null;
    }
}
