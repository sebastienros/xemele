using System.Text;
using System.Buffers;
using System.Threading;
using Xemele.Internal.Operations;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Dtd;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Xemele.SyntaxBenchmarks")]
[assembly: InternalsVisibleTo("Xemele.SyntaxSample")]
namespace Xemele.Parsing.Syntax;

// One operation owns its scratch, interned strings and open scopes. No source
// memory or sink view survives a callback. Text is streamed at scalar boundaries;
// only tags/declarations/references/PI targets require bounded lexical scratch.
internal sealed partial class XmlParserCore : IDisposable, IAsyncDisposable
{
    // Only bounded printable ASCII runs enter these paths. All delimiter,
    // normalization, Unicode and entity-origin cases retain the scalar parser.
    private static readonly SearchValues<char> TextStops = SearchValues.Create("\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u000a\u000b\u000c\u000d\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f<&]\ufffe\uffff");
    private static readonly SearchValues<char> DoubleQuotedStops = SearchValues.Create("\"&<\t\n\r");
    private static readonly SearchValues<char> SingleQuotedStops = SearchValues.Create("'&<\t\n\r");
    private static readonly SearchValues<char> PrintableTagUnits = SearchValues.Create(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
    private static readonly SearchValues<char> AsciiReference = SearchValues.Create(" !\"#$%&'()*+,-./0123456789:<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
    private static readonly SearchValues<char> AsciiTag = SearchValues.Create(" !#$%&()*+,-./0123456789:;<=?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
    private enum State { Data, Less, Bang, Tag, Close, Comment, CData, PiTarget, PiBody, Declaration, Reference, Doctype, DtdSubset, DtdClose }
    private enum Delivery { None, Document, Tag, Close, Text, Declaration, DocumentType, IncompleteDefaults, UnresolvedReference, EndDocument }
    internal readonly record struct Name(string Local, string Uri, string Prefix)
    {
        internal XmlNameView View => new(Local, Uri, Prefix);
    }
    private struct AttributeToken
    {
        internal int NameStart, NameLength, ValueStart, ValueLength;
        internal bool Namespace, IsDefaulted;
        internal Name Name;
        internal string Prefix, Uri;
        internal XmlLocation Location;
        internal string? OwnedName, OwnedValue;
        internal AttributeInterpretation? Incomplete;
    }
    internal readonly ref struct DomAttributeView(bool ns, Name name, string prefix, string uri, ReadOnlySpan<char> value, string? ownedValue, XmlLocation location, bool defaulted)
    {
        internal readonly bool Namespace = ns, IsDefaulted = defaulted;
        internal readonly Name Name = name;
        internal readonly string Prefix = prefix, Uri = uri;
        internal readonly ReadOnlySpan<char> Value = value;
        internal readonly string? OwnedValue = ownedValue;
        internal readonly XmlLocation Location = location;
    }
    internal readonly ref struct DomTagView(XmlParserCore core)
    {
        internal int Count => core.attributeCount;
        internal DomAttributeView this[int index]
        {
            get
            {
                ref var a = ref core.attributes[index];
                return new(a.Namespace, a.Name, a.Prefix, a.Uri, core.AttributeValue(a), a.OwnedValue, a.Location, a.IsDefaulted);
            }
        }
    }
    private readonly record struct Frame(Name Name, int BindingsStart, bool StandaloneWhitespaceDependency, DtdElementDeclaration? ValidationDeclaration);
    private readonly record struct Binding(string Prefix, string Uri);
    private const string XmlUri = "http://www.w3.org/XML/1998/namespace";
    private const string XmlnsUri = "http://www.w3.org/2000/xmlns/";
    private readonly OperationBudget budget;
    private readonly ScalarLocation position;
    private readonly bool trustedByteMappings;
    private Dictionary<string, string> names = new(StringComparer.Ordinal);
    private string?[]? recentInterns;
    private readonly List<Binding> bindings = [];
    private readonly List<Frame> frames = [];
    private char[] token = new char[128];
    private XmlLocation[] tokenLocations = new XmlLocation[128];
    private readonly record struct TokenLocationRun(int Start, int Length, XmlLocation Location, long ByteStride);
    private List<TokenLocationRun>? tokenLocationRuns;
    private XmlLocation TokenLocation(int index)
    {
        if (tokenLocationRuns is not null) for (int i = tokenLocationRuns.Count - 1; i >= 0; i--)
        {
            var run = tokenLocationRuns[i];
            if (index < run.Start) continue;
            int delta = index - run.Start;
            if (delta < run.Length)
                return new(run.Location.SourceUri, run.Location.LineNumber, run.Location.LinePosition + delta,
                    run.Location.ByteOffset is long offset ? offset + delta * run.ByteStride : null);
            break;
        }
        return tokenLocations[index];
    }
    private readonly StringBuilder physicalTag = new();
    private int[] physicalStarts = new int[128];
    private int physicalScalar;
    private XmlLocation attributePhysicalLocation;
    private bool attributePhysicalOrigin;
    private XmlLocation[] attributePhysicalLocations = [];
    private bool[] attributePhysicalOrigins = [];
    private string? attributePhysicalSpelling;
    private int attributePhysicalSpellingStart, attributePhysicalSpellingLength;
    private object? attributePhysicalIdentity;
    private object?[] attributePhysicalIdentities = [];
    private AttributeToken[] attributes = new AttributeToken[8];
    private readonly char[] fragment = new char[8];
    private State state;
    private Delivery delivery = Delivery.Document;
    private IXmlEventSink? sinkIdentity;
    private XmlDiagnostic? diagnostic;
    private XmlLocation opening, textLocation, fragmentLocation, referenceLocation, highLocation, heldLocation, heldSecondLocation;
    private int tokenLength, attributeCount, deliveryIndex, fragmentLength, held, brackets;
    private int sourceMode, gate;
    private CancellationToken activeCancellation;
    private long lastOffset = -1;
    private long mappingChecks;
    private long? byteLength;
    private char high, quote;
    private bool normalizedCr, finalSeen, disposed, faulted, terminal, rootSeen, rootClosed;
    private bool piImmediateClose, piSeparator;
    private bool textActive, textStarted, fragmentPending, fragmentEnded, emptyTag, endDocumentDelivered, declarationSeen;
    private bool declarationAllowed = true;
    private bool unresolvedPending;
    private int unresolvedNameLength;
    private long precedingReferenceTokenCharacters;
    private bool independentReferenceToken;
    private XmlTextKind textKind;
    private string target = "";
    private int versionStart, versionLength, encodingStart, encodingLength;
    private bool? standalone;
    private Name deliveredName;

    public XmlParserCore(XmlParseOptions options, Uri? sourceUri = null, bool asyncResources = false,
        OperationBudget? operationBudget = null, bool trustedByteMappings = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (sourceUri is { IsAbsoluteUri: false }) throw new ArgumentException("Source URI must be absolute.", nameof(sourceUri));
        this.asyncResources = asyncResources;
        this.trustedByteMappings = trustedByteMappings;
        // Composition supplies one sequential budget; its immutable options are
        // authoritative. Existing callers retain independent operation state.
        budget = operationBudget ?? new(options, default);
        position = new(sourceUri);
    }

    public ScanResult Append<TSink>(ReadOnlySpan<char> input, ReadOnlySpan<long> byteOffsets,
        bool isFinalBlock, TSink sink, CancellationToken cancellationToken) where TSink : class, IXmlEventSink
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0) throw new InvalidOperationException("Concurrent or reentrant core use.");
        int consumed = 0;
        try
        {
            CheckUsable();
            ArgumentNullException.ThrowIfNull(sink);
            if (sinkIdentity is not null && !ReferenceEquals(sinkIdentity, sink)) throw new InvalidOperationException("Resume with the same sink.");
            if (finalSeen && !isFinalBlock) throw new InvalidOperationException("Final flag is sticky.");
            bool firstInput = sourceMode == 0 && !input.IsEmpty;
            try { ValidateOffsets(input, byteOffsets, cancellationToken); }
            catch (OperationCanceledException) { faulted = true; AbortOrDeferEntities(); throw; }
            if (isFinalBlock && sourceMode == 2 && byteLength is null) throw new InvalidOperationException("CompleteByteSource must precede final byte input.");
            if (firstInput && sink is IXmlBulkTextSink bulkSink) bulkSink.PrepareInput(input);
            sinkIdentity = sink;
            finalSeen |= isFinalBlock;
            // Exceptions from callbacks and cancellation fault the operation and
            // propagate unchanged. Syntax failures never travel as exceptions.
            try
            {
                activeCancellation = cancellationToken;
                if (!asyncResources) resolution?.BindCancellationToken(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                while (true)
                {
                    if (diagnostic is not null)
                    {
                        if (asyncResources && !asyncCleanupCompleted)
                        { resourceWork = ResourceWork.Cleanup; return new(consumed, ScanStep.NeedResource); }
                        terminal = true; AbortOrDeferEntities(); return new(consumed, ScanStep.Rejected, diagnostic);
                    }
                    if (resourceWork != ResourceWork.None) return new(consumed, ScanStep.NeedResource);
                    if (!Drain(sink, cancellationToken)) return new(consumed, ScanStep.Paused);
                    if (endDocumentDelivered) { terminal = true; return new(consumed, ScanStep.Completed); }
                    if (asyncResources && DrainAsyncDtd()) continue;
                    if (sink is IXmlBulkTextSink)
                    {
                        var entityRun = DrainInternalEntityRun(out var entityAt);
                        if (!entityRun.IsEmpty)
                        {
                            if (!sink.TextFragment(entityRun, false, entityAt)) return new(consumed, ScanStep.Paused);
                            continue;
                        }
                    }
                    if (DrainEntityScalar()) continue;
                    if (consumed == input.Length)
                    {
                        if (!isFinalBlock) return new(consumed, ScanStep.NeedInput);
                        FinalizeSource();
                        continue;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (high == 0)
                    {
                        if (state == State.Data && !textActive && frames.Count != 0 && sink is IXmlDomBatchSink && !RecoveryEnabled &&
                            input[consumed] is not ('<' or '&'))
                        {
                            var start = position.Current(sourceMode == 2 ? byteOffsets[consumed] : null);
                            ResetToken(); BeginText(XmlTextKind.Text, "", start); declarationAllowed = false;
                            textStarted = true; delivery = Delivery.None;
                            if (!sink.StartText(XmlTextKind.Text, "", start)) return new(consumed, ScanStep.Paused);
                        }
                        if (state == State.Less && sink is IXmlBulkTextSink)
                        {
                            int wholeTag = ConsumeWholeAsciiTag(input[consumed..], sourceMode == 2 ? byteOffsets[consumed..] : []);
                            if (wholeTag != 0) { consumed += wholeTag; continue; }
                        }
                        if (state == State.Data && sink is IXmlDomBatchSink textBatch)
                        {
                            int normalizedRun = ConsumeNormalizedTextRun(input[consumed..], sourceMode == 2 ? byteOffsets[consumed..] : [], out int references, out var normalizedAt);
                            if (normalizedRun != 0)
                            {
                                var raw = input.Slice(consumed, normalizedRun); consumed += normalizedRun;
                                bool ended = consumed < input.Length && input[consumed] == '<';
                                if (ended) { textActive = false; textStarted = false; budget.Reset(Quota.TokenCharacters); }
                                bool keepGoing = references == 0 ? sink.TextFragment(raw, ended, normalizedAt) : textBatch.NormalizedTextRun(raw, ended, normalizedAt);
                                if (!keepGoing) return new(consumed, ScanStep.Paused);
                                continue;
                            }
                        }
                        if (input[consumed] == '&' && sink is IXmlBulkTextSink)
                        {
                            int reference = ConsumePredefinedReference(input[consumed..], sourceMode == 2 ? byteOffsets[consumed..] : [], out var referenceAt, out char value);
                            if (reference != 0)
                            {
                                consumed += reference; fragment[0] = value;
                                if (!sink.TextFragment(fragment.AsSpan(0, 1), false, referenceAt)) return new(consumed, ScanStep.Paused);
                                continue;
                            }
                        }
                        int run = ConsumePlainRun(input[consumed..], sourceMode == 2 ? byteOffsets[consumed..] : [], sink is IXmlBulkTextSink, out var runLocation);
                        if (run != 0)
                        {
                            var borrowed = input.Slice(consumed, run);
                            consumed += run;
                            if (state == State.Data)
                            {
                                bool ended = sink is IXmlDomBatchSink && consumed < input.Length && input[consumed] == '<';
                                if (ended) { textActive = false; textStarted = false; budget.Reset(Quota.TokenCharacters); }
                                if (!sink.TextFragment(borrowed, ended, runLocation)) return new(consumed, ScanStep.Paused);
                            }
                            continue;
                        }
                    }
                    char c = input[consumed];
                    long? offset = sourceMode == 2 ? byteOffsets[consumed] : null;
                    if (!trustedByteMappings && offset is long original) ValidateMapping(original, c, nameof(byteOffsets));
                    var location = high != 0 ? highLocation : position.Current(offset);
                    if (high != 0)
                    {
                        if (!char.IsLowSurrogate(c)) { Reject("XML.Syntax", "Unpaired high surrogate.", highLocation); continue; }
                        if (sourceMode == 2 && offset != highLocation.ByteOffset) throw new ArgumentException("Surrogate mappings differ.", nameof(byteOffsets));
                        int scalar = char.ConvertToUtf32(high, c); high = '\0';
                        consumed++; if (offset is long b) lastOffset = b;
                        AcceptScalar(scalar, location);
                    }
                    else if (char.IsHighSurrogate(c))
                    {
                        high = c; highLocation = location; consumed++; if (offset is long b) lastOffset = b;
                    }
                    else if (char.IsLowSurrogate(c)) Reject("XML.Syntax", "Unpaired low surrogate.", location);
                    else
                    {
                        consumed++; if (offset is long b) lastOffset = b;
                        AcceptScalar(c, location);
                    }
                }
            }
            catch { faulted = true; AbortOrDeferEntities(); throw; }
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    public void CompleteByteSource(long byteLength)
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0) throw new InvalidOperationException("Concurrent or reentrant core use.");
        try
        {
            CheckUsable();
            if (sourceMode == 1 || finalSeen || this.byteLength is not null) throw new InvalidOperationException("Invalid byte completion marker.");
            if (byteLength < 0 || byteLength < lastOffset) throw new ArgumentOutOfRangeException(nameof(byteLength));
            this.byteLength = byteLength; sourceMode = 2;
        }
        finally { Volatile.Write(ref gate, 0); }
    }
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0) throw new InvalidOperationException("Concurrent or reentrant core use.");
        try
        {
            if (disposed) return;
            disposed = true; ClearUnresolvedReference(); tokenLocationRuns = null; token = []; tokenLocations = []; physicalStarts = []; attributePhysicalLocations = []; attributePhysicalOrigins = []; attributePhysicalIdentities = []; attributePhysicalIdentity = null; attributePhysicalSpelling = null; physicalTag.Clear(); attributes = [];
            try { DisposeResourcesSynchronously(); }
            finally
            {
                names.Clear(); names = null!;
                bindings.Clear(); bindings.TrimExcess();
                budget.Release(Quota.Depth, frames.Count); frames.Clear(); frames.TrimExcess(); sinkIdentity = null;
            }
        }
        finally { Volatile.Write(ref gate, 0); }
    }
    private void CheckUsable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted || terminal) throw new InvalidOperationException("Core operation is terminal or faulted.");
    }
    private void ValidateOffsets(ReadOnlySpan<char> input, ReadOnlySpan<long> offsets, CancellationToken cancellation)
    {
        if (!offsets.IsEmpty && offsets.Length != input.Length) throw new ArgumentException("Parallel mappings must match input length.", nameof(offsets));
        int mode = offsets.IsEmpty ? 1 : 2;
        if (!input.IsEmpty && sourceMode != 0 && sourceMode != mode) throw new ArgumentException("Source mode cannot change.", nameof(offsets));
        cancellation.ThrowIfCancellationRequested();
        // At most one preflight unit, even if a pending callback pauses before
        // consuming input. The tail is caller-owned and can change on a resume.
        if (!offsets.IsEmpty) ValidateMapping(offsets[0], input[0], nameof(offsets));
        if (!input.IsEmpty && sourceMode == 0) sourceMode = mode;
    }
    private void ValidateMapping(long current, char unit, string parameter)
    {
        // A saturating private work counter permits a count-based regression
        // without timing shared-host tests or adding an ABI declaration.
        if (mappingChecks < long.MaxValue) mappingChecks++;
        if (current < 0 || current < lastOffset || (byteLength is long end && current > end))
            throw new ArgumentException("Invalid original-byte mapping.", parameter);
        if (high != 0 && char.IsLowSurrogate(unit) && current != highLocation.ByteOffset)
            throw new ArgumentException("Surrogate mappings differ.", parameter);
    }
    private int ConsumeWholeAsciiTag(ReadOnlySpan<char> input, ReadOnlySpan<long> offsets)
    {
        if (entities is not null || RecoveryEnabled || sourceMode == 2 && !trustedByteMappings || input[0] is '?' or '!') return 0;
        if (input[0] != '/' && input[0] is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_' or ':')) return 0;
        int capacity = Math.Min(4096, Math.Min(input.Length, position.ColumnCapacity));
        capacity = (int)Math.Min(capacity, Math.Min(budget.Remaining(Quota.DocumentCharacters), budget.Remaining(Quota.TokenCharacters) - 1));
        if (capacity < 2) return 0;
        var candidate = input[..capacity];
        int end = 0; char delimiter = '\0';
        bool closing = candidate[0] == '/';
        if (closing)
        {
            int found = candidate.IndexOf('>');
            if (found < 0) return 0;
            end = found + 1;
        }
        while (!closing && end < candidate.Length)
        {
            int stop = delimiter == 0 ? candidate[end..].IndexOfAny('>', '\'', '"') : candidate[end..].IndexOf(delimiter);
            if (stop < 0) return 0;
            end += stop;
            char c = candidate[end++];
            if (delimiter != 0) delimiter = '\0';
            else if (c == '>') break;
            else delimiter = c;
        }
        if (end == 0 || candidate[end - 1] != '>' || delimiter != 0 || candidate[..end].IndexOfAnyExcept(PrintableTagUnits) >= 0) return 0;
        bool close = candidate[0] == '/';
        int start = close ? 1 : 0, length = end - start - 1;
        if (length > token.Length)
        {
            int size = Math.Max(length, token.Length * 2);
            Array.Resize(ref token, size); Array.Resize(ref tokenLocations, size);
        }
        candidate.Slice(start, length).CopyTo(token);
        var location = position.Current(sourceMode == 2 ? offsets[start] : null);
        tokenLocationRuns?.Clear();
        if (length != 0)
        {
            long stride = sourceMode == 2 ? offsets[1] - offsets[0] : 0;
            (tokenLocationRuns ??= new(8)).Add(new(0, length, new(location.SourceUri, location.LineNumber, location.LinePosition + start, location.ByteOffset), stride));
        }
        tokenLength = length; declarationAllowed = false; quote = '\0';
        budget.TryCharge(Quota.DocumentCharacters, end); budget.TryCharge(Quota.TokenCharacters, end + 1);
        position.AdvanceColumns(end); normalizedCr = false;
        if (sourceMode == 2) lastOffset = offsets[end - 1];
        state = close ? State.Close : State.Tag;
        if (close) ParseClose(); else ParseTag();
        state = State.Data;
        return end;
    }
    private ReadOnlySpan<char> DrainInternalEntityRun(out XmlLocation at)
    {
        at = default;
        if (state != State.Data || !textActive || !textStarted || frames.Count == 0 ||
            frames[^1].StandaloneWhitespaceDependency || entitySources is not { Count: > 0 }) return [];
        var source = entitySources[^1];
        if (source.External is not null || source.Declaration.ReplacementText is not { } text || source.Index == text.Length) return [];
        int length = Math.Min(4096, text.Length - source.Index);
        length = (int)Math.Min(length, Math.Min(budget.Remaining(Quota.TokenCharacters), budget.Remaining(Quota.ExpandedCharacters)));
        var run = text.AsSpan(source.Index, length);
        int stop = run.IndexOfAny(TextStops);
        if (stop >= 0) run = run[..stop];
        int surrogate = run.IndexOfAnyInRange('\ud800', '\udfff');
        if (surrogate >= 0) run = run[..surrogate];
        if (run.Length < 2 || brackets >= 2 && run[0] == '>') return [];
        // Internal declarations already normalized their physical newlines.
        // Replacement literals keep the original reference coordinates and do
        // not advance the primary source or its document-character quota.
        at = source.Reference;
        budget.TryCharge(Quota.TokenCharacters, run.Length); budget.TryCharge(Quota.ExpandedCharacters, run.Length);
        source.Index += run.Length; entitySources[^1] = source; brackets = 0;
        return run;
    }
    private int ConsumeNormalizedTextRun(ReadOnlySpan<char> input, ReadOnlySpan<long> offsets, out int references, out XmlLocation at)
    {
        references = 0; at = default;
        if (entities is not null || !textActive || !textStarted || frames.Count == 0 || RecoveryEnabled ||
            sourceMode == 2 && !trustedByteMappings || brackets >= 2 && input[0] == '>') return 0;
        if (input[0] is '<' or ']' or '\r' or '\n') return 0;
        int capacity = Math.Min(4096, Math.Min(input.Length, position.ColumnCapacity));
        capacity = (int)Math.Min(capacity, Math.Min(budget.Remaining(Quota.DocumentCharacters), budget.Remaining(Quota.TokenCharacters)));
        if (capacity < 2) return 0;
        var candidate = input[..capacity]; int length = 0, pairs = 0;
        long expandedCapacity = budget.Remaining(Quota.ExpandedCharacters);
        while (length < candidate.Length)
        {
            var remaining = candidate[length..]; int ordinary = remaining.IndexOfAny(TextStops);
            if (ordinary < 0) ordinary = remaining.Length;
            int valid = ordinary;
            for (int start = 0; start < ordinary;)
            {
                int surrogate = remaining.Slice(start, ordinary - start).IndexOfAnyInRange('\ud800', '\udfff');
                if (surrogate < 0) break;
                surrogate += start;
                if (!char.IsHighSurrogate(remaining[surrogate]) || surrogate + 1 == ordinary || !char.IsLowSurrogate(remaining[surrogate + 1]))
                { valid = surrogate; break; }
                pairs++; start = surrogate + 2;
            }
            length += valid;
            if (valid != ordinary || length == candidate.Length || candidate[length] != '&' || references >= expandedCapacity) break;
            int reference = PredefinedReferenceLength(candidate[length..], out _);
            if (reference == 0 || budget.Remaining(Quota.EntityDepth) < 1) break;
            length += reference; references++;
        }
        if (length < 2) return 0;
        at = position.Current(sourceMode == 2 ? offsets[0] : null);
        budget.TryCharge(Quota.DocumentCharacters, length - pairs); budget.TryCharge(Quota.TokenCharacters, length - pairs);
        budget.TryCharge(Quota.ExpandedCharacters, references); position.AdvanceColumns(length - pairs);
        if (sourceMode == 2) lastOffset = offsets[length - 1];
        normalizedCr = false; brackets = 0;
        return length;
    }
    private static int PredefinedReferenceLength(ReadOnlySpan<char> input, out char value)
    {
        if (input.StartsWith("&amp;")) { value = '&'; return 5; }
        if (input.StartsWith("&lt;")) { value = '<'; return 4; }
        if (input.StartsWith("&gt;")) { value = '>'; return 4; }
        if (input.StartsWith("&apos;")) { value = '\''; return 6; }
        if (input.StartsWith("&quot;")) { value = '"'; return 6; }
        value = '\0'; return 0;
    }
    private int ConsumePredefinedReference(ReadOnlySpan<char> input, ReadOnlySpan<long> offsets, out XmlLocation at, out char value)
    {
        at = default; value = '\0';
        if (state != State.Data || !textActive || !textStarted || frames.Count == 0 || entities is not null) return 0;
        int length = PredefinedReferenceLength(input, out value);
        if (length == 0) return 0;
        // Fall back before accepting anything near any boundary so the original
        // scalar path reports the exact first excess unit/reference location.
        if (length > position.ColumnCapacity || length > budget.Remaining(Quota.DocumentCharacters) ||
            length > budget.Remaining(Quota.TokenCharacters) || budget.Remaining(Quota.EntityDepth) < 1 ||
            budget.Remaining(Quota.ExpandedCharacters) < 1) return 0;
        at = position.Current(sourceMode == 2 ? offsets[0] : null);
        if (sourceMode == 2)
        {
            if (!trustedByteMappings) for (int i = 0; i < length; i++)
            { ValidateMapping(offsets[i], input[i], nameof(offsets)); lastOffset = offsets[i]; }
            else lastOffset = offsets[length - 1];
        }
        budget.TryCharge(Quota.DocumentCharacters, length); budget.TryCharge(Quota.TokenCharacters, length);
        budget.TryCharge(Quota.ExpandedCharacters, 1);
        position.AdvanceColumns(length); normalizedCr = false; brackets = 0; tokenLength = 0;
        return length;
    }
    private int ConsumePlainRun(ReadOnlySpan<char> input, ReadOnlySpan<long> offsets, bool bulkText, out XmlLocation at)
    {
        at = default;
        bool data = state == State.Data;
        if (data)
        {
            // Dense markup/reference boundaries are cheaper as direct scalar
            // dispatch than as repeated multi-set searches (parser study E01).
            if (input[0] is '<' or '&' or ']' or '\r' or '\n') return 0;
            if (!bulkText || !textActive || !textStarted || frames.Count == 0 || frames[^1].StandaloneWhitespaceDependency ||
                brackets >= 2 && input[0] == '>' || entitySources is { Count: > 0 }) return 0;
        }
        else if (state is not (State.Tag or State.Close or State.Reference)) return 0;
        int length = Math.Min(input.Length, 4096);
        length = Math.Min(length, position.ColumnCapacity);
        length = (int)Math.Min(length, Math.Min(budget.Remaining(Quota.DocumentCharacters), budget.Remaining(Quota.TokenCharacters)));
        if (length == 0) return 0;
        var candidate = input[..length];
        int stop = data ? candidate.IndexOfAny(TextStops) : candidate.IndexOfAnyExcept(state == State.Reference ? AsciiReference : AsciiTag);
        if (stop >= 0) length = stop;
        int pairs = 0;
        if (data) for (int start = 0; start < length;)
        {
            int next = candidate.Slice(start, length - start).IndexOfAnyInRange('\ud800', '\udfff');
            if (next < 0) break;
            next += start;
            if (!char.IsHighSurrogate(candidate[next]) || next + 1 == length || !char.IsLowSurrogate(candidate[next + 1]))
            { length = next; break; }
            if (sourceMode == 2 && offsets[next] != offsets[next + 1])
            {
                // Let the scalar path reject this caller-owned mapping exactly
                // when it reaches the pair, respecting any earlier pause.
                length = next; break;
            }
            pairs++; start = next + 2;
        }
        if (length < 2) return 0;
        at = position.Current(sourceMode == 2 ? offsets[0] : null);
        if (!data)
        {
            if (length > Array.MaxLength - tokenLength) return 0;
            int required = tokenLength + length;
            if (required > token.Length)
            {
                int capacity = (int)Math.Min(Array.MaxLength, Math.Max((long)required, (long)token.Length * 2));
                Array.Resize(ref token, capacity); Array.Resize(ref tokenLocations, capacity);
            }
            input[..length].CopyTo(token.AsSpan(tokenLength));
        }
        if (state == State.Tag && entities is not null)
        {
            int required = tokenLength + length;
            if (required > physicalStarts.Length) Array.Resize(ref physicalStarts, token.Length);
            if (required > attributePhysicalLocations.Length)
            {
                Array.Resize(ref attributePhysicalLocations, token.Length); Array.Resize(ref attributePhysicalOrigins, token.Length);
                Array.Resize(ref attributePhysicalIdentities, token.Length);
            }
            if (length > int.MaxValue - physicalTag.Length) return 0;
            int physicalStart = physicalTag.Length;
            for (int i = 0; i < length; i++)
            {
                physicalStarts[tokenLength + i] = physicalStart + i;
                attributePhysicalLocations[tokenLength + i] = new(at.SourceUri, at.LineNumber, at.LinePosition + i, sourceMode == 2 ? offsets[i] : null);
                attributePhysicalOrigins[tokenLength + i] = false; attributePhysicalIdentities[tokenLength + i] = null;
            }
            physicalTag.Append(input[..length]);
        }
        bool compressedLocations = !data && (sourceMode != 2 || trustedByteMappings);
        if (compressedLocations)
        {
            // A validated ASCII run cannot contain a newline or surrogate. Its
            // coordinates are affine; reconstruct only locations actually used
            // by grammar/diagnostics instead of writing a 32-byte record/unit.
            if (tokenLength == 0) tokenLocationRuns?.Clear();
            long stride = sourceMode == 2 ? offsets[1] - offsets[0] : 0;
            (tokenLocationRuns ??= new(8)).Add(new(tokenLength, length, at, stride));
        }
        if (!data && !compressedLocations || sourceMode == 2 && !trustedByteMappings) for (int i = 0; i < length; i++)
        {
            long? offset = sourceMode == 2 ? offsets[i] : null;
            if (!trustedByteMappings && offset is long original) { ValidateMapping(original, input[i], nameof(offsets)); lastOffset = original; }
            if (!data && !compressedLocations) tokenLocations[tokenLength + i] = new(at.SourceUri, at.LineNumber, at.LinePosition + i, offset);
        }
        if (trustedByteMappings && sourceMode == 2) lastOffset = offsets[length - 1];
        budget.TryCharge(Quota.DocumentCharacters, length - pairs);
        budget.TryCharge(Quota.TokenCharacters, length - pairs);
        position.AdvanceColumns(length - pairs);
        normalizedCr = false;
        if (data) brackets = 0;
        else tokenLength += length;
        return length;
    }
    private static bool XmlScalar(int c) => c is 9 or 10 or 13 || c is >= 0x20 and <= 0xD7FF || c is >= 0xE000 and <= 0xFFFD || c is >= 0x10000 and <= 0x10FFFF;
    private void AcceptScalar(int scalar, XmlLocation location)
    {
        if (!XmlScalar(scalar)) { Reject("XML.Syntax", "Disallowed XML scalar.", location); return; }
        if (!Charge(Quota.DocumentCharacters, 1, location)) return;
        if (!position.TryAdvance(new Rune(scalar))) { Reject("XML.LocationLimit", "Location capacity exceeded.", location); return; }
        if (state == State.DtdSubset)
        {
            normalizedCr = scalar == '\r';
            ProcessDtdScalar(scalar, location);
            return;
        }
        physicalScalar = scalar; attributePhysicalLocation = location; attributePhysicalOrigin = false; attributePhysicalSpelling = null; attributePhysicalIdentity = null;
        bool skip = normalizedCr && scalar == '\n'; normalizedCr = scalar == '\r';
        if (skip && state == State.Tag && entities is not null)
        {
            if (physicalTag.Length == int.MaxValue) { Reject("XML.ResourceLimit", "Physical tag capacity exceeded.", location); return; }
            physicalTag.Append('\n');
        }
        if (skip) return;
        if (scalar == '\r') scalar = '\n';
        Process(scalar, location);
    }
    private bool Charge(Quota quota, long count, XmlLocation location)
    {
        if (budget.TryCharge(quota, count)) return true;
        Reject("XML.ResourceLimit", "XML resource limit exceeded.", location); return false;
    }
    private void Reject(string code, string message, XmlLocation location) => diagnostic ??= new(code, XmlDiagnosticSeverity.Error, message, location);
    private bool Store(int scalar, XmlLocation location)
    {
        if (!Charge(Quota.TokenCharacters, 1, location)) return false;
        if (tokenLength == 0) tokenLocationRuns?.Clear();
        int width = scalar > 0xFFFF ? 2 : 1;
        if (tokenLength > int.MaxValue - width) { Reject("XML.ResourceLimit", "Token capacity exceeded.", location); return false; }
        if (tokenLength + width > token.Length)
        {
            int capacity = (int)Math.Min(int.MaxValue, Math.Max((long)tokenLength + width, (long)token.Length * 2));
            Array.Resize(ref token, capacity); Array.Resize(ref tokenLocations, capacity);
        }
        if (state == State.Tag && entities is not null)
        {
            if (tokenLength + width > physicalStarts.Length) Array.Resize(ref physicalStarts, token.Length);
            if (tokenLength + width > attributePhysicalLocations.Length)
            { Array.Resize(ref attributePhysicalLocations, token.Length); Array.Resize(ref attributePhysicalOrigins, token.Length); Array.Resize(ref attributePhysicalIdentities, token.Length); }
            for (int u = 0; u < width; u++) { attributePhysicalLocations[tokenLength + u] = attributePhysicalLocation; attributePhysicalOrigins[tokenLength + u] = attributePhysicalOrigin; attributePhysicalIdentities[tokenLength + u] = attributePhysicalIdentity; }
            physicalStarts[tokenLength] = physicalTag.Length;
            Span<char> original = stackalloc char[2]; int n = new Rune(physicalScalar == 0 ? scalar : physicalScalar).EncodeToUtf16(original);
            ReadOnlySpan<char> spelling = attributePhysicalSpelling is not null
                ? attributePhysicalSpelling.AsSpan(attributePhysicalSpellingStart, attributePhysicalSpellingLength) : original[..n];
            if (physicalTag.Length > int.MaxValue - spelling.Length) { Reject("XML.ResourceLimit", "Physical tag capacity exceeded.", location); return false; }
            physicalTag.Append(spelling);
            if (width == 2) physicalStarts[tokenLength + 1] = physicalStarts[tokenLength];
        }
        if (width == 1) { token[tokenLength] = (char)scalar; tokenLocations[tokenLength++] = location; }
        else
        {
            scalar -= 0x10000; token[tokenLength] = (char)(0xD800 + (scalar >> 10)); tokenLocations[tokenLength++] = location;
            token[tokenLength] = (char)(0xDC00 + (scalar & 1023)); tokenLocations[tokenLength++] = location;
        }
        return true;
    }
    private void ResetToken() { tokenLocationRuns?.Clear(); physicalTag.Clear(); if (attributePhysicalIdentities.Length != 0) Array.Clear(attributePhysicalIdentities); tokenLength = 0; budget.Reset(Quota.TokenCharacters); }
    private void BeginText(XmlTextKind kind, string piTarget, XmlLocation location)
    {
        textActive = true; textStarted = false; textKind = kind; target = piTarget; textLocation = location;
        held = 0; brackets = 0; delivery = Delivery.Text;
    }
    private void QueueFragment(int scalar, XmlLocation location, int prefixCount = 0, char prefix = '\0')
    {
        fragmentLength = prefixCount;
        for (int i = 0; i < prefixCount; i++) fragment[i] = prefix;
        if (scalar >= 0)
        {
            if (scalar <= 0xFFFF) fragment[fragmentLength++] = (char)scalar;
            else { scalar -= 0x10000; fragment[fragmentLength++] = (char)(0xD800 + (scalar >> 10)); fragment[fragmentLength++] = (char)(0xDC00 + (scalar & 1023)); }
        }
        fragmentLocation = location; fragmentEnded = false; fragmentPending = true; delivery = Delivery.Text;
    }
    private void EndText(XmlLocation location)
    {
        QueueFragment(-1, location); fragmentEnded = true;
    }
    private void Process(int c, XmlLocation location)
    {
        switch (state)
        {
            case State.Data:
                if (c == '<')
                {
                    opening = location; state = State.Less; ResetToken();
                    if (textActive) EndText(location);
                    return;
                }
                if (!textActive && c == '&' && entities is not null && frames.Count != 0)
                {
                    ResetToken(); declarationAllowed = false;
                    if (!Charge(Quota.TokenCharacters, 1, location)) return;
                    referenceLocation = location; state = State.Reference; return;
                }
                if (!textActive) { ResetToken(); BeginText(XmlTextKind.Text, "", location); declarationAllowed = false; }
                if (frames.Count == 0 && !White(c) && !RecoverOutsideText(location)) { Reject("XML.Syntax", "Non-whitespace outside the root.", location); return; }
                if (c == '&' && AllowsUnresolvedContent)
                {
                    // Unknown content ends the preceding text token. Bound its
                    // candidate independently while preserving old accounting if
                    // lookup ultimately identifies a represented expansion.
                    precedingReferenceTokenCharacters = budget.Count(Quota.TokenCharacters);
                    independentReferenceToken = true; budget.Reset(Quota.TokenCharacters);
                }
                if (!Charge(Quota.TokenCharacters, 1, location)) return;
                if (c == '&') { referenceLocation = location; tokenLength = 0; state = State.Reference; return; }
                if (c == '>' && brackets >= 2) { Reject("XML.Syntax", "Literal ]]> in text.", location); return; }
                brackets = c == ']' ? Math.Min(2, brackets + 1) : 0;
                // The standalone dependency is on XML S in element content,
                // not on whitespace produced by CDATA or a direct character
                // reference. Parsed entity replacement enters this Data state
                // again and therefore retains its literal-content origin.
                if (White(c) && frames.Count != 0 && frames[^1].StandaloneWhitespaceDependency)
                { Reject("XML.Standalone", "Standalone document depends on external element-content whitespace.", location); return; }
                QueueFragment(c, location); return;
            case State.Less:
                // The opening '<' is charged after a preceding text token has
                // drained, so each complete lexical token includes delimiters.
                if (!Charge(Quota.TokenCharacters, 1, opening)) return;
                if (c == '!') { Charge(Quota.TokenCharacters, 1, location); state = State.Bang; return; }
                if (c == '?') { Charge(Quota.TokenCharacters, 1, location); state = State.PiTarget; return; }
                declarationAllowed = false;
                if (c == '/') { Charge(Quota.TokenCharacters, 1, location); state = State.Close; return; }
                state = State.Tag; quote = '\0'; Store(c, location); return;
            case State.Bang:
                if (!Store(c, location)) return;
                var prefix = token.AsSpan(0, tokenLength);
                if (prefix.SequenceEqual("DOCTYPE"))
                {
                    if (budget.Options.DtdProcessing == XmlDtdProcessing.Prohibit)
                    { Reject("XML.DtdProhibited", "DTD syntax is prohibited.", opening); return; }
                    if (rootSeen || doctypeSeen || entitySources is { Count: > 0 })
                    { Reject("XML.Syntax", "Invalid DOCTYPE placement.", opening); return; }
                    doctypeSeen = true; declarationAllowed = false; ResetToken(); state = State.Doctype; quote = '\0';
                    entityBudget ??= new(budget, names);
                    return;
                }
                if (prefix.SequenceEqual("--")) { tokenLength = 0; state = State.Comment; BeginText(XmlTextKind.Comment, "", opening); declarationAllowed = false; return; }
                if (prefix.SequenceEqual("[CDATA["))
                {
                    if (frames.Count == 0 && !RecoverOutsideCData()) { Reject("XML.Syntax", "CDATA outside the root.", opening); return; }
                    tokenLength = 0; state = State.CData; BeginText(XmlTextKind.CData, "", opening); declarationAllowed = false; return;
                }
                if (!"DOCTYPE".AsSpan().StartsWith(prefix) && !"--".AsSpan().StartsWith(prefix) && !"[CDATA[".AsSpan().StartsWith(prefix))
                    Reject("XML.Syntax", "Invalid markup declaration.", opening);
                return;
            case State.Doctype:
                if (quote == 0 && c is '[' or '>')
                {
                    if (!ParseDoctypeHeader()) return;
                    if (c == '[') BeginDtdSubset(); else CompleteDoctype();
                    return;
                }
                if (quote == 0 && c is '\'' or '"') quote = (char)c;
                else if (c == quote) quote = '\0';
                Store(c, location); return;
            case State.DtdClose:
                if (White(c)) return;
                if (c != '>') { Reject("XML.Syntax", "Expected DOCTYPE closing delimiter.", location); return; }
                CompleteDoctype(); return;
            case State.Tag:
                if (c == '>' && quote == 0) { if (Charge(Quota.TokenCharacters, 1, location)) ParseTag(); state = State.Data; return; }
                if (quote == 0 && c is '\'' or '"') quote = (char)c;
                else if (c == quote) quote = '\0';
                Store(c, location); return;
            case State.Close:
                if (c == '>') { if (Charge(Quota.TokenCharacters, 1, location)) ParseClose(); state = State.Data; return; }
                Store(c, location); return;
            case State.Reference:
                if (c == ';')
                {
                    if (!Charge(Quota.TokenCharacters, 1, location)) return;
                    var reference = token.AsSpan(0, tokenLength);
                    bool pushed = PushGeneralEntity(reference, referenceLocation);
                    if (diagnostic is null && (pushed || ScalarReference(reference))) RetainValidationReference(false, referenceLocation);
                    if (!pushed && !ScalarReference(reference) && ValidName(reference, false) &&
                        AllowsUnresolvedContent)
                    {
                        independentReferenceToken = false;
                        QueueUnresolvedReference();
                        state = State.Data; brackets = 0;
                        return;
                    }
                    if (independentReferenceToken)
                    {
                        independentReferenceToken = false;
                        if (!Charge(Quota.TokenCharacters, precedingReferenceTokenCharacters, referenceLocation)) return;
                    }
                    int scalar = pushed ? -1 : Reference(reference, referenceLocation);
                    state = State.Data; tokenLength = 0; brackets = 0;
                    if (diagnostic is null && !pushed)
                    {
                        if (!reference.IsEmpty && reference[0] == '#' && White(scalar)) RetainValidationReference(true, referenceLocation);
                        if (!textActive) BeginText(XmlTextKind.Text, "", referenceLocation);
                        QueueFragment(scalar, referenceLocation);
                    }
                }
                else Store(c, location);
                return;
            case State.Comment:
                if (!Charge(Quota.TokenCharacters, 1, location)) return;
                if (held == 2)
                {
                    if (c != '>') { Reject("XML.Syntax", "Double hyphen in comment.", location); return; }
                    held = 0; EndText(location); state = State.Data; return;
                }
                if (c == '-') { if (held == 0) heldLocation = location; held++; return; }
                QueueFragment(c, held > 0 ? heldLocation : location, held, '-'); held = 0; return;
            case State.CData:
                if (!Charge(Quota.TokenCharacters, 1, location)) return;
                if (c == ']')
                {
                    if (held == 0) { heldLocation = location; held = 1; }
                    else if (held == 1) { heldSecondLocation = location; held = 2; }
                    else { QueueFragment(']', heldLocation); heldLocation = heldSecondLocation; heldSecondLocation = location; }
                    return;
                }
                if (held == 2 && c == '>') { held = 0; EndText(location); state = State.Data; return; }
                QueueFragment(c, held > 0 ? heldLocation : location, held, ']'); held = 0; return;
            case State.PiTarget:
                if (White(c) || c == '?')
                {
                    if (!ValidName(token.AsSpan(0, tokenLength), false)) { Reject("XML.Syntax", "Invalid PI target.", opening); return; }
                    var t = token.AsSpan(0, tokenLength);
                    if (t.Equals("xml", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!t.SequenceEqual("xml") || !declarationAllowed || declarationSeen || c == '?') { Reject("XML.Syntax", "Invalid XML declaration placement.", opening); return; }
                        state = State.Declaration; Store(c, location); return;
                    }
                    if (!Charge(Quota.TokenCharacters, 1, location)) return;
                    target = Intern(t, opening); tokenLength = 0; BeginText(XmlTextKind.ProcessingInstruction, target, opening);
                    declarationAllowed = false; state = State.PiBody; held = c == '?' ? 1 : 0; heldLocation = location;
                    piImmediateClose = c == '?'; piSeparator = !piImmediateClose; return;
                }
                Store(c, location); return;
            case State.PiBody:
                if (piImmediateClose && c != '>') { Reject("XML.Syntax", "PI data requires separating whitespace.", location); return; }
                piImmediateClose = false;
                if (!Charge(Quota.TokenCharacters, 1, location)) return;
                // XML PI content excludes the complete S after its target.
                // Whitespace after the first body scalar is ordinary content.
                if (piSeparator && White(c)) return;
                piSeparator = false;
                if (held == 1 && c == '>') { held = 0; EndText(location); state = State.Data; return; }
                if (c == '?')
                {
                    if (held == 1) QueueFragment('?', heldLocation);
                    heldLocation = location; held = 1; return;
                }
                QueueFragment(c, held > 0 ? heldLocation : location, held, '?'); held = 0; return;
            case State.Declaration:
                if (held == 1 && c == '>') { tokenLength--; if (Charge(Quota.TokenCharacters, 1, location)) ParseDeclaration(); state = State.Data; held = 0; return; }
                held = c == '?' ? 1 : 0; Store(c, location); return;
        }
    }
    private bool AllowsUnresolvedContent => standalone != true && doctypeSeen &&
        (doctypeSystem is not null || subsetHasParameterReferences);

    private void QueueUnresolvedReference()
    {
        // Persisted names use the existing operation-wide bounded intern table.
        Intern(token.AsSpan(0, tokenLength), referenceLocation);
        if (diagnostic is not null) return;
        if (tokenLength > Array.MaxLength - 2)
        { Reject("XML.ResourceLimit", "Reference capacity exceeded.", referenceLocation); return; }
        unresolvedNameLength = tokenLength;
        int length = tokenLength + 2;
        if (length > token.Length)
        {
            int capacity = (int)Math.Min(Array.MaxLength, Math.Max((long)length, (long)token.Length * 2));
            Array.Resize(ref token, capacity);
            Array.Resize(ref tokenLocations, capacity);
        }
        token.AsSpan(0, tokenLength).CopyTo(token.AsSpan(1));
        token[0] = '&'; token[length - 1] = ';'; tokenLength = length;
        unresolvedPending = true;
        if (textActive) EndText(referenceLocation);
        else delivery = Delivery.UnresolvedReference;
    }

    private void ClearUnresolvedReference()
    {
        if (unresolvedNameLength != 0) Array.Clear(token, 0, unresolvedNameLength + 2);
        if (disposed) { tokenLocationRuns = null; recentInterns = null; } else tokenLocationRuns?.Clear();
        unresolvedPending = false; unresolvedNameLength = 0; tokenLength = 0;
        budget.Reset(Quota.TokenCharacters);
    }

    private void FinalizeSource()
    {
        var eof = position.Current(sourceMode == 2 ? byteLength : null);
        if (high != 0) { Reject("XML.Syntax", "Incomplete surrogate at EOF.", highLocation); return; }
        if (RecoverPhysicalEof(eof)) return;
        if (state != State.Data || frames.Count != 0 || !rootSeen) { Reject("XML.Syntax", "Incomplete XML document.", eof); return; }
        if (textActive) { EndText(eof); return; }
        opening = eof; delivery = Delivery.EndDocument;
    }
    private bool CompleteTag()
    {
        for (int i = 0; i < attributeCount; i++) if (attributes[i].Incomplete is not null) return false;
        return true;
    }
    private bool Drain(IXmlEventSink sink, CancellationToken cancellation)
    {
        while ((delivery != Delivery.None || unresolvedPending) && diagnostic is null)
        {
            if (DrainDiscardedRecovery()) continue;
            if (delivery == Delivery.None) delivery = Delivery.UnresolvedReference;
            cancellation.ThrowIfCancellationRequested();
            bool keepGoing;
            switch (delivery)
            {
                case Delivery.Document:
                    delivery = Delivery.None; keepGoing = sink.StartDocument(position.Current(sourceMode == 2 ? (lastOffset < 0 ? 0 : lastOffset) : null)); break;
                case Delivery.Text:
                    if (!textStarted) { textStarted = true; keepGoing = sink.StartText(textKind, target, textLocation); }
                    else if (fragmentPending)
                    {
                        fragmentPending = false;
                        if (fragmentEnded) { textActive = false; textStarted = false; budget.Reset(Quota.TokenCharacters); }
                        delivery = Delivery.None;
                        keepGoing = sink.TextFragment(fragment.AsSpan(0, fragmentLength), fragmentEnded, fragmentLocation);
                    }
                    else { delivery = Delivery.None; continue; }
                    break;
                case Delivery.Tag:
                    if (deliveryIndex == 0 && sink is IXmlDomBatchSink batch && CompleteTag())
                    {
                        delivery = emptyTag ? Delivery.Close : Delivery.None;
                        deliveryIndex = 2 * attributeCount + 1;
                        keepGoing = batch.CompleteTag(deliveredName, new(this), emptyTag, opening);
                        Array.Clear(attributes, 0, attributeCount);
                    }
                    else if (deliveryIndex == 0)
                    {
                        deliveryIndex++;
                        keepGoing = sink is IXmlOwnedNameSink owned
                            ? owned.StartOwnedElement(deliveredName.Local, deliveredName.Uri, deliveredName.Prefix, emptyTag, opening)
                            : sink.StartElement(deliveredName.View, emptyTag, opening);
                    }
                    else if (deliveryIndex <= attributeCount)
                    {
                        ref var a = ref attributes[deliveryIndex++ - 1];
                        if (!a.Namespace) continue;
                        keepGoing = sink.NamespaceDeclaration(new(a.Prefix, a.Uri, a.Location));
                    }
                    else if (deliveryIndex <= 2 * attributeCount)
                    {
                        ref var a = ref attributes[deliveryIndex++ - attributeCount - 1];
                        if (a.Namespace) continue;
                        keepGoing = a.Incomplete is { } incomplete
                            ? ((IXmlIncompleteAttributeSink)sink).IncompleteAttribute(new(a.Name.View, incomplete.LexicalValue,
                                incomplete.IsTokenized, incomplete.ValueLocation, incomplete.Parts, incomplete.DefaultSequence, a.IsDefaulted))
                            : sink is IXmlOwnedNameSink owned
                                ? owned.OwnedAttribute(a.Name.Local, a.Name.Uri, a.Name.Prefix, AttributeValue(a), a.OwnedValue, a.Location, a.IsDefaulted)
                                : sink.Attribute(new(a.Name.View, AttributeValue(a), a.Location, a.IsDefaulted));
                    }
                    else
                    {
                        delivery = emptyTag ? Delivery.Close : Delivery.None;
                        Array.Clear(attributes, 0, attributeCount);
                        keepGoing = sink.StartElementContent();
                    }
                    break;
                case Delivery.Close:
                    var closingName = deliveredName;
                    delivery = Delivery.None; PopFrame(); AdvanceRecoveryClose(); keepGoing = sink.EndElement(closingName.View, opening); break;
                case Delivery.UnresolvedReference:
                    delivery = Delivery.None;
                    if (sink is not IXmlUnresolvedReferenceSink unresolvedSink)
                    {
                        Reject("XML.UnresolvedReferenceSinkRequired", "Sink cannot represent unresolved entity references.", referenceLocation);
                        ClearUnresolvedReference();
                        continue;
                    }
                    // Mark delivery before invoking the sink: pauses, cancellation and
                    // callback faults cannot replay this accepted occurrence.
                    unresolvedPending = false;
                    try
                    {
                        keepGoing = unresolvedSink.UnresolvedReference(new(token.AsSpan(1, unresolvedNameLength),
                            token.AsSpan(0, unresolvedNameLength + 2), referenceLocation));
                    }
                    finally { ClearUnresolvedReference(); }
                    break;
                case Delivery.IncompleteDefaults:
                    if (incompleteDefaultIndex == incompleteDefaults!.Count)
                    { delivery = Delivery.None; continue; }
                    var declaration = incompleteDefaults[incompleteDefaultIndex++];
                    keepGoing = ((IXmlIncompleteAttributeSink)sink).IncompleteAttributeDefault(new(declaration.ElementName,
                        declaration.AttributeName, declaration.Interpretation.LexicalValue, declaration.Interpretation.IsTokenized,
                        declaration.IsExternal, declaration.Location, declaration.Interpretation.ValueLocation,
                        declaration.Interpretation.Parts, declaration.Interpretation.DefaultSequence!.Value));
                    if (incompleteDefaultIndex == incompleteDefaults.Count) delivery = Delivery.None;
                    break;
                case Delivery.DocumentType:
                    if (!PreflightIncompleteDefaults(sink)) { delivery = Delivery.None; continue; }
                    delivery = incompleteDefaults is { Count: > 0 } ? Delivery.IncompleteDefaults : Delivery.None;
                    keepGoing = sink is not IXmlDtdSink dtdSink || dtdSink.DocumentType(documentType!, entities!.Model, doctypeLocation);
                    break;
                case Delivery.Declaration:
                    delivery = Delivery.None; keepGoing = sink.Declaration(new(token.AsSpan(versionStart, versionLength), token.AsSpan(encodingStart, encodingLength), standalone, opening)); break;
                default:
                    delivery = Delivery.None; endDocumentDelivered = true;
                    if (!budget.Options.Recover && validationInstanceDiagnostic is { } witness && sink is IXmlValidationProvenanceSink provenance)
                        provenance.ValidationProvenance(witness, validationInstanceDeclaration!);
                    keepGoing = sink.EndDocument(opening); break;
            }
            if (!keepGoing) return false;
        }
        return true;
    }
    private static bool White(int c) => c is ' ' or '\t' or '\n' or '\r';
    private static bool NameStart(int c) => c is ':' or '_' || c is >= 'A' and <= 'Z' || c is >= 'a' and <= 'z' ||
        c is >= 0xC0 and <= 0xD6 || c is >= 0xD8 and <= 0xF6 || c is >= 0xF8 and <= 0x2FF || c is >= 0x370 and <= 0x37D ||
        c is >= 0x37F and <= 0x1FFF || c is >= 0x200C and <= 0x200D || c is >= 0x2070 and <= 0x218F ||
        c is >= 0x2C00 and <= 0x2FEF || c is >= 0x3001 and <= 0xD7FF || c is >= 0xF900 and <= 0xFDCF ||
        c is >= 0xFDF0 and <= 0xFFFD || c is >= 0x10000 and <= 0xEFFFF;
    private static bool NameChar(int c) => NameStart(c) || c is '-' or '.' or 0xB7 || c is >= '0' and <= '9' || c is >= 0x300 and <= 0x36F || c is >= 0x203F and <= 0x2040;
    private bool ValidName(ReadOnlySpan<char> name, bool qname)
    {
        if (name.IsEmpty) return false;
        int colons = 0, work = 0; bool first = true;
        // ASCII names dominate XML. Avoid Rune enumeration while retaining the
        // full XML Name production for non-ASCII and supplementary scalars.
        int ascii = 0;
        for (; ascii < name.Length && name[ascii] < 0x80; ascii++)
        {
            Work(work++); int c = name[ascii];
            bool valid = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_' or ':' ||
                !first && c is >= '0' and <= '9' or '-' or '.';
            if (!valid) return false;
            if (c == ':') { if (!qname || first || ++colons > 1) return false; first = true; }
            else first = false;
        }
        foreach (var rune in name[ascii..].EnumerateRunes())
        {
            Work(work++); int c = rune.Value;
            if ((first ? !NameStart(c) : !NameChar(c))) return false;
            if (c == ':') { if (!qname || first || ++colons > 1) return false; first = true; }
            else first = false;
        }
        return !first;
    }
    private string Intern(ReadOnlySpan<char> value, XmlLocation location)
    {
        if (value.IsEmpty) return "";
        activeCancellation.ThrowIfCancellationRequested();
        // One bounded probe for short repeated symbols. Collisions only evict
        // this accelerator: full ordinal equality is mandatory, and the actual
        // intern table retains randomized hashing and quota enforcement.
        int bucket = value.Length <= 64 ? (value.Length * 17 + value[0]) & 63 : -1;
        if (bucket >= 0 && recentInterns?[bucket] is { } cached && value.SequenceEqual(cached)) return cached;
        // Ordinal's alternate lookup hashes borrowed UTF-16 directly and uses
        // the runtime dictionary collision defense. Only a miss owns a string.
        var lookup = names.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(value, out string? existing))
        {
            if (bucket >= 0) (recentInterns ??= new string?[64])[bucket] = existing;
            return existing;
        }
        if (!Charge(Quota.InternedNames, 1, location)) return "";
        string owned = new(value); names.Add(owned, owned);
        if (bucket >= 0) (recentInterns ??= new string?[64])[bucket] = owned;
        return owned;
    }
    private int Colon(ReadOnlySpan<char> value)
    {
        for (int i = 0; i < value.Length;)
        {
            activeCancellation.ThrowIfCancellationRequested();
            int count = Math.Min(4096, value.Length - i);
            int found = value.Slice(i, count).IndexOf(':');
            if (found >= 0) return i + found;
            i += count;
        }
        return -1;
    }
    private bool Equal(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length;)
        {
            activeCancellation.ThrowIfCancellationRequested();
            int count = Math.Min(4096, left.Length - i);
            if (!left.Slice(i, count).SequenceEqual(right.Slice(i, count))) return false;
            i += count;
        }
        return true;
    }
    private void Work(int i) { if ((i & 255) == 0) activeCancellation.ThrowIfCancellationRequested(); }
    private Name Resolve(ReadOnlySpan<char> qname, bool attribute, XmlLocation location)
    {
        int colon = Colon(qname);
        var prefix = colon < 0 ? ReadOnlySpan<char>.Empty : qname[..colon];
        var local = colon < 0 ? qname : qname[(colon + 1)..];
        string uri = "";
        if (prefix.SequenceEqual("xmlns")) Reject("XML.Namespace", "Reserved xmlns prefix.", location);
        else if (prefix.SequenceEqual("xml")) uri = XmlUri;
        else if (!prefix.IsEmpty || !attribute)
        {
            bool found = false;
            for (int i = bindings.Count - 1; i >= 0; i--)
            {
                Work(i);
                if (Equal(prefix, bindings[i].Prefix)) { uri = bindings[i].Uri; found = true; break; }
            }
            if (!prefix.IsEmpty && (!found || uri.Length == 0)) Reject("XML.Namespace", "Unbound namespace prefix.", location);
        }
        return new(Intern(local, location), uri, Intern(prefix, location));
    }
    private int ReadName(ref int i, out bool valid)
    {
        int start = i, colons = 0; bool first = true, ascii = true;
        valid = true;
        while (i < tokenLength && !White(token[i]) && token[i] is not '=' and not '/' and not '?')
        {
            Work(i); char c = token[i++];
            if (c >= 0x80) { ascii = false; continue; }
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_' or ':' || !first && c is >= '0' and <= '9' or '-' or '.')) valid = false;
            if (c == ':') { if (first || ++colons > 1) valid = false; first = true; }
            else first = false;
        }
        int length = i - start;
        valid = ascii ? valid && !first && length != 0 : ValidName(token.AsSpan(start, length), true);
        return length;
    }
    private void SkipWhite(ref int i) { while (i < tokenLength && White(token[i])) { Work(i); i++; } }
    private bool ReadPlainDtdAttribute(ref int index, ref AttributeToken attribute, char delimiter)
    {
        // Only primary literal values with no expansion/normalization trigger
        // need this path. General entity values and unknown provenance retain
        // the full interpretation/parts graph.
        if (entitySources is { Count: > 0 }) return false;
        int start = index, end = start;
        while (end < tokenLength)
        {
            activeCancellation.ThrowIfCancellationRequested();
            var block = token.AsSpan(end, Math.Min(4096, tokenLength - end));
            int stop = block.IndexOfAny(delimiter == '"' ? DoubleQuotedStops : SingleQuotedStops);
            if (stop < 0) { end += block.Length; continue; }
            end += stop;
            if (token[end] != delimiter) return false;
            attribute.ValueLength = end - start;
            var declaration = AttributeDeclaration(AttributeName(attribute));
            if (declaration is not null && declaration.Type != DtdAttributeType.CData)
                attribute.OwnedValue = NormalizeKnownAttribute(new string(token.AsSpan(start, attribute.ValueLength)), declaration,
                    attribute.ValueLength == 0 ? TokenLocation(end) : TokenLocation(start));
            index = end + 1;
            return true;
        }
        return false;
    }
    private bool ReadAttribute(ref int i, out AttributeToken a, bool declaration = false)
    {
        a = default; a.NameStart = i; a.Location = i < tokenLength ? TokenLocation(i) : opening;
        a.NameLength = ReadName(ref i, out bool validName);
        if (!validName) { Reject("XML.Syntax", "Invalid attribute name.", a.Location); return false; }
        SkipWhite(ref i);
        if (i >= tokenLength || token[i++] != '=') { Reject("XML.Syntax", "Expected attribute equals.", a.Location); return false; }
        SkipWhite(ref i);
        if (i >= tokenLength || token[i] is not ('\'' or '"')) { Reject("XML.Syntax", "Expected quoted attribute value.", a.Location); return false; }
        char delimiter = token[i++]; a.ValueStart = i;
        if (!declaration && entities is not null)
        {
            if (ReadPlainDtdAttribute(ref i, ref a, delimiter)) return diagnostic is null;
            return ReadDtdAttribute(ref i, ref a, delimiter);
        }
        int write = i;
        while (i < tokenLength && token[i] != delimiter)
        {
            activeCancellation.ThrowIfCancellationRequested();
            var remaining = token.AsSpan(i, Math.Min(4096, tokenLength - i));
            int ordinary = remaining.IndexOfAny(delimiter == '"' ? DoubleQuotedStops : SingleQuotedStops);
            if (ordinary < 0) ordinary = remaining.Length;
            if (ordinary != 0)
            {
                if (write != i) remaining[..ordinary].CopyTo(token.AsSpan(write));
                i += ordinary; write += ordinary;
                continue;
            }
            char c = token[i];
            if (c == '<') { Reject("XML.Syntax", "Literal less-than in attribute.", TokenLocation(i)); return false; }
            if (c == '&')
            {
                if (declaration) { Reject("XML.Syntax", "References in declaration.", TokenLocation(i)); return false; }
                var refLocation = TokenLocation(i++); int start = i;
                while (i < tokenLength && token[i] != ';' && token[i] != delimiter) { Work(i); i++; }
                if (i >= tokenLength || token[i] != ';') { Reject("XML.Syntax", "Unfinished reference.", refLocation); return false; }
                int scalar = Reference(token.AsSpan(start, i++ - start), refLocation);
                if (diagnostic is not null) return false;
                if (scalar < 0x10000) token[write++] = (char)scalar;
                else { scalar -= 0x10000; token[write++] = (char)(0xD800 + (scalar >> 10)); token[write++] = (char)(0xDC00 + (scalar & 1023)); }
            }
            else { token[write++] = White(c) ? ' ' : c; i++; }
        }
        if (i >= tokenLength) { Reject("XML.Syntax", "Unfinished attribute.", a.Location); return false; }
        i++; a.ValueLength = write - a.ValueStart;
        return true;
    }
    private void ParseTag()
    {
        PrepareRecoveryTag();
        if (rootClosed && !RecoveryEnabled) { Reject("XML.Syntax", "Multiple root elements.", opening); return; }
        int i = 0; int nameLength = ReadName(ref i, out bool validName);
        if (!validName) { Reject("XML.Syntax", "Invalid element name.", opening); return; }
        dtdAttributes = entities?.GetAttributes(token.AsSpan(0, nameLength));
        attributeCount = 0; emptyTag = false; budget.Reset(Quota.AttributesPerElement);
        while (i < tokenLength)
        {
            bool space = White(token[i]); SkipWhite(ref i);
            if (i == tokenLength) break;
            if (token[i] == '/' && i + 1 == tokenLength) { emptyTag = true; i++; break; }
            if (!space) { Reject("XML.Syntax", "Attributes require separating whitespace.", TokenLocation(i)); return; }
            if (!Charge(Quota.AttributesPerElement, 1, TokenLocation(i)) || !ReadAttribute(ref i, out var a)) return;
            if (attributeCount == attributes.Length)
            {
                if (attributeCount == int.MaxValue) { Reject("XML.ResourceLimit", "Attribute capacity exceeded.", a.Location); return; }
                Array.Resize(ref attributes, (int)Math.Min(int.MaxValue, (long)attributes.Length * 2));
            }
            attributes[attributeCount++] = a;
        }
        if (entities is not null && !ApplyDefaultAttributes(token.AsSpan(0, nameLength))) return;
        int bindingStart = bindings.Count;
        for (int n = 0; n < attributeCount; n++)
        {
            Work(n); ref var a = ref attributes[n]; var q = AttributeName(a);
            if (q.SequenceEqual("xmlns") || q.StartsWith("xmlns:"))
            {
                if (a.Incomplete is { } incomplete)
                { Reject("XML.UnresolvedNamespaceValue", "Namespace value contains unresolved entity references.", FirstUnknown(incomplete.Parts)); return; }
                a.Namespace = true; a.Prefix = Intern(q.SequenceEqual("xmlns") ? ReadOnlySpan<char>.Empty : q[6..], a.Location);
                a.Uri = Intern(AttributeValue(a), a.Location);
                if (diagnostic is not null) return;
                if (a.Prefix == "xmlns" || a.Uri == XmlnsUri || (a.Prefix == "xml") != (a.Uri == XmlUri) || (a.Prefix.Length != 0 && a.Uri.Length == 0))
                { Reject("XML.Namespace", "Reserved namespace binding.", a.Location); return; }
                for (int b = bindingStart; b < bindings.Count; b++)
                { Work(b); if (ReferenceEquals(bindings[b].Prefix, a.Prefix)) { Reject("XML.Namespace", "Duplicate namespace declaration.", a.Location); return; } }
                bindings.Add(new(a.Prefix, a.Uri));
            }
        }
        deliveredName = Resolve(token.AsSpan(0, nameLength), false, opening);
        for (int n = 0; n < attributeCount; n++)
        {
            Work(n); ref var a = ref attributes[n]; if (a.Namespace) continue;
            a.Name = Resolve(AttributeName(a), true, a.Location);
            for (int p = 0; p < n; p++)
            {
                Work(p); ref var previous = ref attributes[p];
                if (!previous.Namespace && ReferenceEquals(previous.Name.Local, a.Name.Local) && ReferenceEquals(previous.Name.Uri, a.Name.Uri))
                { if (!RecoverDuplicateAttribute(n, a.Location)) { Reject("XML.Namespace", "Duplicate expanded attribute name.", a.Location); return; } break; }
            }
        }
        if (diagnostic is not null) return;
        for (int n = 0; n < attributeCount; n++)
            if (attributes[n].Incomplete is { } incomplete && sinkIdentity is not IXmlIncompleteAttributeSink)
            { Reject("XML.IncompleteAttributeSinkRequired", "Sink cannot represent incomplete attribute values.", FirstUnknown(incomplete.Parts)); return; }
        if (!FinishRecoveryTag()) return;
        if (!Charge(Quota.Depth, 1, opening)) return;
        var validationDeclaration = ValidationElementDeclaration(token.AsSpan(0, nameLength));
        if (diagnostic is not null) return;
        frames.Add(new(deliveredName, bindingStart, StandaloneElementWhitespace(token.AsSpan(0, nameLength)), validationDeclaration)); rootSeen = true; deliveryIndex = 0; delivery = Delivery.Tag;
    }
    private void ParseClose()
    {
        int i = 0; int length = ReadName(ref i, out bool validName); SkipWhite(ref i);
        if (i != tokenLength || !validName)
        { Reject("XML.Syntax", "Invalid end tag.", opening); return; }
        if (entitySources is { Count: > 0 } && frames.Count <= entitySources[^1].ElementDepth)
        { Reject("XML.Syntax", "Entity cannot close an element from its parent source.", opening); return; }
        if (RecoverClosingTag(length)) return;
        if (frames.Count == 0) { Reject("XML.Syntax", "Invalid end tag.", opening); return; }
        var expected = frames[^1].Name; var q = token.AsSpan(0, length);
        int prefixLength = expected.Prefix.Length;
        bool equal = prefixLength == 0 ? Equal(q, expected.Local) :
            q.Length == prefixLength + 1 + expected.Local.Length && q[prefixLength] == ':' &&
            Equal(q[..prefixLength], expected.Prefix) && Equal(q[(prefixLength + 1)..], expected.Local);
        if (!equal) { Reject("XML.Syntax", "Mismatched end tag.", opening); return; }
        deliveredName = expected; delivery = Delivery.Close;
    }
    private void PopFrame()
    {
        int first = frames[^1].BindingsStart;
        bindings.RemoveRange(first, bindings.Count - first); frames.RemoveAt(frames.Count - 1); budget.Release(Quota.Depth, 1);
        if (frames.Count == 0) rootClosed = true;
    }
    private int Reference(ReadOnlySpan<char> reference, XmlLocation location)
    {
        int value = ScalarReferenceValue(reference, location);
        if (diagnostic is not null || !Charge(Quota.EntityDepth, 1, location)) return 0;
        try { Charge(Quota.ExpandedCharacters, 1, location); }
        finally { budget.Release(Quota.EntityDepth, 1); }
        return value;
    }

    private int ScalarReferenceValue(ReadOnlySpan<char> reference, XmlLocation location)
    {
        int value;
        if (reference.SequenceEqual("lt")) value = '<';
        else if (reference.SequenceEqual("gt")) value = '>';
        else if (reference.SequenceEqual("amp")) value = '&';
        else if (reference.SequenceEqual("apos")) value = '\'';
        else if (reference.SequenceEqual("quot")) value = '"';
        else if (!reference.IsEmpty && reference[0] == '#')
        {
            int i = 1, radix = 10; value = 0;
            if (i < reference.Length && reference[i] == 'x') { radix = 16; i++; }
            if (i == reference.Length) { Reject("XML.Syntax", "Empty numeric reference.", location); return 0; }
            for (; i < reference.Length; i++)
            {
                Work(i); int c = reference[i]; int digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0 || digit >= radix || value > (0x10FFFF - digit) / radix) { Reject("XML.Syntax", "Invalid numeric reference.", location); return 0; }
                value = value * radix + digit;
            }
            if (!XmlScalar(value)) { Reject("XML.Syntax", "Disallowed reference scalar.", location); return 0; }
        }
        else
        {
            Reject(ValidName(reference, false) ? "XML.UndeclaredEntity" : "XML.Syntax", "Invalid or undeclared entity.", location); return 0;
        }
        return value;
    }
    private void ParseDeclaration()
    {
        int i = 3, count = 0; versionStart = versionLength = encodingStart = encodingLength = 0; standalone = null;
        while (i < tokenLength)
        {
            if (!White(token[i])) { Reject("XML.Syntax", "Declaration requires whitespace.", opening); return; }
            SkipWhite(ref i); if (i == tokenLength) break;
            if (!ReadAttribute(ref i, out var a, declaration: true)) return;
            var name = token.AsSpan(a.NameStart, a.NameLength); var value = token.AsSpan(a.ValueStart, a.ValueLength);
            if (count == 0 && name.SequenceEqual("version") && VersionNumber(value)) { versionStart = a.ValueStart; versionLength = a.ValueLength; }
            else if (count == 1 && name.SequenceEqual("encoding") && EncodingName(value)) { encodingStart = a.ValueStart; encodingLength = a.ValueLength; }
            else if ((count == 1 || count == 2 && encodingLength != 0) && standalone is null && name.SequenceEqual("standalone") && (value.SequenceEqual("yes") || value.SequenceEqual("no"))) standalone = value.SequenceEqual("yes");
            else { Reject("XML.Syntax", "Invalid declaration order or value.", a.Location); return; }
            count++;
        }
        if (versionLength == 0) { Reject("XML.Syntax", "Missing XML version.", opening); return; }
        declarationSeen = true; declarationAllowed = false; delivery = Delivery.Declaration;
    }
    private bool VersionNumber(ReadOnlySpan<char> value)
    {
        if (value.Length < 3 || !value.StartsWith("1.")) return false;
        for (int i = 2; i < value.Length; i++)
        {
            Work(i);
            if (value[i] is < '0' or > '9') return false;
        }
        return true;
    }
    private bool EncodingName(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || !(value[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z')) return false;
        int work = 0;
        foreach (char c in value) { Work(work++); if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')) return false; }
        return true;
    }
}

// Opt-in only: these sinks consume a borrowed text run before returning and do
// not rely on the scalar parser's fixed-size fragment/callback boundaries.
// This private boundary passes immutable parser-owned names without copying
// them back through borrowed views. General callbacks retain their contract.
// Only the private DOM builder can use this non-pausing complete-tag boundary.
// Attributes borrow parser scratch until this synchronous call returns.
internal interface IXmlDomBatchSink : IXmlBulkTextSink
{
    bool CompleteTag(XmlParserCore.Name name, XmlParserCore.DomTagView attributes, bool empty, XmlLocation location);
    bool NormalizedTextRun(ReadOnlySpan<char> raw, bool ended, XmlLocation location);
}
internal interface IXmlOwnedNameSink
{
    bool StartOwnedElement(string local, string uri, string prefix, bool empty, XmlLocation location);
    bool OwnedAttribute(string local, string uri, string prefix, ReadOnlySpan<char> value, string? ownedValue, XmlLocation location, bool defaulted);
}
internal interface IXmlBulkTextSink : IXmlEventSink
{
    void PrepareInput(ReadOnlySpan<char> input) { }
}
