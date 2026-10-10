using System.Reflection;
using System.Runtime.CompilerServices;
// Compare concrete runtime internal declarations with the approved independently
// compiled model, rather than manufacturing an empty public API fragment.
var model = Assembly.LoadFile(Path.GetFullPath(args[0]));
var runtime = Assembly.LoadFile(Path.GetFullPath(args[1]));
string[] types = ["ScanStep", "ScanResult", "XmlNameView", "XmlAttributeView", "XmlNamespaceView", "XmlDeclarationView", "XmlTextKind", "IXmlEventSink", "XmlParserCore"];
var rows = new List<string>();
foreach (string shortName in types)
{
    string name = "Xemele.Parsing.Syntax." + shortName;
    var expected = Shape(model.GetType(name, throwOnError: true)!);
    var actual = Shape(runtime.GetType(name, throwOnError: true)!);
    if (!expected.SequenceEqual(actual)) throw new Exception("Internal declaration mismatch: " + name + "\nexpected:\n" + string.Join('\n', expected) + "\nactual:\n" + string.Join('\n', actual));
    rows.AddRange(actual);
}
File.WriteAllLines(args[2], rows);
Console.WriteLine($"Matched {types.Length} approved internal types, {rows.Count} declaration rows; no new public API family.");
static string TypeName(Type t) => t.IsGenericParameter ? "!" + t.GenericParameterPosition : t.IsGenericType ? t.GetGenericTypeDefinition().FullName + "<" + string.Join(',', t.GetGenericArguments().Select(TypeName)) + ">" : t.FullName ?? t.Name;
static string Parameters(ParameterInfo[] parameters) => string.Join(',', parameters.Select(p => TypeName(p.ParameterType) + " " + p.Name + (p.HasDefaultValue ? "=" + (p.DefaultValue?.ToString() ?? "null") : "")));
static string[] Shape(Type t)
{
    var rows = new List<string> { $"type {t.FullName} public={t.IsPublic} interface={t.IsInterface} enum={t.IsEnum} value={t.IsValueType} ref={t.IsByRefLike} readonly={t.IsDefined(typeof(IsReadOnlyAttribute))} sealed={t.IsSealed}" };
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    foreach (var c in t.GetConstructors(flags)) rows.Add($"{t.FullName}.ctor({Parameters(c.GetParameters())})");
    foreach (var m in t.GetMethods(flags))
    {
        string generics = m.IsGenericMethod ? "<" + string.Join(',', m.GetGenericArguments().Select(g => $"!{g.GenericParameterPosition}:{g.GenericParameterAttributes}:{string.Join('&', g.GetGenericParameterConstraints().Select(TypeName))}")) + ">" : "";
        rows.Add($"{t.FullName}.{m.Name}{generics}({Parameters(m.GetParameters())}):{TypeName(m.ReturnType)} static={m.IsStatic}");
    }
    foreach (var p in t.GetProperties(flags)) rows.Add($"{t.FullName}.{p.Name}:{TypeName(p.PropertyType)}");
    foreach (var f in t.GetFields(flags)) rows.Add($"{t.FullName}.{f.Name}:{TypeName(f.FieldType)} literal={f.IsLiteral}" + (f.IsLiteral ? "=" + f.GetRawConstantValue() : ""));
    return rows.Order(StringComparer.Ordinal).ToArray();
}
