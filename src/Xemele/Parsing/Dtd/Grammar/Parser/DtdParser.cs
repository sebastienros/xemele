using System.Runtime.ExceptionServices;
using System.Text;
namespace Xemele.Parsing.Dtd;

internal enum DtdParseStep { NeedInput, NeedEntity, Completed, Rejected }
internal readonly record struct DtdEntityRequest(string Name, string? SystemId, string? PublicId, XmlLocation Location);
internal readonly record struct DtdParseResult(int Consumed, DtdParseStep Step, DtdEntityRequest? Entity, XmlDiagnostic? Diagnostic);

// Only owned declaration tokens, model output, and source cursors survive a call.
// Comments/PI data/ignored sections/whitespace are scanned without accumulation.
internal sealed partial class DtdParser : IDisposable
{
    private enum Lex { Between, Open, Bang, Keyword, Declaration, Name, Literal, CommentStart, Comment, CommentDash, CommentEnd, PiTarget, Pi, PiQuestion, Conditional, ConditionalEnd, Ignore, Parameter }
    private sealed class Source
    {
        internal readonly int Id;
        // DeclSep replacement must be independently complete; in-declaration
        // and conditional-header insertions may continue in the resumed source.
        internal Lex Insertion;
        internal int IncludeFloor;
        internal readonly bool External, Entered, LiteralExpansion;
        internal readonly string? Name, Text;
        internal int Index, Mode, Line, Column, High;
        internal long LastOffset = -1;
        internal long? Eof;
        internal XmlLocation HighAt;
        internal Uri? Uri;
        internal bool Final, Cr;
        internal XmlTextDeclarationFramer? Prolog;
        internal DtdLiteralInfo? LiteralInfo;
        internal readonly object PhysicalIdentity = new();
        internal readonly List<(int Scalar, XmlLocation At)> Prefix = new(7);
        internal Source(int id, bool external, XmlLocation at, string? name = null, string? text = null, bool entered = false, bool literal = false)
        { Id = id; External = external; Line = at.LineNumber; Column = at.LinePosition; Uri = at.SourceUri; Name = name; Text = text; Entered = entered; LiteralExpansion = literal; }
        internal XmlLocation At(long? offset = null) => new(Uri, Line, Column, offset);
        internal bool Advance(int c)
        {
            if (Line == 0) { Cr = c == '\r'; return true; }
            if (c == '\n' && Cr) { Cr = false; return true; }
            if (c is '\r' or '\n') { if (Line == int.MaxValue) return false; Line++; Column = 1; }
            else { if (Column == int.MaxValue) return false; Column++; }
            Cr = c == '\r'; return true;
        }
    }
    private readonly XmlResourceLimits limits;
    private readonly IDtdBudget budget;
    private readonly bool shared;
    private readonly List<Source> sources = [];
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    private HashSet<string> names = new(StringComparer.Ordinal);
    private List<DtdElementDeclaration> elements = [];
    private List<DtdAttributeDeclaration> attributes = [];
    private List<DtdEntityDeclaration> general = [], parameter = [];
    private HashSet<string>? parameterOriginGeneral;
    private List<DtdNotationDeclaration> notations = [];
    private readonly List<Token> tokens = [];
    private readonly StringBuilder scratch = new(), reference = new();
    private readonly Stack<int> includes = new();
    private Lex lex, parameterResume;
    private string declaration = "";
    private char quote;
    private int nextId = 1, lexicalSource, quoteSource, ignoreDepth, ignoreRun, gate;
    private long documentScalars, tokenScalars, declarationScalars;
    private bool lexicalEntered, declarationInParameter, piData, declarationExternal, separator, tokenSpace, literalEntity, disposed, faulted, terminal;
    private XmlLocation declarationAt, tokenAt, parameterAt, lexicalAt, tokenPhysicalAt;
    private DtdEntityRequest? request;
    private Source? pending;
    private XmlDiagnostic? diagnostic;
    private XmlDiagnostic? declarationValidityDiagnostic;
    private DtdModel? model;
    private readonly record struct Token(string Text, bool Literal, bool Space, XmlLocation At, int Source,
        DtdLiteralInfo? LiteralInfo = null, XmlLocation PhysicalAt = default);
    private readonly StringBuilder physicalLiteral = new();
    private readonly List<XmlLocation> literalLocations = [];
    private readonly List<bool> literalOrigins = [];
    private DtdLiteralInfo? lastLiteral;
    private XmlLocation literalValueAt, physicalDtdLocation;
    private long physicalLiteralScalars, physicalStreamScalars;
    private readonly StringBuilder literalPhysicalStream = new();
    private readonly List<int> literalPhysicalStarts = [], literalPhysicalLengths = [];
    private string? physicalDtdSpelling;
    private int physicalDtdSpellingStart, physicalDtdSpellingLength, physicalDtdScalar;
    private object literalPhysicalIdentity = new();
    private object? physicalDtdIdentity;
    private readonly List<object> literalPhysicalIdentities = [];
    private void CapturePhysicalLiteral(int scalar)
    {
        if (!(lex == Lex.Literal || lex == Lex.Parameter && parameterResume == Lex.Literal) || IgnoreCurrentDeclaration || Current.Id != quoteSource || scalar == quote) return;
        Span<char> pair = stackalloc char[2]; int width = ValueSyntax.WriteScalar(scalar, pair);
        if (physicalLiteralScalars == int.MaxValue || physicalLiteral.Length > int.MaxValue - width || limits.TokenCharacters is int max && physicalLiteralScalars >= max)
        { Refuse(ValueBudget.Limit(Current.At())); return; }
        physicalLiteralScalars++; physicalLiteral.Append(pair[..width]);
    }

