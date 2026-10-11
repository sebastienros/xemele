namespace Xemele.Parsing.Dtd;

internal sealed partial class DtdParser
{
    private bool appendStarted, seeded, initialFramed;

    private Source FreshExternal()
    {
        Check();
        if (appendStarted || sources.Count != 1 || !Current.External || Current.Mode != 0 || Current.Eof is not null)
            throw new InvalidOperationException("Initialization requires a fresh external subset.");
        return Current;
    }

    internal void SeedPriorModel(DtdModel model, CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            FreshExternal();
            if (!shared) throw new InvalidOperationException("Prior models require the same shared operation ledger.");
            if (seeded) throw new InvalidOperationException("Prior model was already seeded.");
            ArgumentNullException.ThrowIfNull(model);
            cancellationToken.ThrowIfCancellationRequested();
            // Stage all owning collections before publication. Cancellation/allocation
            // failure leaves the parser fresh and the shared operation ledger untouched.
            var newElements = new List<DtdElementDeclaration>();
            var newAttributes = new List<DtdAttributeDeclaration>();
            var newGeneral = new List<DtdEntityDeclaration>();
            var newParameter = new List<DtdEntityDeclaration>();
            var newNotations = new List<DtdNotationDeclaration>();
            var newNames = new HashSet<string>(StringComparer.Ordinal);
            var elementKeys = new HashSet<string>(StringComparer.Ordinal);
            var attributeKeys = new HashSet<(string, string)>();
            var generalKeys = new HashSet<string>(StringComparer.Ordinal);
            var parameterKeys = new HashSet<string>(StringComparer.Ordinal);
            var notationKeys = new HashSet<string>(StringComparer.Ordinal);
            var particles = new Stack<DtdParticle>();
            var newDeclarationValidityDiagnostic = model.DeclarationValidityDiagnostic;
            foreach (var e in model.Elements)
            {
                Remember(e.Name);
                if (elementKeys.Add(e.Name)) newElements.Add(e);
                else newDeclarationValidityDiagnostic ??= DuplicateElementDiagnostic(e.Location);
                particles.Push(e.Content);
                while (particles.Count != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var particle = particles.Pop();
                    if (particle.Name is not null) Remember(particle.Name);
                    foreach (var child in particle.Children)
                    { cancellationToken.ThrowIfCancellationRequested(); particles.Push(child); }
                }
            }
            foreach (var a in model.Attributes)
            {
                Remember(a.ElementName); Remember(a.Name);
                foreach (var token in a.Tokens) Remember(token);
                if (attributeKeys.Add((a.ElementName, a.Name))) newAttributes.Add(a);
            }
            foreach (var e in model.GeneralEntities)
            {
                Remember(e.Name);
                if (e.NotationName is not null) Remember(e.NotationName);
                if (generalKeys.Add(e.Name)) newGeneral.Add(e);
            }
            foreach (var e in model.ParameterEntities)
            { Remember(e.Name); if (parameterKeys.Add(e.Name)) newParameter.Add(e); }
            foreach (var n in model.Notations)
            {
                Remember(n.Name);
                if (notationKeys.Add(n.Name)) newNotations.Add(n);
                else newDeclarationValidityDiagnostic ??= DuplicateNotationDiagnostic(n.Location);
            }
            cancellationToken.ThrowIfCancellationRequested();
            elements = newElements; attributes = newAttributes; general = newGeneral;
            parameter = newParameter; notations = newNotations; names = newNames; seeded = true;
            declarationValidityDiagnostic = newDeclarationValidityDiagnostic;

            void Remember(string name)
            {
                for (int i = 0; i < name.Length; i++)
                    if ((i & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                newNames.Add(name);
            }
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    internal void SetFramedInitialSource(XmlLocation firstLocation)
    {
        Enter();
        try
        {
            Source source = FreshExternal();
            if (initialFramed) throw new InvalidOperationException("Initial source was already framed.");
            ValidateFramedLocation(firstLocation, source.Uri, source.LastOffset);
            source.Prolog?.Dispose(); source.Prolog = null;
            SetCursor(source, firstLocation); initialFramed = true;
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    internal void PushFramedEntity(Uri finalUri, XmlLocation firstLocation)
    {
        Enter();
        try
        {
            Check();
            if (pending is null) throw new InvalidOperationException("No pending entity.");
            ArgumentNullException.ThrowIfNull(finalUri);
            if (!finalUri.IsAbsoluteUri) throw new ArgumentException("Final URI must be absolute.", nameof(finalUri));
            ValidateFramedLocation(firstLocation, finalUri, -1);
            // EnterEntity already succeeded when the request was issued. Reserve
            // stack capacity before mutating that one pending source; never reenter.
            sources.EnsureCapacity(sources.Count + 1);
            SetCursor(pending, firstLocation);
            sources.Add(pending); pending = null; request = null;
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    private static void ValidateFramedLocation(XmlLocation at, Uri? uri, long lowerBound)
    {
        if (at.SourceUri != uri || at.SourceUri is { IsAbsoluteUri: false } || at.ByteOffset < 0 ||
            at.ByteOffset is long offset && offset < lowerBound ||
            !((at.LineNumber > 0 && at.LinePosition > 0) || (at.LineNumber == 0 && at.LinePosition == 0)))
            throw new ArgumentException("Invalid framed source location.", nameof(at));
    }

    private static void SetCursor(Source source, XmlLocation at)
    {
        source.Uri = at.SourceUri; source.Line = at.LineNumber; source.Column = at.LinePosition;
        source.LastOffset = at.ByteOffset ?? source.LastOffset;
    }
}
