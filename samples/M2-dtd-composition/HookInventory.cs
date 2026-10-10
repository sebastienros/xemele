using System.Reflection;
using Xemele.Parsing.Dtd;
internal static class HookInventory
{
    internal static bool Print()
    {
        var nullability = new NullabilityInfoContext();
        var names = new HashSet<string>(StringComparer.Ordinal) { "SeedPriorModel", "SetFramedInitialSource", "PushFramedEntity" };
        var methods = typeof(DtdParser).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => names.Contains(m.Name)).OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
        string[] expected = [
            "PushFramedEntity|internal=True|return=System.Void|finalUri:System.Uri:NotNull:optional=False:default=none;firstLocation:Xemele.XmlLocation:NotNull:optional=False:default=none",
            "SeedPriorModel|internal=True|return=System.Void|model:Xemele.Parsing.Dtd.DtdModel:NotNull:optional=False:default=none;cancellationToken:System.Threading.CancellationToken:NotNull:optional=False:default=none",
            "SetFramedInitialSource|internal=True|return=System.Void|firstLocation:Xemele.XmlLocation:NotNull:optional=False:default=none"
        ];
        var actual = methods.Select(m => m.Name + "|internal=" + m.IsAssembly + "|return=" + m.ReturnType.FullName + "|" +
            string.Join(';', m.GetParameters().Select(p => p.Name + ":" + p.ParameterType.FullName + ":" + nullability.Create(p).ReadState + ":optional=" + p.IsOptional + ":default=" + (p.HasDefaultValue ? p.DefaultValue ?? "null" : "none")))).ToArray();
        foreach (string line in actual) Console.WriteLine(line);
        return actual.SequenceEqual(expected);
    }
}
