namespace Xemele;

/// <summary>Describes whether an external resource was permitted and obtained.</summary>
public enum XmlResourceResolutionStatus
{
    /// <summary>Access to the resource was denied.</summary>
    Denied = 0,
    /// <summary>The resource was permitted but could not be obtained.</summary>
    Unavailable = 1,
    /// <summary>The resource content is available.</summary>
    Resolved = 2
}

/// <summary>A resource outcome; copying this value does not transfer or dispose resource ownership.</summary>
public readonly struct XmlResourceResolution
{
    /// <summary>Gets the resolution outcome.</summary>
    public XmlResourceResolutionStatus Status
    {
        get;
    }
    /// <summary>Gets the owned resource for a resolved outcome, or null otherwise.</summary>
    public XmlResolvedResource? Resource
    {
        get;
    }

    private XmlResourceResolution(XmlResourceResolutionStatus status, XmlResolvedResource? resource)
    {
        Status = status;
        Resource = resource;
    }

    /// <summary>Gets an outcome indicating that resource access was denied.</summary>
    public static XmlResourceResolution Denied => default;
    /// <summary>Gets an outcome indicating that the resource could not be obtained.</summary>
    public static XmlResourceResolution Unavailable => new(XmlResourceResolutionStatus.Unavailable, null);

    /// <summary>Creates a successful outcome containing an owned resource.</summary>
    /// <param name="resource">The resource whose ownership must be handled by the consumer.</param>
    /// <returns>A resolved outcome containing the supplied resource.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is null.</exception>
    public static XmlResourceResolution FromResource(XmlResolvedResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return new(XmlResourceResolutionStatus.Resolved, resource);
    }
}
