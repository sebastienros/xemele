using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Xemele.Internal.Operations;
[assembly: InternalsVisibleTo("Xemele.InputBenchmarks")]
[assembly: InternalsVisibleTo("Xemele.InputSample")]
namespace Xemele.Parsing.Input;

internal enum XmlInputEncoding { Undetermined, Utf8, Utf16LittleEndian, Utf16BigEndian, Ascii, Latin1, Windows1252 }
internal enum DecodeStep { NeedInput, NeedOutput, Completed, InvalidInput }
internal readonly record struct DecodeResult(int ConsumedBytes, int WrittenCharacters, DecodeStep Step, XmlDiagnostic? Diagnostic);
internal interface IXmlInputDecoder : IDisposable
{
    XmlInputEncoding Encoding { get; }
    DecodeResult Decode(ReadOnlySpan<byte> input, Span<char> output, Span<long> byteOffsets, bool isFinalBlock, CancellationToken cancellationToken);
    XmlDiagnostic? ValidateDeclaration(ReadOnlySpan<char> encodingName, XmlLocation location);
}

// Only four owned bytes survive calls: detection prefix or unfinished scalar.
// No caller input/output view is retained. Raw units and location scalars differ.
internal sealed class XmlInputDecoder : IXmlInputDecoder
{
    [InlineArray(4)] private struct Carry { private byte first; }
    private Carry carry;
    private int count;
    private long carryOffset;
    private long acceptedBytes;
    private readonly ScalarLocation location;
    // Snapshot/validation only: primary bytes are not external-resource quota units.
    private readonly OperationBudget budget;
    private bool bom, final, completed, rejected, disposed, declarationValidated, faulted;
    private bool signatureSelected, provisionalNonAscii;
    private int active;
    private XmlInputEncoding encoding;
    public XmlInputEncoding Encoding { get { ObjectDisposedException.ThrowIf(disposed, this); return encoding; } }

    public XmlInputDecoder(XmlParseOptions options, Uri? sourceUri = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (sourceUri is { IsAbsoluteUri: false }) throw new ArgumentException("Source URI must be absolute.", nameof(sourceUri));
        budget = new(options, default);
        location = new(sourceUri);
    }

