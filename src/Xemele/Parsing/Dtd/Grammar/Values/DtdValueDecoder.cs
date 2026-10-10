namespace Xemele.Parsing.Dtd;

internal enum DtdValueStep { NeedInput, NeedOutput, Completed, Rejected }
internal readonly record struct DtdValueResult(int Consumed, int Written, DtdValueStep Step, XmlDiagnostic? Diagnostic);

// One entity-declaration literal, after PE composition and source newline normalization.
// Delimiters are excluded. No source view or numeric spelling is retained.
internal sealed class DtdValueDecoder : IDisposable
{
    private enum State { Text, Ampersand, GeneralFirst, General, NumberStart, HexStart, Digits }
    private readonly ValueBudget budget;
    private readonly XmlLocation location;
    private State state;
    private int scalar = -1, high, number, radix;
    private bool digits, final, terminal, disposed, busy;
    private long outputScalars;

    public DtdValueDecoder(XmlResourceLimits limits, XmlLocation location = default, IDtdBudget? budget = null)
    {
        this.budget = new(limits, budget); this.location = location;
    }

    public DtdValueResult Decode(ReadOnlySpan<char> input, Span<char> output, bool isFinalBlock, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (busy || terminal) throw new InvalidOperationException("Decoder is active or terminal.");
        if (output.Length < 2) throw new ArgumentException("Output must hold a complete scalar.", nameof(output));
        if (final && !isFinalBlock) throw new InvalidOperationException("Final input is sticky.");
        busy = true; final |= isFinalBlock;
        int consumed = 0, written = 0;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scalar < 0)
                {
                    if (consumed == input.Length)
                    {
                        if (!final) return new(consumed, written, DtdValueStep.NeedInput, null);
                        if (high != 0 || state != State.Text) return Reject("Incomplete value or reference.");
                        terminal = true; return new(consumed, written, DtdValueStep.Completed, null);
                    }
                    int c = input[consumed++];
                    if (high != 0)
                    {
                        if (!char.IsLowSurrogate((char)c)) return Reject("Unpaired surrogate.");
                        scalar = char.ConvertToUtf32((char)high, (char)c); high = 0;
                    }
                    else if (char.IsHighSurrogate((char)c)) { high = c; continue; }
                    else scalar = c;
                    if (!ValueSyntax.IsXmlScalar(scalar)) return Reject("Illegal XML scalar.");
                }
                int emit = -1;
                bool retainScalar = false, expansion = false;
                State next = state;
                switch (state)
                {
                    case State.Text:
                        if (scalar == '&') next = State.Ampersand;
                        else emit = scalar;
                        break;
                    case State.Ampersand:
                        if (scalar == '#') next = State.NumberStart;
                        else
                        {
                            if (!ValueSyntax.IsNameStart(scalar)) return Reject("Invalid general reference name.");
                            emit = '&'; retainScalar = true; next = State.GeneralFirst;
                        }
                        break;
                    case State.GeneralFirst:
                        emit = scalar; next = State.General; break;
                    case State.General:
                        if (scalar == ';') { emit = scalar; next = State.Text; }
                        else if (ValueSyntax.IsNameChar(scalar)) emit = scalar;
                        else return Reject("Invalid general reference.");
                        break;
                    case State.NumberStart:
                        number = 0; digits = false;
                        if (scalar == 'x') { radix = 16; next = State.HexStart; break; }
                        radix = 10; next = State.Digits;
                        goto case State.Digits;
                    case State.HexStart:
                        next = State.Digits; goto case State.Digits;
                    case State.Digits:
                        if (scalar == ';')
                        {
                            if (!digits || !ValueSyntax.IsXmlScalar(number)) return Reject("Illegal character reference.");
                            emit = number; expansion = true; next = State.Text;
                        }
                        else
                        {
                            int digit = ValueSyntax.Digit(scalar, radix);
                            if (digit < 0 || number > (0x10FFFF - digit) / radix) return Reject("Invalid or overflowing character reference.");
                            number = number * radix + digit; digits = true;
                        }
                        break;
                }
                if (emit >= 0)
                {
                    int units = emit > 0xFFFF ? 2 : 1;
                    if (output.Length - written < units) return new(consumed, written, DtdValueStep.NeedOutput, null);
                    XmlDiagnostic? refusal = budget.CheckToken(outputScalars, location);
                    if (refusal is not null) return RejectDiagnostic(refusal);
                    if (expansion)
                    {
                        refusal = budget.Enter(location);
                        if (refusal is not null) return RejectDiagnostic(refusal);
                        try { refusal = budget.Charge(1, location); }
                        finally { budget.Exit(); }
                        if (refusal is not null) return RejectDiagnostic(refusal);
                    }
                    outputScalars++;
                    written += ValueSyntax.WriteScalar(emit, output[written..]);
                }
                state = next;
                if (!retainScalar) scalar = -1;
            }
        }
        catch { terminal = true; throw; }
        finally { busy = false; }

        DtdValueResult Reject(string message) => RejectDiagnostic(ValueSyntax.Error(message, location));
        DtdValueResult RejectDiagnostic(XmlDiagnostic diagnostic)
        { terminal = true; return new(consumed, written, DtdValueStep.Rejected, diagnostic); }
    }

    public void Dispose()
    {
        if (busy) throw new InvalidOperationException("Decoder is active.");
        disposed = true; scalar = -1; high = 0;
    }
}

