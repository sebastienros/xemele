using System.Runtime.CompilerServices;
using System.Text;
using Xemele.Internal.Operations;
[assembly: InternalsVisibleTo("Xemele.DtdFramingSample")]
[assembly: InternalsVisibleTo("Xemele.DtdFramingBenchmarks")]
namespace Xemele.Parsing.Dtd;

internal enum TextDeclarationStep
{
    NeedInput, Declared, Absent, Rejected
}
internal readonly record struct XmlTextDeclaration(string? Version, string EncodingName, XmlLocation Location);
internal readonly record struct TextDeclarationResult(int Consumed, TextDeclarationStep Step,
    XmlTextDeclaration? Declaration, XmlDiagnostic? Diagnostic, XmlLocation NextLocation);

// Only recognition (seven units), one unfinished scalar and bounded version/encoding values
// survive Append. Whitespace and fixed grammar tokens never enter growable storage.
internal sealed class XmlTextDeclarationFramer : IDisposable
{
    [InlineArray(7)]
    private struct PrefixChars
    {
        private char first;
    }
    [InlineArray(7)]
    private struct PrefixOffsets
    {
        private long first;
    }
    private enum State
    {
        Recognition, BeforeField, Field, BeforeEquals, BeforeQuote, Value, AfterVersion, AfterEncoding, Close
    }
    private readonly XmlResourceLimits limits;
    private readonly IDtdBudget? budget;
    private readonly ScalarLocation cursor;
    private PrefixChars prefix;
    private PrefixOffsets offsets;
    private int prefixLength, prefixScalars, mode, gate, fieldIndex, valueIndex, versionLength, encodingLength;
    private long lastOffset = -1, expanded, tokenScalars;
    private char high, quote;
    private XmlLocation highLocation, prefixLocation;
    private State state;
    private bool versionField, hasVersion, hasEncoding, separator, afterCr, disposed, faulted, terminal, drained;
    private char[] values = [];
    private TextDeclarationStep decision;
    private XmlTextDeclaration? declaration;
    private XmlDiagnostic? diagnostic;

    public XmlTextDeclarationFramer(XmlResourceLimits limits, Uri? sourceUri = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        if (sourceUri is { IsAbsoluteUri: false })
        {
            throw new ArgumentException("Source URI must be absolute.", nameof(sourceUri));
        }

        this.limits = limits with
        {
        };
        cursor = new(sourceUri);
        prefixLocation = cursor.Current();
    }
    internal XmlTextDeclarationFramer(XmlResourceLimits limits, IDtdBudget budget, Uri? sourceUri = null) : this(limits, sourceUri)
    {
        ArgumentNullException.ThrowIfNull(budget);
        this.budget = budget;
    }

    public TextDeclarationResult Append(ReadOnlySpan<char> input, ReadOnlySpan<long> byteOffsets,
        bool isFinalBlock, CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            CheckUsable();
            try
            {
                ValidateMappings(input, byteOffsets, cancellationToken);
            }
            catch (OperationCanceledException) { faulted = true; throw; }
            var consumed = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!input.IsEmpty && mode == 0)
                {
                    mode = byteOffsets.IsEmpty ? 1 : 2;
                }

                while (consumed < input.Length && !terminal)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var c = input[consumed];
                    long? offset = mode == 2 ? byteOffsets[consumed] : null;
                    var at = high != 0 ? highLocation : cursor.Current(offset);
                    int scalar;
                    var first = c;
                    if (high != 0)
                    {
                        if (!char.IsLowSurrogate(c))
                        {
                            Reject("XML.Syntax", "Unpaired high surrogate.", at);
                            break;
                        }
                        if (offset != highLocation.ByteOffset)
                        {
                            throw new ArgumentException("Surrogate mappings differ.", nameof(byteOffsets));
                        }

                        first = high;
                        scalar = char.ConvertToUtf32(high, c);
                    }
                    else if (char.IsHighSurrogate(c))
                    {
                        high = c;
                        highLocation = at;
                        consumed++;
                        if (offset is long h)
                        {
                            lastOffset = h;
                        }

                        continue;
                    }
                    else if (char.IsLowSurrogate(c))
                    {
                        Reject("XML.Syntax", "Unpaired low surrogate.", at);
                        break;
                    }
                    else
                    {
                        scalar = c;
                    }

                    if (!IsXmlScalar(scalar))
                    {
                        Reject("XML.Syntax", "Invalid XML scalar.", at);
                        break;
                    }
                    if (state != State.Recognition && !Charge(1, at))
                    {
                        break;
                    }

                    high = '\0';
                    consumed++;
                    if (offset is long b)
                    {
                        lastOffset = b;
                    }

