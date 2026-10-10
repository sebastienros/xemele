namespace Xemele;

public enum XmlDtdProcessing { Prohibit, Parse }

public sealed partial record XmlParseOptions
{
    public XmlDtdProcessing DtdProcessing { get; init; } = XmlDtdProcessing.Prohibit;
    public XmlResourceResolver? Resolver { get; init; }
}
