namespace Xemele;

/// <summary>Specifies whether a document type declaration may be parsed.</summary>
public enum XmlDtdProcessing
{
    /// <summary>Reject document type declarations.</summary>
    Prohibit,
    /// <summary>Parse document type declarations and apply supported DTD declarations.</summary>
    Parse
}

public sealed partial record XmlParseOptions
{
    /// <summary>Gets the DTD processing policy; the default prohibits document type declarations.</summary>
    public XmlDtdProcessing DtdProcessing { get; init; } = XmlDtdProcessing.Prohibit;
    /// <summary>Gets the resolver for external XML resources, or <see langword="null"/> when no resolver is supplied.</summary>
    public XmlResourceResolver? Resolver
    {
        get; init;
    }
}
