using System.Reflection;
using Xemele.Parsing.Dtd;
internal static class Inventory
{
    internal static void Print()
    {
        var nullable = new NullabilityInfoContext();
        Type[] types = [typeof(DtdSubsetKind), typeof(DtdAttributeType), typeof(DtdDefaultKind), typeof(DtdParticleKind), typeof(DtdOccurrence),
            typeof(DtdParticle), typeof(DtdElementDeclaration), typeof(DtdAttributeDeclaration), typeof(DtdEntityDeclaration), typeof(DtdNotationDeclaration),
            typeof(DtdModel), typeof(DtdParseStep), typeof(DtdEntityRequest), typeof(DtdParseResult), typeof(IDtdBudget), typeof(DtdParser),
            typeof(TextDeclarationStep), typeof(XmlTextDeclaration), typeof(TextDeclarationResult), typeof(XmlTextDeclarationFramer)];
        foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            Console.WriteLine($"{type.FullName}|sealed={type.IsSealed}|value={type.IsValueType}|interfaces={string.Join(',', type.GetInterfaces().Select(t => t.ToString()).Order(StringComparer.Ordinal))}");
            if (type.IsEnum) { foreach (var name in Enum.GetNames(type)) Console.WriteLine($"  enum:{name}={Convert.ToInt32(Enum.Parse(type, name))}"); continue; }
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(c => c.IsPublic || (type == typeof(DtdParser) || type == typeof(XmlTextDeclarationFramer)) && c.IsAssembly).OrderBy(c => c.ToString(), StringComparer.Ordinal))
                Console.WriteLine("  ctor:" + ctor.IsPublic + "|" + string.Join(';', ctor.GetParameters().Select(Param)));
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(m => m.ToString(), StringComparer.Ordinal))
                Console.WriteLine("  method:" + method + "|" + nullable.Create(method.ReturnParameter).ReadState + "|" + string.Join(';', method.GetParameters().Select(Param)));
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(p => p.Name, StringComparer.Ordinal))
                Console.WriteLine("  property:" + property.Name + "|" + property.PropertyType + "|" + nullable.Create(property).ReadState + "|set:" + (property.SetMethod is not null));
        }
        string Param(ParameterInfo p) => p.Name + ":" + p.ParameterType + ":" + nullable.Create(p).ReadState + ":optional=" + p.IsOptional + ":default=" + (p.HasDefaultValue ? p.DefaultValue ?? "null" : "none");
    }
}
