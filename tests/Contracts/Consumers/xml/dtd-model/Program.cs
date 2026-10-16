using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Dtd;

if (args.Length != 1) throw new ArgumentException("Supply the independently compiled approved model assembly.");
var approved = Assembly.LoadFrom(Path.GetFullPath(args[0]));
var actual = typeof(DtdParticle).Assembly;
string[] names = ["DtdSubsetKind", "DtdAttributeType", "DtdDefaultKind", "DtdParticleKind", "DtdOccurrence", "DtdParticle", "DtdElementDeclaration", "DtdAttributeDeclaration", "DtdEntityDeclaration", "DtdNotationDeclaration", "DtdModel"];
var nullability = new NullabilityInfoContext();
string TypeName(Type t) => t.IsGenericType ? t.GetGenericTypeDefinition().FullName + "[" + string.Join(",", t.GetGenericArguments().Select(TypeName)) + "]" : t.FullName ?? t.Name;
string Nulls(NullabilityInfo n) => $"{n.ReadState}/{n.WriteState}[{string.Join(",", n.GenericTypeArguments.Select(Nulls))}]";
string Parameter(ParameterInfo p) => $"{p.Name}:{TypeName(p.ParameterType)}:{p.Attributes}:{Nulls(nullability.Create(p))}";
string[] Inventory(Type type)
{
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    var rows = new List<string> { $"type:{TypeName(type)}:{type.Attributes}:{TypeName(type.BaseType!)}:{string.Join(",", type.GetInterfaces().Select(TypeName).Order())}" };
    foreach (var constructor in type.GetConstructors(flags))
        rows.Add($"ctor:{constructor.Attributes}({string.Join(",", constructor.GetParameters().Select(Parameter))})");
    foreach (var property in type.GetProperties(flags))
        rows.Add($"property:{property.Name}:{TypeName(property.PropertyType)}:{Nulls(nullability.Create(property))}:get={property.GetMethod?.Attributes}:set={property.SetMethod?.Attributes}:mods={string.Join(",", property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers().Select(TypeName) ?? [])}");
    foreach (var method in type.GetMethods(flags))
        rows.Add($"method:{method.Name}:{method.Attributes}:{TypeName(method.ReturnType)}:{Nulls(nullability.Create(method.ReturnParameter))}({string.Join(",", method.GetParameters().Select(Parameter))})");
    foreach (var field in type.GetFields(flags))
        rows.Add($"field:{field.Name}:{TypeName(field.FieldType)}:{field.Attributes}:{(field.IsLiteral ? field.GetRawConstantValue() : null)}");
    return rows.Order(StringComparer.Ordinal).ToArray();
}
int members = 0;
foreach (string name in names)
{
    var expected = approved.GetType("Xemele.Parsing.Dtd." + name, true)!;
    var implementation = actual.GetType("Xemele.Parsing.Dtd." + name, true)!;
    var expectedRows = Inventory(expected);
    var actualRows = Inventory(implementation);
    if (!expectedRows.SequenceEqual(actualRows))
        throw new InvalidOperationException(name + " inventory mismatch:\n" + string.Join("\n", expectedRows.Except(actualRows).Select(x => "missing: " + x).Concat(actualRows.Except(expectedRows).Select(x => "added: " + x))));
    members += actualRows.Length;
}
var extra = actual.GetTypes().Where(t => t.Namespace == "Xemele.Parsing.Dtd").Select(t => t.Name).Except(names).ToArray();
if (extra.Length > 0) throw new InvalidOperationException("Unexpected model types: " + string.Join(",", extra));

