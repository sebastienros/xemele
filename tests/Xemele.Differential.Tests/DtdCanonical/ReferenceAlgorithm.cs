using System.Reflection;
using System.Xml;
namespace Xemele.Differential.Tests.DtdCanonical;
internal static class ReferenceAlgorithm
{
    internal static byte[] Reference(string xml,int mode,bool comments,string? expression=null,string[]? prefixes=null)
    {
        string package=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".nuget/packages/system.security.cryptography.xml/10.0.11/lib/net10.0/System.Security.Cryptography.Xml.dll");
        var assembly=Assembly.LoadFrom(package);var type=assembly.GetType(mode==0?"System.Security.Cryptography.Xml.XmlDsigC14NTransform":"System.Security.Cryptography.Xml.XmlDsigExcC14NTransform",true)!;
        var transform=Activator.CreateInstance(type,[comments])!;
        if(prefixes is {Length:>0}) type.GetProperty("InclusiveNamespacesPrefixList")!.SetValue(transform,string.Join(' ',prefixes));
        var document=new System.Xml.XmlDocument{PreserveWhitespace=true,XmlResolver=null};document.LoadXml(xml);
        object input=expression is null?document:document.SelectNodes(expression)!;
        type.GetMethod("LoadInput")!.Invoke(transform,[input]);
        using var result=(Stream)type.GetMethod("GetOutput",Type.EmptyTypes)!.Invoke(transform,null)!;using var bytes=new MemoryStream();result.CopyTo(bytes);return bytes.ToArray();
    }
}
