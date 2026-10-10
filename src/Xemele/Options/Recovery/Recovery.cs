namespace Xemele;

public sealed partial record XmlParseOptions
{
    public bool Recover { get; init; } = false;
}