                    var skipLf = scalar == '\n' && afterCr;
                    if (!cursor.TryAdvance(new Rune(scalar)))
                    {
                        Reject("XML.LocationLimit", "Source cursor overflow.", at);
                        break;
                    }
                    afterCr = scalar == '\r';
                    if (state == State.Recognition)
                    {
                        if (prefixLength == 0)
                        {
                            prefixLocation = at;
                        }

                        prefix[prefixLength] = first;
                        offsets[prefixLength++] = offset ?? 0;
                        if (scalar > 0xFFFF)
                        {
                            prefix[prefixLength] = c;
                            offsets[prefixLength++] = offset ?? 0;
                        }
                        prefixScalars++;
                        if (prefixLength <= 5 && scalar == "<?xml"[prefixLength - 1])
                        {
                            continue;
                        }

                        if (prefixLength == 6 && IsSpace(scalar))
                        {
                            // Charge the deferred recognition prefix exactly once.
                            if (!Charge(prefixScalars, prefixLocation))
                            {
                                break;
                            }

                            tokenScalars = prefixScalars;
                            if (!CheckToken(at))
                            {
                                break;
                            }

                            prefix = default;
                            offsets = default;
                            prefixLength = 0;
                            state = State.BeforeField;
                        }
                        else
                        {
                            Decide(TextDeclarationStep.Absent);
                        }
                    }
                    else if (!skipLf)
                    {
                        tokenScalars++;
                        if (!CheckToken(at))
                        {
                            break;
                        }

                        Process(scalar == '\r' ? '\n' : scalar, at);
                    }
                }
                if (!terminal && isFinalBlock)
                {
                    if (high != 0)
                    {
                        Reject("XML.Syntax", "Incomplete scalar at EOF.", highLocation);
                    }
                    else if (state == State.Recognition)
                    {
                        Decide(TextDeclarationStep.Absent);
                    }
                    else
                    {
                        Reject("XML.Syntax", "Incomplete text declaration at EOF.", cursor.Current());
                    }
                }
                // The next mapping is known only when the caller supplied a suffix.
                long? next = mode == 2 && consumed < input.Length ? byteOffsets[consumed] : null;
                return new(consumed, terminal ? decision : TextDeclarationStep.NeedInput, declaration, diagnostic, cursor.Current(next));
            }
            catch { faulted = true; throw; }
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    private void Process(int c, XmlLocation at)
    {
        switch (state)
        {
            case State.BeforeField:
                if (IsSpace(c))
                {
                    return;
                }

                versionField = !hasVersion && !hasEncoding && c == 'v';
                if (c != (versionField ? 'v' : 'e'))
                {
                    break;
                }

                fieldIndex = 1;
                state = State.Field;
                return;
            case State.Field:
                ReadOnlySpan<char> field = versionField ? "version" : "encoding";
                if (c != field[fieldIndex])
                {
                    break;
                }

                if (++fieldIndex == field.Length)
                {
                    state = State.BeforeEquals;
                }

                return;
            case State.BeforeEquals:
                if (IsSpace(c))
                {
                    return;
                }

                if (c != '=')
                {
                    break;
                }

                state = State.BeforeQuote;
                return;
            case State.BeforeQuote:
                if (IsSpace(c))
                {
                    return;
                }

                if (c is not ('\'' or '"'))
                {
                    break;
                }

                quote = (char)c;
                valueIndex = 0;
                state = State.Value;
                return;
            case State.Value:
                if (c == quote)
                {
                    if (valueIndex == 0 || (versionField && valueIndex < 3))
                    {
                        break;
                    }

                    if (versionField)
                    {
                        hasVersion = true;
                        state = State.AfterVersion;
                    }
                    else
                    {
                        hasEncoding = true;
                        state = State.AfterEncoding;
                    }
                    separator = false;
                    return;
                }
                if (versionField)
                {
                    if (valueIndex == 0 ? c != '1' : valueIndex == 1 ? c != '.' : c is < '0' or > '9')
                    {
                        break;
                    }
                }
                else
                {
                    if (!IsLetter(c) && (valueIndex == 0 || !(c is >= '0' and <= '9' or '.' or '_' or '-')))
                    {
                        break;
                    }
                }
                // Append has charged aggregate work and token quota before retention/growth.
                var length = versionLength + encodingLength;
                if (length == values.Length)
                {
                    if (length == Array.MaxLength)
                    {
                        Reject("XML.ResourceLimit", "Declaration value capacity exceeded.", at);
                        return;
                    }
                    var size = (int)Math.Min(Array.MaxLength, Math.Max(32L, (long)length * 2));
                    if (limits.TokenCharacters is int max)
                    {
                        size = Math.Min(size, max);
                    }

                    Array.Resize(ref values, size);
                }
                values[length] = (char)c;
                if (versionField)
                {
                    versionLength++;
                }
                else
                {
                    encodingLength++;
                }

                valueIndex++;
                return;
            case State.AfterVersion:
                if (IsSpace(c))
                {
                    separator = true;
                    return;
                }
                if (!separator || c != 'e')
                {
                    break;
                }

                versionField = false;
                fieldIndex = 1;
                state = State.Field;
                return;
            case State.AfterEncoding:
                if (IsSpace(c))
                {
                    return;
                }

                if (c != '?')
                {
                    break;
                }

                state = State.Close;
                return;
            case State.Close:
                if (c != '>')
                {
                    break;
                }

                declaration = new(hasVersion ? (values.AsSpan(0, versionLength).SequenceEqual("1.0") ? "1.0" : new string(values, 0, versionLength)) : null, new string(values, versionLength, encodingLength), prefixLocation);
                values = [];
                versionLength = encodingLength = 0;
                Decide(TextDeclarationStep.Declared);
                return;
        }
        Reject("XML.Syntax", "Invalid external text declaration.", at);
    }
    private bool Charge(long count, XmlLocation at)
    {
        if (budget is not null)
        {
            var refusal = budget.ChargeExpandedCharacters(count, at);
            if (refusal is null)
            {
                return true;
            }

            diagnostic = refusal;
            Decide(TextDeclarationStep.Rejected);
            return false;
        }
        if (count > long.MaxValue - expanded || (limits.ExpandedCharacters is long max && count > max - expanded))
        {
            Reject("XML.ResourceLimit", "Expanded character limit exceeded.", at);
            return false;
        }
        expanded += count;
        return true;
    }
    private bool CheckToken(XmlLocation at)
    {
        if (tokenScalars <= int.MaxValue && (limits.TokenCharacters is not int max || tokenScalars <= max))
        {
            return true;
        }

        Reject("XML.ResourceLimit", "Text declaration token limit exceeded.", at);
        return false;
    }
    private static bool IsSpace(int c) => c is ' ' or '\t' or '\r' or '\n';
    private static bool IsLetter(int c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    private static bool IsXmlScalar(int c) => c is 9 or 10 or 13 or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;
    private void Decide(TextDeclarationStep step)
    {
        terminal = true;
        decision = step;
    }
    private void Reject(string code, string message, XmlLocation at)
    {
        diagnostic = new(code, XmlDiagnosticSeverity.Fatal, message, at);
        Decide(TextDeclarationStep.Rejected);
    }
    private void ValidateMappings(ReadOnlySpan<char> input, ReadOnlySpan<long> mappings, CancellationToken cancellation)
    {
        if (!mappings.IsEmpty && mappings.Length != input.Length)
        {
            throw new ArgumentException("Mappings must parallel input.", nameof(mappings));
        }

        var newMode = mappings.IsEmpty ? 1 : 2;
        if (!input.IsEmpty && mode != 0 && mode != newMode)
        {
            throw new ArgumentException("Source mode cannot change.", nameof(mappings));
        }

        var previous = lastOffset;
        for (var i = 0; i < mappings.Length; i++)
        {
            if ((i & 4095) == 0)
            {
                cancellation.ThrowIfCancellationRequested();
            }

            if (mappings[i] < 0 || mappings[i] < previous)
            {
                throw new ArgumentException("Mappings must be nonnegative and monotonic.", nameof(mappings));
            }

            if (i > 0 && char.IsLowSurrogate(input[i]) && char.IsHighSurrogate(input[i - 1]) && mappings[i] != mappings[i - 1])
            {
                throw new ArgumentException("Surrogate mappings differ.", nameof(mappings));
            }

            previous = mappings[i];
        }
        if (high != 0 && !input.IsEmpty && !mappings.IsEmpty && char.IsLowSurrogate(input[0]) && mappings[0] != highLocation.ByteOffset)
        {
            throw new ArgumentException("Surrogate mappings differ.", nameof(mappings));
        }
    }
    public XmlLocation PrefixLocation
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!terminal || decision != TextDeclarationStep.Absent || faulted)
            {
                throw new InvalidOperationException("No absent prefix.");
            }

            return prefixLocation;
        }
    }
    public int DrainPrefix(Span<char> output, Span<long> byteOffsets)
    {
        Enter();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted || !terminal || decision != TextDeclarationStep.Absent || drained)
            {
                throw new InvalidOperationException("Prefix may be drained once after absence.");
            }

            if (output.Length < 7 || (mode == 2 ? byteOffsets.Length != output.Length : !byteOffsets.IsEmpty))
            {
                throw new ArgumentException("Prefix output requires seven units and matching source mappings.");
            }

            ((ReadOnlySpan<char>)prefix)[..prefixLength].CopyTo(output);
            if (mode == 2)
            {
                ((ReadOnlySpan<long>)offsets)[..prefixLength].CopyTo(byteOffsets);
            }

            var count = prefixLength;
            prefixLength = 0;
            prefix = default;
            offsets = default;
            drained = true;
            return count;
        }
        finally { Volatile.Write(ref gate, 0); }
    }
    private void Enter()
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0)
        {
            throw new InvalidOperationException("Concurrent or reentrant framer use.");
        }
    }
    private void CheckUsable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (terminal || faulted)
        {
            throw new InvalidOperationException("Framer is terminal.");
        }
    }
    public void Dispose()
    {
        Enter();
        try
        {
            disposed = true;
            prefix = default;
            offsets = default;
            prefixLength = 0;
            high = '\0';
            values = [];
            versionLength = encodingLength = 0;
            declaration = null;
            diagnostic = null;
        }
        finally { Volatile.Write(ref gate, 0); }
    }
}
