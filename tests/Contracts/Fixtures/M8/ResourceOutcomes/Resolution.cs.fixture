namespace Xemele;

public enum XmlResourceResolutionStatus
{
    Denied = 0,
    Unavailable = 1,
    Resolved = 2
}

/// <summary>A resource outcome; copying this value does not transfer or dispose resource ownership.</summary>
public readonly struct XmlResourceResolution
{
    public XmlResourceResolutionStatus Status { get; }
    public XmlResolvedResource? Resource { get; }

    private XmlResourceResolution(XmlResourceResolutionStatus status, XmlResolvedResource? resource)
    { Status = status; Resource = resource; }

    public static XmlResourceResolution Denied => default;
    public static XmlResourceResolution Unavailable => new(XmlResourceResolutionStatus.Unavailable, null);

    public static XmlResourceResolution FromResource(XmlResolvedResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return new(XmlResourceResolutionStatus.Resolved, resource);
    }
}
