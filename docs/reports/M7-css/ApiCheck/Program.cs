using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;

if (args.Length != 3 || args[0] is not ("emit" or "compare"))
{
    Console.Error.WriteLine("Usage: ApiCheck emit|compare <assembly.dll> <fragment.txt>");
    return 2;
}
try
{
    var reference = MetadataReference.CreateFromFile(Path.GetFullPath(args[1]));
    var runtime = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path));
    // Resolve verified tree value types so their nullable metadata is not treated
    // as an unresolved reference type. This is an owned contract extractor copy.
    var dependencies = Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "*.dll")
        .Where(path => Path.GetFullPath(path) != Path.GetFullPath(args[1]))
        .Select(path => MetadataReference.CreateFromFile(path));
    var compilation = CSharpCompilation.Create("ApiInspection", references: runtime.Append(reference).Concat(dependencies),
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
    var assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(reference)!;
    var format = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers |
            SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType |
            SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
        parameterOptions: SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeParamsRefOut |
            SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        kindOptions: SymbolDisplayKindOptions.IncludeTypeKeyword | SymbolDisplayKindOptions.IncludeMemberKeyword | SymbolDisplayKindOptions.IncludeNamespaceKeyword,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
    var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
    void Add(ISymbol symbol)
    {
        string id = symbol.GetDocumentationCommentId() ?? throw new InvalidDataException($"No documentation identity: {symbol}");
        string declaration = symbol.ToDisplayString(format) + "; access=" + symbol.DeclaredAccessibility;
        if (symbol is INamedTypeSymbol type)
        {
            var bases = new List<string>();
            if (type.BaseType is not null) bases.Add(type.BaseType.ToDisplayString(format));
            bases.AddRange(type.Interfaces.Select(i => i.ToDisplayString(format)).Order(StringComparer.Ordinal));
            declaration += " : " + string.Join(", ", bases);
            declaration += $"; abstract={type.IsAbstract}; sealed={type.IsSealed}; readonly={type.IsReadOnly}; refLike={type.IsRefLikeType}";
        }
        declaration += $"; virtual={symbol.IsVirtual}; abstract={symbol.IsAbstract}; override={symbol.IsOverride}; sealed={symbol.IsSealed}";
        if (symbol is IPropertySymbol property)
        {
            declaration += "; get=" + property.GetMethod?.DeclaredAccessibility + "; set=" + property.SetMethod?.DeclaredAccessibility;
            declaration += "; init=" + (property.SetMethod?.IsInitOnly ?? false) + "; required=" + property.IsRequired;
        }
        // Constants are ABI contract data, independent of culture and the display formatter.
        if (symbol is IFieldSymbol { HasConstantValue: true } field)
            declaration += " = " + JsonSerializer.Serialize(field.ConstantValue);
        // Assembly attributes affect source/binary semantics beyond C# display text.
        var attributes = symbol.GetAttributes().Where(a => a.AttributeClass?.ContainingNamespace.ToDisplayString() != "System.Runtime.CompilerServices")
            .Select(a => a.ToString()).Order(StringComparer.Ordinal);
        declaration += string.Concat(attributes.Select(a => " [" + a + "]"));
        if (!rows.TryAdd(id, declaration)) throw new InvalidDataException("Duplicate member: " + id);
    }
    static bool Visible(ISymbol symbol) => symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
    void Type(INamedTypeSymbol type)
    {
        if (!Visible(type)) return;
        Add(type);
        foreach (var member in type.GetMembers())
        {
            if (member is INamedTypeSymbol nested) { Type(nested); continue; }
            if (!Visible(member)) continue;
            if (member is IMethodSymbol method && method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.StaticConstructor) continue;
            Add(member);
        }
    }
    void Namespace(INamespaceSymbol ns)
    {
        foreach (var child in ns.GetNamespaceMembers()) Namespace(child);
        if(ns.ToDisplayString()=="Xemele.Css") foreach (var type in ns.GetTypeMembers()) Type(type);
    }
    Namespace(assembly.GlobalNamespace);
    // Alphabetic JSON keys, Unicode left literal: same canonical format as discovery.
    var jsonOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    var output = new List<string> { "xemele-api-v1" };
    foreach (var (id, declaration) in rows)
        output.Add(JsonSerializer.Serialize(new { declaration, id, member = id }, jsonOptions));
    string canonical = string.Join('\n', output) + "\n";
    if (args[0] == "emit") { File.WriteAllText(args[2], canonical); Console.WriteLine($"Emitted {rows.Count} members"); return 0; }
    if (File.ReadAllText(args[2]).Replace("\r\n", "\n", StringComparison.Ordinal) != canonical)
    { Console.Error.WriteLine("API mismatch: missing, added, changed, reordered or noncanonical entries."); return 1; }
    Console.WriteLine($"Matched {rows.Count} compiled members"); return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