    public DecodeResult Decode(ReadOnlySpan<byte> input, Span<char> output, Span<long> byteOffsets, bool isFinalBlock, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (output.Length < 2 || byteOffsets.Length != output.Length)
            throw new ArgumentException("Output requires two UTF-16 units and an equally sized mapping span.");
        if (completed || rejected || faulted) throw new InvalidOperationException("Decoder is terminal.");
        if (final && !isFinalBlock) throw new InvalidOperationException("Final flag cannot be withdrawn.");
        if (Interlocked.Exchange(ref active, 1) != 0) throw new InvalidOperationException("Concurrent decoder use.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            final |= isFinalBlock;
            int consumed = 0, written = 0;
            if (encoding == XmlInputEncoding.Undetermined)
            {
                Span<byte> detection = stackalloc byte[4];
                int available = count + Math.Min(4 - count, input.Length);
                ((ReadOnlySpan<byte>)carry)[..count].CopyTo(detection);
                input[..(available - count)].CopyTo(detection[count..]);
                if (available < 4 && !final)
                {
                    int extra = available - count;
                    if (!Accept(extra)) return Error(0, 0, "XML.ResourceLimit", acceptedBytes);
                    detection[..available].CopyTo(carry); count = available;
                    return new(extra, 0, DecodeStep.NeedInput, null);
                }
                ReadOnlySpan<byte> prefix = detection[..available];
                if (prefix.StartsWith((ReadOnlySpan<byte>)[0, 0, 0xFE, 0xFF]) || prefix.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE, 0, 0]) ||
                    prefix.SequenceEqual("\0\0\0<"u8) || prefix.SequenceEqual("<\0\0\0"u8) ||
                    prefix.SequenceEqual((ReadOnlySpan<byte>)[0, 0, 0x3C, 0]) || prefix.SequenceEqual((ReadOnlySpan<byte>)[0, 0x3C, 0, 0]) ||
                    prefix.SequenceEqual((ReadOnlySpan<byte>)[0x4C, 0x6F, 0xA7, 0x94]))
                    return Error(consumed, 0, "INPUT.UnsupportedEncoding", 0);
                int skip = 0;
                if (prefix.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])) { encoding = XmlInputEncoding.Utf8; skip = 3; }
                else if (prefix.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])) { encoding = XmlInputEncoding.Utf16LittleEndian; skip = 2; }
                else if (prefix.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF])) { encoding = XmlInputEncoding.Utf16BigEndian; skip = 2; }
                else if (available >= 4 && detection[0] == '<' && detection[1] == 0 && detection[3] == 0) encoding = XmlInputEncoding.Utf16LittleEndian;
                else if (available >= 4 && detection[0] == 0 && detection[1] == '<' && detection[2] == 0) encoding = XmlInputEncoding.Utf16BigEndian;
                else encoding = XmlInputEncoding.Utf8;
                bom = skip != 0;
                signatureSelected = bom || encoding != XmlInputEncoding.Utf8;
                int ownedSkip = Math.Min(skip, count);
                int submittedSkip = skip - ownedSkip;
                if (!Accept(submittedSkip)) return Error(0, 0, "XML.ResourceLimit", acceptedBytes);
                consumed = submittedSkip;
                Drop(ownedSkip); carryOffset = skip;
            }
            Span<byte> candidate = stackalloc byte[4];
            while (count != 0 || consumed < input.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int available = count + Math.Min(4 - count, input.Length - consumed);
                ((ReadOnlySpan<byte>)carry)[..count].CopyTo(candidate);
                input.Slice(consumed, available - count).CopyTo(candidate[count..]);
                long offset = count != 0 ? carryOffset : acceptedBytes;
                Rune scalar;
                int bytes;
                OperationStatus status;
                if (encoding == XmlInputEncoding.Utf8)
                    status = Rune.DecodeFromUtf8(candidate[..available], out scalar, out bytes);
                else if (encoding is XmlInputEncoding.Ascii or XmlInputEncoding.Latin1 or XmlInputEncoding.Windows1252)
                    status = DecodeSingleByte(candidate[0], encoding, out scalar, out bytes);
                else
                    status = DecodeUtf16(candidate[..available], encoding == XmlInputEncoding.Utf16LittleEndian, out scalar, out bytes);
                if (status == OperationStatus.InvalidData || (status == OperationStatus.NeedMoreData && final))
                    return Error(consumed, written, "INPUT.InvalidEncoding", offset);
                if (status == OperationStatus.NeedMoreData)
                {
                    int extra = available - count;
                    if (!Accept(extra)) return Error(consumed, written, "XML.ResourceLimit", offset);
                    candidate[..available].CopyTo(carry); count = available; carryOffset = offset;
                    consumed += extra;
                    return new(consumed, written, DecodeStep.NeedInput, null);
                }
                if (output.Length - written < scalar.Utf16SequenceLength)
                    return new(consumed, written, DecodeStep.NeedOutput, null);
                int fromInput = Math.Max(0, bytes - count);
                if (!Accept(fromInput)) return Error(consumed, written, "XML.ResourceLimit", offset);
                // Capture location before advancing; errors never partly emit a scalar.
                XmlLocation at = location.Current(offset);
                if (!location.TryAdvance(scalar)) return Reject(consumed, written, "XML.LocationLimit", at);
                if (!declarationValidated && !signatureSelected && !scalar.IsAscii) provisionalNonAscii = true;
                int units = scalar.EncodeToUtf16(output[written..]);
                byteOffsets[written] = offset;
                if (units == 2) byteOffsets[written + 1] = offset;
                written += units; consumed += fromInput;
                Drop(Math.Min(bytes, count));
                carryOffset += bytes;
            }
            if (final) { completed = true; return new(consumed, written, DecodeStep.Completed, null); }
            return new(consumed, written, DecodeStep.NeedInput, null);
        }
        catch { faulted = true; throw; }
        finally { Volatile.Write(ref active, 0); }
    }
    private bool Accept(int bytes)
    {
        if (bytes > long.MaxValue - acceptedBytes) return false;
        acceptedBytes += bytes;
        return true;
    }
    private void Drop(int bytes)
    {
        for (int i = bytes; i < count; i++) carry[i - bytes] = carry[i];
        count -= bytes;
    }
    private static OperationStatus DecodeUtf16(ReadOnlySpan<byte> bytes, bool little, out Rune scalar, out int used)
    {
        scalar = default; used = 0;
        if (bytes.Length < 2) return OperationStatus.NeedMoreData;
        int first = Unit(bytes, little);
        if (first is >= 0xDC00 and <= 0xDFFF) return OperationStatus.InvalidData;
        if (first is >= 0xD800 and <= 0xDBFF)
        {
            if (bytes.Length < 4) return OperationStatus.NeedMoreData;
            int second = Unit(bytes[2..], little);
            if (second is < 0xDC00 or > 0xDFFF) return OperationStatus.InvalidData;
            scalar = new Rune((char)first, (char)second); used = 4;
        }
        else { scalar = new Rune(first); used = 2; }
        return OperationStatus.Done;
    }
    // ISO-8859-1 is an exact byte-to-scalar map; XML legality belongs to grammar.
    // Undefined Windows-1252 slots are zero sentinels, never replacement output.
    private static ReadOnlySpan<ushort> WindowsControls => [
        0x20AC, 0, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021,
        0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0, 0x017D, 0,
        0, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014,
        0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0, 0x017E, 0x0178];
    private static OperationStatus DecodeSingleByte(byte value, XmlInputEncoding encoding, out Rune scalar, out int used)
    {
        scalar = default; used = 0;
        if (encoding == XmlInputEncoding.Ascii && value > 0x7F) return OperationStatus.InvalidData;
        int mapped = value;
        if (encoding == XmlInputEncoding.Windows1252 && value is >= 0x80 and <= 0x9F)
        {
            mapped = WindowsControls[value - 0x80];
            if (mapped == 0) return OperationStatus.InvalidData;
        }
        scalar = new Rune(mapped); used = 1;
        return OperationStatus.Done;
    }
    private static int Unit(ReadOnlySpan<byte> bytes, bool little) => little ? bytes[0] | bytes[1] << 8 : bytes[0] << 8 | bytes[1];
    private DecodeResult Error(int consumed, int written, string code, long offset) => Reject(consumed, written, code, location.Current(offset));
    private DecodeResult Reject(int consumed, int written, string code, XmlLocation at)
    {
        rejected = true;
        return new(consumed, written, DecodeStep.InvalidInput, new(code, XmlDiagnosticSeverity.Fatal, "Invalid or unsupported byte encoding, declaration, or input capacity.", at));
    }
    public XmlDiagnostic? ValidateDeclaration(ReadOnlySpan<char> encodingName, XmlLocation location)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (rejected || faulted || declarationValidated || encoding == XmlInputEncoding.Undetermined || Volatile.Read(ref active) != 0)
            throw new InvalidOperationException("Declaration must be validated exactly once after detection.");
        declarationValidated = true;
        bool empty = encodingName.IsEmpty;
        bool utf8 = EqualsAscii(encodingName, "UTF-8");
        bool generic = EqualsAscii(encodingName, "UTF-16");
        bool le = EqualsAscii(encodingName, "UTF-16LE");
        bool be = EqualsAscii(encodingName, "UTF-16BE");
        XmlInputEncoding legacy = encodingName.Length switch
        {
            8 when EqualsAscii(encodingName, "US-ASCII") => XmlInputEncoding.Ascii,
            10 when EqualsAscii(encodingName, "ISO-8859-1") => XmlInputEncoding.Latin1,
            12 when EqualsAscii(encodingName, "WINDOWS-1252") => XmlInputEncoding.Windows1252,
            _ => XmlInputEncoding.Undetermined
        };
        if (!empty && !utf8 && !generic && !le && !be && legacy == XmlInputEncoding.Undetermined)
            return Reject(0, 0, "INPUT.UnsupportedEncoding", location).Diagnostic;
        if (legacy != XmlInputEncoding.Undetermined && !signatureSelected && encoding == XmlInputEncoding.Utf8)
        {
            // Composition validates the bounded ASCII declaration before body input.
            // An unfinished UTF scalar or a non-ASCII prolog cannot be reinterpreted.
            if (provisionalNonAscii || count != 0)
                return Reject(0, 0, "INPUT.InvalidEncoding", location).Diagnostic;
            encoding = legacy;
            return null;
        }
        bool match = encoding switch
        {
            XmlInputEncoding.Utf8 => empty || utf8,
            XmlInputEncoding.Utf16LittleEndian => le || (bom && (empty || generic)),
            XmlInputEncoding.Utf16BigEndian => be || (bom && (empty || generic)),
            _ => false
        };
        return match ? null : Reject(0, 0, "INPUT.EncodingMismatch", location).Diagnostic;
    }
    private static bool EqualsAscii(ReadOnlySpan<char> value, ReadOnlySpan<char> expected)
    {
        if (value.Length != expected.Length) return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i]; if (c is >= 'a' and <= 'z') c = (char)(c - 32);
            if (c != expected[i]) return false;
        }
        return true;
    }
    public void Dispose() { disposed = true; carry = default; count = 0; }
}
