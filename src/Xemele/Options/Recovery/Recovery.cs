namespace Xemele;

public sealed partial record XmlParseOptions
{
    /// <summary>Gets whether diagnostic-returning factories attempt recovery from supported XML errors.</summary>
    /// <remarks>The default is <see langword="false"/>. Strict factories reject options with recovery enabled.</remarks>
    public bool Recover { get; init; } = false;
}