    public DtdParser(DtdSubsetKind kind, XmlResourceLimits limits, Uri? sourceUri = null) : this(kind, limits, new XmlLocation(sourceUri, 1, 1, null)) { }
    public DtdParser(DtdSubsetKind kind, XmlResourceLimits limits, XmlLocation initialLocation)
    {
        ArgumentNullException.ThrowIfNull(limits); limits.Validate();
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (initialLocation.SourceUri is { IsAbsoluteUri: false } || initialLocation.ByteOffset < 0 ||
            !((initialLocation.LineNumber > 0 && initialLocation.LinePosition > 0) || (initialLocation.LineNumber == 0 && initialLocation.LinePosition == 0)))
            throw new ArgumentException("Invalid initial source location.", nameof(initialLocation));
        this.limits = limits with { }; budget = new LocalBudget(this.limits);
        var source = new Source(nextId++, kind == DtdSubsetKind.External, initialLocation);
        if (source.External) source.Prolog = MakeFramer(source.Uri);
        // An explicit initial byte location is also the lower bound on supplied absolute mappings.
        source.LastOffset = initialLocation.ByteOffset ?? -1;
        sources.Add(source);
    }
    internal DtdParser(DtdSubsetKind kind, XmlResourceLimits limits, IDtdBudget budget, XmlLocation initialLocation) : this(kind, limits, initialLocation)
    { ArgumentNullException.ThrowIfNull(budget); this.budget = budget; shared = true; }
    private XmlTextDeclarationFramer MakeFramer(Uri? uri) => new(limits with { ExpandedCharacters = null }, uri);
    public DtdModel Model
    { get { ObjectDisposedException.ThrowIf(disposed, this); if (model is null || faulted) throw new InvalidOperationException("The initial subset is not complete."); return model; } }
    internal bool HasParameterReferences { get; private set; }
    internal XmlLocation? FirstUnreadParameter { get; private set; }
    // Retained prior-model evidence is separate from live declaration-reading controls.
    private XmlLocation? priorUnreadParameterLocation;
    private bool nonvalidatingReadState, standaloneReadState, externalSubsetReadState;
    private XmlLocation? undeclaredDefault;

