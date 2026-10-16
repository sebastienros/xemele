using Xemele.Parsing.Dtd;
using Xemele.Parsing.Input;
using Xemele.Resolution.Core;

namespace Xemele.Parsing.Entities;

// An active source owns fixed scratch and its resolver handle. Parent sources
// keep only indices into their own scratch while a nested source runs. Neither
// the resource nor its decoded body is collected into a whole-source string.
internal sealed class ExternalXmlSource : IDisposable, IAsyncDisposable
{
    private readonly XmlResolutionSession session;
    private readonly XmlResourceHandle handle;
    private readonly XmlInputDecoder decoder;
    private readonly byte[] bytes = new byte[4096];
    private readonly char[] characters = new char[4096];
    private readonly long[] mappings = new long[4096];
    private int byteIndex, byteCount, characterIndex, characterCount;
    private bool physicalEof, decoderCompleted, disposed, framed;
    private XmlDiagnostic? pendingDecoderFailure;
    internal long ByteLength
    {
        get; private set;
    }
    internal Uri SourceUri => handle.SourceUri;
    internal XmlLocation FirstLocation
    {
        get; private set;
    }
    // The framer already owns these bounded values. Publish only after decoder
    // validation; null means no declaration, not an invented default version.
    internal XmlTextDeclaration? TextDeclaration
    {
        get; private set;
    }
    internal XmlDiagnostic? Diagnostic
    {
        get; private set;
    }
    internal ReadOnlySpan<char> Characters => characters.AsSpan(characterIndex, characterCount - characterIndex);
    internal ReadOnlySpan<long> Mappings => mappings.AsSpan(characterIndex, characterCount - characterIndex);
    internal bool HasCharacters => characterIndex != characterCount;
    internal bool Completed => decoderCompleted && characterIndex == characterCount;

    internal ExternalXmlSource(XmlResolutionSession session, XmlResourceHandle handle, XmlParseOptions options)
    {
        this.session = session;
        this.handle = handle;
        decoder = new(options, handle.SourceUri);
        FirstLocation = new(handle.SourceUri, 1, 1, null);
    }

