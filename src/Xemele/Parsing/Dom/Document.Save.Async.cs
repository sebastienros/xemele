namespace Xemele;

public sealed partial class XmlDocument
{
    public Task SaveAsync(Stream output, XmlSaveOptions? options = null, CancellationToken cancellationToken = default) =>
        XmlAsyncSerializer.SerializeAsync(this, output, options, cancellationToken);
}