long setupStart = GC.GetAllocatedBytesForCurrentThread();
var location = new XmlLocation(new Uri("https://fixture.invalid/sample.dtd"), 3, 9, 82);
var leaf = new DtdParticle(DtdParticleKind.Name, "child", DtdOccurrence.Optional, []);
var sourceChildren = new[] { leaf };
var content = new DtdParticle(DtdParticleKind.Sequence, null, DtdOccurrence.OneOrMore, sourceChildren);
var element = new DtdElementDeclaration("root", content, false, location);
var tokens = new[] { "small", "large" };
var attribute = new DtdAttributeDeclaration("root", "size", DtdAttributeType.Enumeration, tokens, DtdDefaultKind.Fixed, "small", false, location);
var entity = new DtdEntityDeclaration("image", false, null, "image.png", null, "png", true, location);
DtdElementDeclaration[] elements = [element];
DtdAttributeDeclaration[] attributes = [attribute];
DtdEntityDeclaration[] generals = [entity];
DtdEntityDeclaration[] parameters = [];
DtdNotationDeclaration[] notations = [new("png", null, "image/png", location)];
long fixtureSetup = GC.GetAllocatedBytesForCurrentThread() - setupStart;
Xemele.Parsing.Dtd.DtdModel Create() => new(elements, attributes, generals, parameters, notations);
var sample = Create();
sourceChildren[0] = new(DtdParticleKind.Any, null, DtdOccurrence.Once, []);
tokens[0] = "changed";
if (sample.Elements[0].Content.Children[0].Name != "child" || sample.Attributes[0].Tokens[0] != "small") throw new InvalidOperationException("Caller mutation escaped snapshot.");
for (int i = 0; i < 100; i++) GC.KeepAlive(Create());
long start = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 1000; i++) GC.KeepAlive(Create());
long persistence = GC.GetAllocatedBytesForCurrentThread() - start;
for (int i = 0; i < 100; i++) GC.KeepAlive(new DtdParticle(DtdParticleKind.Sequence, null, DtdOccurrence.Once, sourceChildren));
start = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 1000; i++) GC.KeepAlive(new DtdParticle(DtdParticleKind.Sequence, null, DtdOccurrence.Once, sourceChildren));
long particlePersistence = GC.GetAllocatedBytesForCurrentThread() - start;
for (int i = 0; i < 100; i++) GC.KeepAlive(new DtdAttributeDeclaration("r", "a", DtdAttributeType.Enumeration, tokens, DtdDefaultKind.Value, "small", false, location));
start = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 1000; i++) GC.KeepAlive(new DtdAttributeDeclaration("r", "a", DtdAttributeType.Enumeration, tokens, DtdDefaultKind.Value, "small", false, location));
long attributePersistence = GC.GetAllocatedBytesForCurrentThread() - start;
var retained = Lifetime();
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
if (retained.Source.IsAlive || retained.Characters.IsAlive) throw new InvalidOperationException("Model retained mutable source storage.");
if (retained.Model.Elements[0].Name != "owned" || retained.Model.Elements[0].Content.Name != "owned") throw new InvalidOperationException("Owned lifetime content lost.");
GC.KeepAlive(retained.Model);
Console.WriteLine(JsonSerializer.Serialize(new
{
    approvedTypes = names.Length,
    inventoryRows = members,
    fixtureSetupBytes = fixtureSetup,
    modelPersistenceBytes = persistence,
    particlePersistenceBytes = particlePersistence,
    attributePersistenceBytes = attributePersistence,
    measuredOperations = 1000,
    sourceListCapacity = 200000,
    sourceCollected = !retained.Source.IsAlive,
    mutableCharactersCollected = !retained.Characters.IsAlive,
    limitation = "Informational synchronous constructor persistence on this host. Fixture setup separate; immutable strings, Uri and child objects retained; no throughput, zero-allocation, or retained-byte-count claim."
}));

[MethodImpl(MethodImplOptions.NoInlining)]
static (Xemele.Parsing.Dtd.DtdModel Model, WeakReference Source, WeakReference Characters) Lifetime()
{
    char[] characters = ['o', 'w', 'n', 'e', 'd'];
    string name = new(characters);
    var children = new DtdParticle(DtdParticleKind.Name, name, DtdOccurrence.Once, []);
    var source = new List<DtdElementDeclaration>(200000) { new(name, children, false, default) };
    var model = new Xemele.Parsing.Dtd.DtdModel(source, [], [], [], []);
    return (model, new WeakReference(source), new WeakReference(characters));
}
