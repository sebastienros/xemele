using System.Runtime.CompilerServices;
using Xemele;
using Xemele.Html;

internal static class ModelProbe
{
    [ModuleInitializer]
    internal static void Verify()
    {
        if (typeof(HtmlNode).Assembly != typeof(XmlNode).Assembly || typeof(HtmlNode).Assembly == typeof(Xemele.Css.CssSelector).Assembly)
            throw new Exception("Consumers must reference actual verified HTML and XML models");
        var html = new HtmlDocument();
        var p = html.CreateElement("p");
        html.DocumentNode.AppendChild(p);
        p.SetAttribute(new("class", "a"));
        if (p.OwnerDocument != html || p.Parent != html.DocumentNode || p.GetAttribute("class") != "a")
            throw new Exception("Actual HTML model changed");
        Console.WriteLine("PASS: actual HTML and XML model assembly identity; actual HTML tree probe");
    }
}
