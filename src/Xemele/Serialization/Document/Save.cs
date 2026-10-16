namespace Xemele;

public sealed partial class XmlDocument
{
    /// <summary>Serializes this document to XML text.</summary>
    /// <param name="options">Serialization options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling serialization.</param>
    /// <returns>The XML string, whose declaration has no encoding pseudo-attribute.</returns>
    /// <exception cref="InvalidOperationException">This document is not serializable as XML.</exception>
    public string ToXmlString(XmlSaveOptions? options = null, CancellationToken cancellationToken = default) => XmlSerializer.Serialize(this, options, cancellationToken);
    /// <summary>Serializes this document to a stream without closing it.</summary>
    /// <param name="output">The writable, caller-owned stream.</param>
    /// <param name="options">Serialization options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling serialization.</param>
    /// <exception cref="InvalidOperationException">This document is not serializable as XML.</exception>
    public void Save(Stream output, XmlSaveOptions? options = null, CancellationToken cancellationToken = default) => XmlSerializer.Serialize(this, output, options, cancellationToken);
}
