using System.Reflection;
using Xemele;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
internal static class Inventory
{
    internal static void Print()
    {
        var nullable = new NullabilityInfoContext();
        Type[] types = [typeof(XmlDocumentType), typeof(XmlDocument), typeof(XmlParseOptions), typeof(XmlDtdProcessing),
            typeof(EntityUse), typeof(EntityExpansionRequest), typeof(IXmlEntityContext), typeof(IXmlDtdSink), typeof(IXmlEventSink), typeof(DomSink)];
        foreach(var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            Console.WriteLine($"{type.FullName}|sealed={type.IsSealed}|value={type.IsValueType}");
            if(type.IsEnum) { foreach(var name in Enum.GetNames(type)) Console.WriteLine($"  enum:{name}={Convert.ToInt32(Enum.Parse(type,name))}"); continue; }
            if(type != typeof(XmlDocument) && type != typeof(DomSink))
                foreach(var ctor in type.GetConstructors().OrderBy(c=>c.ToString(),StringComparer.Ordinal))
                    Console.WriteLine("  ctor:"+string.Join(';',ctor.GetParameters().Select(Param)));
            foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                .Where(m=> !m.IsSpecialName && (type == typeof(XmlDocument) ? m.Name == "SetDtdMetadata" : type == typeof(DomSink) ? m.Name == "DocumentType" : m.IsPublic))
                .OrderBy(m=>m.ToString(),StringComparer.Ordinal))
                Console.WriteLine("  method:"+method+"|"+nullable.Create(method.ReturnParameter).ReadState+"|"+string.Join(';',method.GetParameters().Select(Param)));
            foreach(var property in type.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                .Where(p=>type == typeof(XmlDocument) ? p.Name is "DocumentType" or "Dtd" : type != typeof(DomSink) && p.GetMethod!.IsPublic)
                .OrderBy(p=>p.Name,StringComparer.Ordinal))
                Console.WriteLine("  property:"+property.Name+"|"+property.PropertyType+"|"+nullable.Create(property).ReadState+"|set:"+(property.SetMethod is not null));
        }
        var options=new XmlParseOptions(); Console.WriteLine($"defaults:{options.DtdProcessing}|resolverNull={options.Resolver is null}|limits={options.Limits.Depth}");
        Console.WriteLine($"optionalDom={typeof(IXmlDtdSink).IsAssignableFrom(typeof(DomSink))}|eventDom={typeof(IXmlEventSink).IsAssignableFrom(typeof(DomSink))}");
        string Param(ParameterInfo p)=>p.Name+":"+p.ParameterType+":"+nullable.Create(p).ReadState+":optional="+p.IsOptional+":default="+(p.HasDefaultValue?p.DefaultValue??"null":"none");
    }
}