internal static class ValueSyntax
{
    internal static bool IsXmlScalar(int c) => c is 9 or 10 or 13 or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;
    internal static bool IsNameStart(int c) => c is ':' or '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= 0xC0 and <= 0xD6 or >= 0xD8 and <= 0xF6 or >= 0xF8 and <= 0x2FF or >= 0x370 and <= 0x37D or >= 0x37F and <= 0x1FFF or >= 0x200C and <= 0x200D or >= 0x2070 and <= 0x218F or >= 0x2C00 and <= 0x2FEF or >= 0x3001 and <= 0xD7FF or >= 0xF900 and <= 0xFDCF or >= 0xFDF0 and <= 0xFFFD or >= 0x10000 and <= 0xEFFFF;
    internal static bool IsNameChar(int c) => IsNameStart(c) || c is '-' or '.' or >= '0' and <= '9' or 0xB7 or >= 0x300 and <= 0x36F or >= 0x203F and <= 0x2040;
    internal static int Digit(int c, int radix) => c is >= '0' and <= '9' ? c - '0' : radix == 16 && c is >= 'a' and <= 'f' ? c - 'a' + 10 : radix == 16 && c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
    internal static int WriteScalar(int c, Span<char> output)
    {
        if (c <= 0xFFFF) { output[0] = (char)c; return 1; }
        c -= 0x10000; output[0] = (char)(0xD800 + (c >> 10)); output[1] = (char)(0xDC00 + (c & 1023)); return 2;
    }
    internal static XmlDiagnostic Error(string message, XmlLocation location) => new("DTD.InvalidValue", XmlDiagnosticSeverity.Fatal, message, location);
}

// The supplied operation ledger alone owns aggregate expansion/depth when present.
// Token/output capacity stays local; source/name charges are always the caller's.
internal sealed class ValueBudget
{
    private readonly XmlResourceLimits limits;
    private readonly IDtdBudget? shared;
    private long expanded;
    private int depth;
    internal ValueBudget(XmlResourceLimits limits, IDtdBudget? shared)
    { ArgumentNullException.ThrowIfNull(limits); limits.Validate(); this.limits = limits with { }; this.shared = shared; }
    internal XmlDiagnostic? CheckToken(long existing, XmlLocation at) => existing >= int.MaxValue || limits.TokenCharacters is int max && existing >= max ? Limit(at) : null;
    internal XmlDiagnostic? Charge(long count, XmlLocation at)
    {
        if (shared is not null) return shared.ChargeExpandedCharacters(count, at);
        if (count > long.MaxValue - expanded || limits.ExpandedCharacters is long max && count > max - expanded) return Limit(at);
        expanded += count; return null;
    }
    internal XmlDiagnostic? Enter(XmlLocation at)
    {
        if (shared is not null) return shared.EnterEntity(at);
        if (depth == int.MaxValue || limits.EntityDepth is int max && depth >= max) return Limit(at);
        depth++; return null;
    }
    internal void Exit() { if (shared is not null) shared.ExitEntity(); else depth--; }
    internal static XmlDiagnostic Limit(XmlLocation at) => new("XML.ResourceLimit", XmlDiagnosticSeverity.Fatal, "DTD value resource limit exceeded.", at);
}
