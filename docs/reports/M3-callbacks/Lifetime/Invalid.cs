using Xemele.Streaming;
#if STORAGE
public sealed class InvalidStorage { public XmlStartElement Saved; }
#endif
#if AWAIT
public static class InvalidAwait
{
    public static async Task Invalid()
    {
        XmlAttributeView view = default;
        await Task.Yield();
        Console.WriteLine(view.Count);
    }
}
#endif
