using System.Reflection;
using Xemele;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;

internal static class Contract
{
    internal static void Print()
    {
        var nullable = new NullabilityInfoContext();
        foreach (var constructor in typeof(XmlParserCore).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(c => c.GetParameters().Length))
            Console.WriteLine("core:" + constructor.IsPublic + ":" + string.Join(";", constructor.GetParameters().Select(p => p.Name + ":" + p.ParameterType + ":" + nullable.Create(p).ReadState + ":optional=" + p.IsOptional + ":default=" + (p.HasDefaultValue ? p.DefaultValue ?? "null" : "none"))));
        Console.WriteLine("context=" + typeof(IXmlEntityContext).FullName + ";optional=" + typeof(IXmlDtdSink).FullName + ";default=" + new XmlParseOptions().DtdProcessing);
    }
}
