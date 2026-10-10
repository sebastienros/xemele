namespace Xemele;
public sealed partial class XmlDocument
{
    public string ToXmlString(XmlSaveOptions? options = null, CancellationToken cancellationToken = default) => XmlSerializer.Serialize(this, options, cancellationToken);
    public void Save(Stream output, XmlSaveOptions? options = null, CancellationToken cancellationToken = default) => XmlSerializer.Serialize(this, output, options, cancellationToken);
}
