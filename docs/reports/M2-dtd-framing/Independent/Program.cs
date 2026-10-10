using Xemele;
using Xemele.Parsing.Dtd;

if (!args.Contains("--contract-inventory"))
{
    using var f = new XmlTextDeclarationFramer(new());
    var r = f.Append("<?xml encoding='X'?>body", [], true, default);
    if (r.Step != TextDeclarationStep.Declared || r.Declaration!.Value.EncodingName != "X" || r.Consumed != 20)
        throw new Exception("Independent source framing failed.");
}
// No decoder, resolver, syntax core, tree, operation budget or DTD grammar source
// is present in this project. Report actual internal signatures for comparison.
foreach (var type in new[] { typeof(IDtdBudget), typeof(XmlTextDeclarationFramer), typeof(XmlTextDeclaration), typeof(TextDeclarationResult), typeof(TextDeclarationStep) })
{
    Console.WriteLine(type.FullName);
    if (!type.IsNotPublic) throw new Exception("Internal contract exposed publicly.");
    foreach (var constructor in type.GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).OrderBy(c => c.ToString(), StringComparer.Ordinal))
        Console.WriteLine("  internal " + constructor);
    foreach (var member in type.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
        .Where(m => m is System.Reflection.MethodBase or System.Reflection.PropertyInfo or System.Reflection.FieldInfo).OrderBy(m => m.ToString(), StringComparer.Ordinal))
        Console.WriteLine("  " + member);
}