    internal void ConfigureDeclarationReading(bool standalone, XmlLocation? firstUnread = null, bool hasExternalSubset = false)
    {
        Check();
        if (appendStarted) throw new InvalidOperationException("Configure declaration reading before input.");
        nonvalidatingReadState = true; standaloneReadState = standalone; externalSubsetReadState = hasExternalSubset;
        FirstUnreadParameter = firstUnread;
    }

    private bool IgnoreCurrentDeclaration => SuppressDeclarations && declaration is "ENTITY" or "ATTLIST";
    private bool SuppressDeclarations => nonvalidatingReadState && !standaloneReadState && FirstUnreadParameter is not null;

    private Source Current => sources[^1];
    private void Enter()
    { if (Interlocked.CompareExchange(ref gate, 1, 0) != 0) throw new InvalidOperationException("Parser is active."); }
    private void Check()
    { ObjectDisposedException.ThrowIf(disposed, this); if (terminal || faulted) throw new InvalidOperationException("Parser is terminal or faulted."); }

    public DtdParseResult Append(ReadOnlySpan<char> input, ReadOnlySpan<long> byteOffsets, bool isFinalBlock, CancellationToken cancellationToken)
    {
        Enter(); int consumed = 0;
        try
        {
            Check(); grammarCancellation = cancellationToken; appendStarted = true; if (pending is not null) throw new InvalidOperationException("Supply the requested entity before appending.");
            cancellationToken.ThrowIfCancellationRequested();
            int submittedIndex = sources.Count - 1; Source submitted = sources[submittedIndex];
            // Walk stable indices rather than searching the stack at every level.
            while (submitted.Text is not null) { cancellationToken.ThrowIfCancellationRequested(); submitted = sources[--submittedIndex]; }
            Validate(input, byteOffsets, submitted, cancellationToken);
            if (submitted.Final && !isFinalBlock) throw new InvalidOperationException("Final input is sticky for its source.");
            if (isFinalBlock && (submitted.Mode == 2 || !byteOffsets.IsEmpty) && submitted.Eof is null)
                throw new InvalidOperationException("Byte source EOF must be supplied before final input.");
            submitted.Final |= isFinalBlock;
            if (!input.IsEmpty && submitted.Mode == 0) submitted.Mode = byteOffsets.IsEmpty ? 1 : 2;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (!terminal && pending is null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Source s = Current;
                    bool owned = s.Text is not null;
                    ReadOnlySpan<char> available = owned ? s.Text.AsSpan(s.Index) : input[consumed..];
                    if (available.IsEmpty)
                    {
                        if (owned || s == submitted && isFinalBlock)
                        {
                            FinishSource(s, cancellationToken);
                            if (terminal || pending is not null) break;
                            if (!owned) return new(consumed, DtdParseStep.NeedInput, null, null);
                            continue;
                        }
                        break;
                    }
                    char c = available[0]; long? offset = !owned && submitted.Mode == 2 ? byteOffsets[consumed] : null;
                    XmlLocation at = s.High != 0 ? s.HighAt : s.At(offset);
                    physicalDtdLocation = owned && s.LiteralInfo is { } info ? info.Locations[s.Index] : at;
                    physicalDtdSpelling = owned ? s.LiteralInfo?.PhysicalStream : null;
                    physicalDtdIdentity = owned ? s.LiteralInfo?.PhysicalIdentities?[s.Index] : null;
                    physicalDtdSpellingStart = physicalDtdSpelling is not null ? s.LiteralInfo!.PhysicalStarts![s.Index] : 0;
                    physicalDtdSpellingLength = physicalDtdSpelling is not null ? s.LiteralInfo!.PhysicalLengths![s.Index] : 0;
                    if (s.High == 0 && char.IsHighSurrogate(c))
                    {
                        s.High = c; s.HighAt = at;
                        if (owned) s.Index++; else consumed++;
                        if (offset is long h) s.LastOffset = h;
                        continue;
                    }
                    int scalar = c;
                    if (s.High != 0)
                    {
                        if (!char.IsLowSurrogate(c)) { Reject("Unpaired high surrogate.", at); break; }
                        if (offset != s.HighAt.ByteOffset) throw new ArgumentException("Surrogate mappings differ.", nameof(byteOffsets));
                        scalar = char.ConvertToUtf32((char)s.High, c);
                    }
                    if (!ValueSyntax.IsXmlScalar(scalar)) { Reject("Invalid XML scalar.", at); break; }
                    if (!shared && s == sources[0])
                    {
                        if (documentScalars == long.MaxValue || limits.DocumentCharacters is long max && documentScalars >= max)
                        { Refuse(ValueBudget.Limit(at)); break; }
                        documentScalars++;
                    }
                    if ((s.External || s.Entered) && !Accept(budget.ChargeExpandedCharacters(1, at))) break;
                    s.High = 0;
                    if (owned) s.Index++; else consumed++;
                    if (offset is long b) s.LastOffset = b;
                    physicalDtdScalar = scalar;
                    CapturePhysicalLiteral(scalar);
                    bool skipLf = scalar == '\n' && s.Cr;
                    if (!s.Advance(scalar)) { Reject("Source location overflow.", at, "XML.LocationLimit"); break; }
                    if (skipLf && !owned && lex == Lex.Literal && !IgnoreCurrentDeclaration && literalPhysicalLengths.Count != 0)
                    {
                        if (!AppendPhysicalLiteral("\n", at)) break;
                        literalPhysicalLengths[^1]++;
                    }
                    if (s.Prolog is not null) ProcessProlog(s, scalar, at, cancellationToken);
                    else if (owned || !skipLf) Process(!owned && scalar == '\r' ? '\n' : scalar, at, cancellationToken);
                }
                if (terminal) { Drain(null); return new(consumed, model is null ? DtdParseStep.Rejected : DtdParseStep.Completed, null, diagnostic); }
                if (pending is not null) return new(consumed, DtdParseStep.NeedEntity, request, null);
                return new(consumed, DtdParseStep.NeedInput, null, null);
            }
            catch (Exception ex) { faulted = true; Drain(ex); throw; }
        }
        catch (OperationCanceledException ex) { faulted = true; Drain(ex); throw; }
        finally { Volatile.Write(ref gate, 0); }
    }
    private void Validate(ReadOnlySpan<char> input, ReadOnlySpan<long> offsets, Source s, CancellationToken cancellation)
    {
        if (!offsets.IsEmpty && offsets.Length != input.Length) throw new ArgumentException("Mappings must parallel input.", nameof(offsets));
        if (!input.IsEmpty && s.Mode != 0 && s.Mode != (offsets.IsEmpty ? 1 : 2)) throw new ArgumentException("Source mode cannot change.", nameof(offsets));
        long previous = s.LastOffset;
        for (int i = 0; i < offsets.Length; i++)
        {
            if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            long b = offsets[i];
            if (b < 0 || b < previous || s.Eof is long eof && b >= eof) throw new ArgumentException("Mappings must be monotonic within source bounds.", nameof(offsets));
            if (i > 0 && char.IsHighSurrogate(input[i - 1]) && char.IsLowSurrogate(input[i]) && b != offsets[i - 1]) throw new ArgumentException("Surrogate mappings differ.", nameof(offsets));
            previous = b;
        }
        if (s.High != 0 && !input.IsEmpty && char.IsLowSurrogate(input[0]) && !offsets.IsEmpty && offsets[0] != s.HighAt.ByteOffset) throw new ArgumentException("Surrogate mappings differ.", nameof(offsets));
    }
    public void CompleteByteSource(long byteLength)
    {
        Enter(); try
        {
            Check(); if (pending is not null) throw new InvalidOperationException("Entity has not been pushed.");
            Source s = Current;
            if (s.Text is not null || s.Mode == 1 || s.Final || s.Eof is not null) throw new InvalidOperationException("Source cannot be finalized as bytes.");
            if (byteLength < 0 || byteLength < s.LastOffset) throw new ArgumentOutOfRangeException(nameof(byteLength));
            s.Mode = 2; s.Eof = byteLength;
        } finally { Volatile.Write(ref gate, 0); }
    }
    public void PushEntity(Uri finalSourceUri)
    {
        Enter(); try
        {
            Check(); if (pending is null) throw new InvalidOperationException("No pending entity.");
            ArgumentNullException.ThrowIfNull(finalSourceUri);
            if (!finalSourceUri.IsAbsoluteUri) throw new ArgumentException("Final URI must be absolute.", nameof(finalSourceUri));
            pending.Uri = finalSourceUri; pending.Prolog = MakeFramer(finalSourceUri);
            sources.Add(pending); pending = null; request = null;
        } catch (Exception ex) { if (pending is null && request is not null) { faulted = true; Drain(ex); } throw; }
        finally { Volatile.Write(ref gate, 0); }
    }
    private void ProcessProlog(Source s, int scalar, XmlLocation at, CancellationToken cancellation)
    {
        Span<char> chars = stackalloc char[2]; int count = ValueSyntax.WriteScalar(scalar, chars);
        // Prefix ownership is bounded to recognition, cleared as soon as a declaration is known.
        if (s.Prefix.Count < 7) s.Prefix.Add((scalar, at));
        var r = s.Prolog!.Append(chars[..count], [], false, cancellation);
        if (r.Step == TextDeclarationStep.Rejected) { Refuse(r.Diagnostic! with { Location = at }); return; }
        if (r.Step == TextDeclarationStep.Declared) { s.Prolog.Dispose(); s.Prolog = null; s.Prefix.Clear(); return; }
        if (r.Step == TextDeclarationStep.Absent) ReplayPrefix(s, cancellation);
        else if (s.Prefix.Count == 6 && char.IsWhiteSpace((char)scalar)) s.Prefix.Clear();
    }
    private void ReplayPrefix(Source s, CancellationToken cancellation)
    {
        Span<char> output = stackalloc char[7]; s.Prolog!.DrainPrefix(output, []); s.Prolog.Dispose(); s.Prolog = null;
        bool cr = false;
        foreach (var (scalar, at) in s.Prefix)
        { if (!(scalar == '\n' && cr)) Process(scalar == '\r' ? '\n' : scalar, at, cancellation); cr = scalar == '\r'; if (terminal || pending is not null) break; }
        s.Prefix.Clear();
    }
    private void FinishSource(Source s, CancellationToken cancellation)
    {
        if (s.High != 0) { Reject("Incomplete scalar at source EOF.", s.HighAt); return; }
        if (s.Prolog is not null)
        {
            var r = s.Prolog.Append([], [], true, cancellation);
            if (r.Step == TextDeclarationStep.Rejected) { Refuse(r.Diagnostic! with { Location = s.At(s.Eof) }); return; }
            if (r.Step == TextDeclarationStep.Absent) ReplayPrefix(s, cancellation);
            else { s.Prolog.Dispose(); s.Prolog = null; s.Prefix.Clear(); }
        }
        if (terminal || pending is not null) return;
        if (s.Entered && !s.LiteralExpansion) Process(' ', s.At(s.Eof), cancellation);
        if (terminal || pending is not null) return;
        if (s.Entered)
        {
            // PE Between Declarations is a WFC. Proper declaration/group/
            // conditional nesting is validity-only for in-grammar insertions.
            if (s.Insertion == Lex.Between && (lex != Lex.Between || includes.Count != s.IncludeFloor || declaration.Length != 0) ||
                lexicalSource == s.Id && lex is Lex.Open or Lex.Bang or Lex.Keyword or Lex.CommentStart or Lex.Comment or Lex.CommentDash or Lex.CommentEnd or Lex.PiTarget or Lex.Pi or Lex.PiQuestion ||
                lex == Lex.Parameter || lex == Lex.Literal && quoteSource == s.Id)
            { Reject("Markup crosses an entity boundary.", s.At(s.Eof)); return; }
            sources.RemoveAt(sources.Count - 1); active.Remove(s.Name!); s.Prolog?.Dispose(); budget.ExitEntity();
            return;
        }
        if (lex != Lex.Between || includes.Count != 0 || declaration.Length != 0) { Reject("Incomplete DTD at EOF.", s.At(s.Eof)); return; }
        // Applicability depends on the entire internal subset. The missing live
        // declaration was recorded at the default, so later declarations cannot
        // repair its order; a later PE can make Entity Declared validity-only.
        if (!ValidateDirectDefaultOrder(cancellation)) return;
        if (undeclaredDefault is { } missing && !HasParameterReferences)
        { Reject("Entity must be declared before the attribute default.", missing, "DTD.UndeclaredEntity"); return; }
        if (!ValidateKnownDefaults(cancellation)) return;
        model = new(elements, attributes, general, parameter, notations, declarationValidityDiagnostic,
            priorUnreadParameterLocation ?? FirstUnreadParameter); terminal = true;
    }
    private bool Accept(XmlDiagnostic? refusal) { if (refusal is null) return true; Refuse(refusal); return false; }
    private void Refuse(XmlDiagnostic refusal) { diagnostic = refusal; terminal = true; }
    private void Reject(string message, XmlLocation at, string code = "DTD.Syntax") => Refuse(new(code, XmlDiagnosticSeverity.Fatal, message, at));
    private bool Bounded(long existing, XmlLocation at)
    { if (existing >= int.MaxValue || limits.TokenCharacters is int max && existing >= max) { Refuse(ValueBudget.Limit(at)); return false; } return true; }
    private void Drain(Exception? primary)
    {
        Exception? first = null;
        if (pending is not null) { var p = pending; pending = null; request = null; active.Remove(p.Name!); try { budget.ExitEntity(); } catch (Exception ex) { first = ex; } }
        while (sources.Count > 1)
        {
            var s = Current; sources.RemoveAt(sources.Count - 1); active.Remove(s.Name!);
            s.Prolog?.Dispose();
            try { budget.ExitEntity(); } catch (Exception ex) { first ??= ex; }
        }
        if (primary is null && first is not null) ExceptionDispatchInfo.Capture(first).Throw();
    }
    public void Dispose()
    {
        Enter(); try { if (disposed) return; disposed = true; Drain(null); }
        finally { foreach (var s in sources) s.Prolog?.Dispose(); sources.Clear(); scratch.Clear(); reference.Clear(); tokens.Clear(); ResetPhysicalLiteral(); physicalDtdSpelling = null; lastLiteral = null; parameterOriginGeneral?.Clear(); Volatile.Write(ref gate, 0); }
    }
    private sealed class LocalBudget(XmlResourceLimits limits) : IDtdBudget
    {
        private long expanded; private int depth, retained;
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation at)
        { if (count < 0 || count > long.MaxValue - expanded || limits.ExpandedCharacters is long max && count > max - expanded) return ValueBudget.Limit(at); expanded += count; return null; }
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation at)
        { if (retained == int.MaxValue || limits.InternedNames is int max && retained >= max) return ValueBudget.Limit(at); retained++; return null; }
        public XmlDiagnostic? EnterEntity(XmlLocation at)
        { if (depth == int.MaxValue || limits.EntityDepth is int max && depth >= max) return ValueBudget.Limit(at); depth++; return null; }
        public void ExitEntity() => depth--;
    }
}