    internal bool Frame(XmlResourceLimits limits, IDtdBudget budget, CancellationToken cancellation)
    {
        using var framer = new XmlTextDeclarationFramer(limits, budget, SourceUri);
        var characterInputSeen = false;
        while (Diagnostic is null)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!DecodeNext(2, 1, cancellation))
            {
                if (Diagnostic is not null)
                {
                    return false;
                }

                var final = framer.Append([], [], true, cancellation);
                return FinishFrame(final, framer, characterInputSeen);
            }
            characterInputSeen = true;
            var result = framer.Append(Characters, Mappings, decoderCompleted, cancellation);
            Consume(result.Consumed);
            if (result.Step != TextDeclarationStep.NeedInput)
            {
                return FinishFrame(result, framer, characterInputSeen);
            }
        }
        return false;
    }

    private bool FinishFrame(TextDeclarationResult result, XmlTextDeclarationFramer frame, bool characterInputSeen)
    {
        if (result.Step == TextDeclarationStep.Rejected)
        {
            Diagnostic = result.Diagnostic;
            if (decoderCompleted && Diagnostic is { Location.ByteOffset: null })
            {
                Diagnostic = Diagnostic with
                {
                    Location = Diagnostic.Location with
                    {
                        ByteOffset = ByteLength
                    }
                };
            }

            return false;
        }
        Diagnostic = decoder.ValidateDeclaration(result.Declaration is { } declaration ? declaration.EncodingName.AsSpan() : [],
            result.Declaration?.Location ?? frame.PrefixLocation);
        if (Diagnostic is not null)
        {
            return false;
        }

        if (result.Step == TextDeclarationStep.Declared)
        {
            FirstLocation = result.NextLocation;
        }
        else
        {
            FirstLocation = frame.PrefixLocation;
            // Prefix and an already-decoded detection carry suffix fit in
            // fixed scratch. Drain owns no view after this method returns.
            Span<char> prefix = stackalloc char[7];
            Span<long> offsets = stackalloc long[7];
            var count = frame.DrainPrefix(prefix, characterInputSeen ? offsets : []);
            var suffix = characterCount - characterIndex;
            characters.AsSpan(characterIndex, suffix).CopyTo(characters.AsSpan(count));
            mappings.AsSpan(characterIndex, suffix).CopyTo(mappings.AsSpan(count));
            prefix[..count].CopyTo(characters);
            offsets[..count].CopyTo(mappings);
            characterIndex = 0;
            characterCount = count + suffix;
        }
        TextDeclaration = result.Declaration;
        framed = true;
        return true;
    }

    internal async ValueTask<bool> FrameAsync(XmlResourceLimits limits, IDtdBudget budget, CancellationToken cancellation)
    {
        using var framer = new XmlTextDeclarationFramer(limits, budget, SourceUri);
        var characterInputSeen = false;
        while (Diagnostic is null)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!await DecodeNextAsync(2, 1, cancellation).ConfigureAwait(false))
            {
                if (Diagnostic is not null)
                {
                    return false;
                }

                return FinishFrame(framer.Append([], [], true, cancellation), framer, characterInputSeen);
            }
            characterInputSeen = true;
            var result = framer.Append(Characters, Mappings, decoderCompleted, cancellation);
            Consume(result.Consumed);
            if (result.Step != TextDeclarationStep.NeedInput)
            {
                return FinishFrame(result, framer, characterInputSeen);
            }
        }
        return false;
    }

    internal ValueTask<bool> EnsureCharactersAsync(CancellationToken cancellation)
    {
        if (Diagnostic is not null)
        {
            return ValueTask.FromResult(false);
        }

        if (HasCharacters)
        {
            return ValueTask.FromResult(true);
        }

        if (pendingDecoderFailure is not null)
        {
            Diagnostic = pendingDecoderFailure;
            return ValueTask.FromResult(false);
        }
        return DecodeNextAsync(4096, 4096, cancellation);
    }

    internal bool EnsureCharacters(CancellationToken cancellation)
    {
        if (Diagnostic is not null)
        {
            return false;
        }

        if (characterIndex != characterCount)
        {
            return true;
        }

        if (pendingDecoderFailure is not null)
        {
            Diagnostic = pendingDecoderFailure;
            return false;
        }
        return DecodeNext(4096, 4096, cancellation);
    }

    internal void Consume(int count)
    {
        if ((uint)count > (uint)(characterCount - characterIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        characterIndex += count;
    }

    private bool DecodeNext(int capacity, int readSize, CancellationToken cancellation)
    {
        if (characterIndex != characterCount)
        {
            return true;
        }

        characterIndex = characterCount = 0;
        while (!decoderCompleted && Diagnostic is null)
        {
            cancellation.ThrowIfCancellationRequested();
            // Drain bounded decoder carry before asking the resource for more
            // bytes. During prolog framing each read adds only one original byte.
            var result = decoder.Decode(bytes.AsSpan(byteIndex, byteCount - byteIndex),
                characters.AsSpan(0, capacity), mappings.AsSpan(0, capacity), physicalEof, cancellation);
            byteIndex += result.ConsumedBytes;
            ByteLength = checked(ByteLength + result.ConsumedBytes);
            characterCount = result.WrittenCharacters;
            if (result.Step == DecodeStep.InvalidInput)
            {
                if (framed && characterCount != 0)
                {
                    pendingDecoderFailure = result.Diagnostic;
                    return true;
                }
                Diagnostic = result.Diagnostic;
                return false;
            }
            decoderCompleted = result.Step == DecodeStep.Completed;
            if (characterCount != 0)
            {
                return true;
            }

            if (decoderCompleted)
            {
                return false;
            }

            if (byteIndex == byteCount)
            {
                var read = session.Read(handle, bytes.AsSpan(0, readSize), FirstLocation);
                if (read.Diagnostic is not null)
                {
                    Diagnostic = read.Diagnostic;
                    return false;
                }
                byteIndex = 0;
                byteCount = read.Count;
                physicalEof = read.Count == 0;
            }
        }
        return false;
    }

    private async ValueTask<bool> DecodeNextAsync(int capacity, int readSize, CancellationToken cancellation)
    {
        if (characterIndex != characterCount)
        {
            return true;
        }

        characterIndex = characterCount = 0;
        while (!decoderCompleted && Diagnostic is null)
        {
            cancellation.ThrowIfCancellationRequested();
            // Drain bounded decoder carry before asking the resource for more
            // bytes. During prolog framing each read adds only one original byte.
            var result = decoder.Decode(bytes.AsSpan(byteIndex, byteCount - byteIndex),
                characters.AsSpan(0, capacity), mappings.AsSpan(0, capacity), physicalEof, cancellation);
            byteIndex += result.ConsumedBytes;
            ByteLength = checked(ByteLength + result.ConsumedBytes);
            characterCount = result.WrittenCharacters;
            if (result.Step == DecodeStep.InvalidInput)
            {
                if (framed && characterCount != 0)
                {
                    pendingDecoderFailure = result.Diagnostic;
                    return true;
                }
                Diagnostic = result.Diagnostic;
                return false;
            }
            decoderCompleted = result.Step == DecodeStep.Completed;
            if (characterCount != 0)
            {
                return true;
            }

            if (decoderCompleted)
            {
                return false;
            }

            if (byteIndex == byteCount)
            {
                var read = await session.ReadAsync(handle, bytes.AsMemory(0, readSize), FirstLocation).ConfigureAwait(false);
                if (read.Diagnostic is not null)
                {
                    Diagnostic = read.Diagnostic;
                    return false;
                }
                byteIndex = 0;
                byteCount = read.Count;
                physicalEof = read.Count == 0;
            }
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            decoder.Dispose();
        }
        finally { await handle.DisposeAsync().ConfigureAwait(false); }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        decoder.Dispose();
        handle.Dispose();
    }
}
