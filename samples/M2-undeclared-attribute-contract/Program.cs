using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Syntax;

int checks = 0;
void Check(bool test) { checks++; if (!test) throw new InvalidOperationException("Model assertion " + checks); }
try
{
    Check(Preservation.Compare(args[0]) == 7);
    Assembly product = Assembly.LoadFrom(args[0]);
    // Public handle/member metadata is independently compiled from frozen fixtures.
    foreach (Type type in new[] { typeof(XmlNode), typeof(XmlAttribute), typeof(XmlNamespaceDeclaration),
        typeof(XmlAttributes), typeof(XmlNamespaceDeclarations), typeof(XmlNodeChildren), typeof(XmlUnresolvedReference) })
    {
        Type actual = product.GetType(type.FullName!, true)!;
        if (!PublicShape(type).SequenceEqual(PublicShape(actual))) throw new InvalidOperationException("Frozen public shape mismatch: " + type.FullName);
        Check(true);
    }
    Check(product.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.EndsWith(args[1], StringComparison.Ordinal));
    var primary = new XmlLocation(new Uri("urn:primary"), 3, 9, 40);
    var nested = new XmlLocation(new Uri("urn:entity-declaration"), 2, 15, 29);
    var dtd = new XmlLocation(new Uri("urn:subset"), 1, 1, 0);
    char[] lexical = "  A &x;&y; B&#x9;".ToCharArray();
    XmlAttributeValuePart[] parts = [new XmlKnownAttributeText("  A ", primary),
        new XmlUnknownAttributeReference("x", "&x;", primary),
        new XmlUnknownAttributeReference("y", "&y;", nested), new XmlKnownAttributeText(" B\t", primary)];
    var sink = new Consumer();
    var declaration = new XmlIncompleteAttributeDefaultView("r", "a", lexical, true, true, dtd, primary, parts, 0);
    Check(!sink.IncompleteAttributeDefault(declaration)); // pause after exactly one copy
    var view = new XmlIncompleteAttributeView(new("a", "", ""), lexical, true, primary, parts, 0);
    Check(!sink.IncompleteAttribute(view));
    Array.Fill(lexical, '!'); parts[0] = new XmlKnownAttributeText("changed", dtd);
    Check(sink.Defaults.Count == 1 && sink.Values.Count == 1);
    var owned = sink.Values[0];
    Check(owned.LexicalValue == "  A &x;&y; B&#x9;" && owned.IsTokenized);
    Check(owned.Parts.Count == 4 && ((XmlKnownAttributeText)owned.Parts[0]).Text == "  A ");
    Check(((XmlUnknownAttributeReference)owned.Parts[2]).Location == nested);
    Check(ReferenceEquals(owned.Default, sink.Defaults[0]) && owned.Sequence == 0);
    Check(owned.Default!.IsExternal && owned.Default.DeclarationLocation == dtd);
    Check(typeof(XmlIncompleteAttributeValue).GetProperty("Value") is null);
    Check(typeof(XmlIncompleteAttributeView).GetProperty("Value") is null);
    Check(typeof(XmlIncompleteAttributeDefaultView).IsByRefLike);
    Check(typeof(XmlIncompleteAttributeView).IsByRefLike);
    Check(typeof(IXmlIncompleteAttributeSink).GetMethods().Length == 2);
    Check(typeof(IXmlEventSink).GetMethods().Length == 10);
    Check(new XmlDocument().IncompleteAttributeDefaults.Count == 0);
    Check(new XmlDocument().IncompleteAttributeValues.Count == 0);
    Check(new XmlDocument().UnresolvedReferences.Count == 0);
    bool immutable = false;
    try { ((IList<XmlAttributeValuePart>)owned.Parts)[0] = new XmlKnownAttributeText("bad", primary); }
    catch (NotSupportedException) { immutable = true; }
    Check(immutable);
    // Independently authored truth table: these checks are policy models only.
    foreach (var row in new (bool Dtd, bool External, bool Pe, bool? Standalone, bool Wfc)[] {
        (false,false,false,null,true), (false,false,false,false,true), (false,false,false,true,true),
        (true,false,false,null,true), (true,false,false,false,true), (true,false,false,true,true),
        (true,false,true,null,false), (true,false,true,false,false), (true,false,true,true,true),
        (true,true,false,null,false), (true,true,false,false,false), (true,true,false,true,true),
        (true,true,true,null,false), (true,true,true,false,false), (true,true,true,true,true) })
    {
        Check(DocumentOriginWfc(row.Dtd,row.External,row.Pe,row.Standalone) == row.Wfc);
        Check(!EntityDeclaredFirstSentence(row.Dtd,row.External,row.Pe,row.Standalone,insideExternalOrPe:true));
    }
    // PE declaration alone, read/unread external subset, and three-value standalone are independent inputs.
    Check(DocumentOriginWfc(true,false,false,null));
    foreach (bool read in new[] { true, false }) { _ = read; Check(!DocumentOriginWfc(true,true,false,null)); }
    Check(ProcessDeclaration(afterUnreadPe:false,standalone:null));
    Check(!ProcessDeclaration(afterUnreadPe:true,standalone:null));
    Check(!ProcessDeclaration(afterUnreadPe:true,standalone:false));
    Check(ProcessDeclaration(afterUnreadPe:true,standalone:true));
    Check(DefaultOrder(direct:true,actuallyDeclaredLater:true,wfc:false) == "DTD.InvalidDefault");
    Check(DefaultOrder(direct:false,actuallyDeclaredLater:true,wfc:false) == "incomplete");
    Check(DefaultOrder(direct:false,actuallyDeclaredLater:true,wfc:true) == "XML.UndeclaredEntity");
    Check(DefaultOrder(direct:true,actuallyDeclaredLater:false,wfc:false) == "incomplete");
    Check(NamespacePolicy(applied:true,incomplete:true) == "XML.UnresolvedNamespaceValue");
    Check(NamespacePolicy(applied:false,incomplete:true) == "provenance only");
    Check(NamespacePolicy(applied:true,incomplete:false) == "resolve normally");
    foreach (string operation in new[] { "SerializeString", "SerializeStream", "Save", "ToXmlString", "TextContent", "CloneShallow", "CloneDeep", "CloneDocument", "ImportSource" })
        foreach (var counts in new[] { (1,0,0), (0,1,0), (0,0,1), (0,0,0) })
        {
            _ = operation; int writes=0, copies=0;
            bool refused = false;
            try { RefuseOwner(counts.Item1,counts.Item2,counts.Item3); writes++; copies++; }
            catch (InvalidOperationException error) { refused = error.Message == "Document contains unresolved entity references."; }
            Check(refused == (counts != (0,0,0)));
            Check(refused ? writes==0 && copies==0 : writes==1 && copies==1);
        }
    Check(AttributeGetter(incomplete:false,known:"  known\t") == "  known\t");
    bool getterRefused=false; try { AttributeGetter(true,"ignored"); } catch (InvalidOperationException e) { getterRefused=e.Message=="Attribute value contains unresolved entity references."; }
    Check(getterRefused);
    Console.WriteLine("Passed " + checks + " contract/model assertions; no product parsing or conformance execution.");
    Console.WriteLine("RuntimeIdentity:" + JsonSerializer.Serialize(new { Version=Environment.Version.ToString(),
        CoreLibrary=typeof(object).Assembly.Location, ProcessArchitecture=RuntimeInformation.ProcessArchitecture.ToString(),
        CoreLibraryInformationalVersion=typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion }));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }

static bool DocumentOriginWfc(bool dtd,bool external,bool pe,bool? standalone) => !dtd || (!external && !pe) || standalone==true;
static bool EntityDeclaredFirstSentence(bool dtd,bool external,bool pe,bool? standalone,bool insideExternalOrPe) => !insideExternalOrPe && DocumentOriginWfc(dtd,external,pe,standalone);
static bool ProcessDeclaration(bool afterUnreadPe,bool? standalone) => !afterUnreadPe || standalone==true;
static string DefaultOrder(bool direct,bool actuallyDeclaredLater,bool wfc) => direct && actuallyDeclaredLater ? "DTD.InvalidDefault" : wfc ? "XML.UndeclaredEntity" : "incomplete";
static string NamespacePolicy(bool applied,bool incomplete) => !incomplete ? "resolve normally" : applied ? "XML.UnresolvedNamespaceValue" : "provenance only";
static void RefuseOwner(int content,int defaults,int values) { if (content+defaults+values>0) throw new InvalidOperationException("Document contains unresolved entity references."); }
static string AttributeGetter(bool incomplete,string known) => incomplete ? throw new InvalidOperationException("Attribute value contains unresolved entity references.") : known;
static string[] PublicShape(Type type)
{
    const BindingFlags flags = BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
    var nulls = new NullabilityInfoContext();
    static string N(Type t) => t.IsByRef ? N(t.GetElementType()!)+"&" : t.IsGenericType ? t.GetGenericTypeDefinition().FullName+"<"+string.Join(",",t.GetGenericArguments().Select(N))+">" : t.FullName!;
    static string Q(NullabilityInfo q) => q.ReadState+"/"+q.WriteState+"["+string.Join(",",q.GenericTypeArguments.Select(Q))+"]"+(q.ElementType is null ? "" : Q(q.ElementType));
    string P(MethodBase m) => string.Join(",",m.GetParameters().Select(p=>N(p.ParameterType)+":"+p.Name+":"+p.IsOptional+":"+p.IsIn+":"+p.IsOut+":"+Q(nulls.Create(p))));
    return new[]{"type:"+type.IsValueType+":"+type.IsSealed+":"+type.IsByRefLike}
        .Concat(type.GetConstructors(flags).Select(m=>"ctor:"+P(m)))
        .Concat(type.GetMethods(flags).Select(m=>"method:"+m.Name+":"+P(m)+":"+N(m.ReturnType)+":"+m.IsStatic+":"+Q(nulls.Create(m.ReturnParameter))+":"+string.Join(",",m.ReturnParameter.GetRequiredCustomModifiers().Select(N))))
        .Concat(type.GetProperties(flags).Select(p=>"property:"+p.Name+":"+N(p.PropertyType)+":"+p.CanRead+":"+p.CanWrite+":"+Q(nulls.Create(p))))
        .Order(StringComparer.Ordinal).ToArray();
}
sealed class Consumer : IXmlIncompleteAttributeSink
{
    public List<XmlIncompleteAttributeDefault> Defaults { get; } = [];
    public List<XmlIncompleteAttributeValue> Values { get; } = [];
    public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView d)
    {
        Defaults.Add(new(d.ElementName.ToString(),d.AttributeName.ToString(),d.LexicalValue.ToString(),d.IsTokenized,d.IsExternal,
            d.DeclarationLocation,d.ValueLocation,Array.AsReadOnly(d.Parts.ToArray()),d.DeclarationSequence)); return false;
    }
    public bool IncompleteAttribute(XmlIncompleteAttributeView a)
    {
        Values.Add(new(default,default,a.LexicalValue.ToString(),a.IsTokenized,a.ValueLocation,
            a.DefaultDeclarationSequence is int n ? Defaults[n] : null,Array.AsReadOnly(a.Parts.ToArray()),Values.Count)); return false;
    }
}
