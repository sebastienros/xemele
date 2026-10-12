using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdIdentity;

internal sealed class IdentityFixture
{
    internal const string Source="""
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
<xs:element name="root"><xs:keyref name="R" refer="K"><xs:selector xpath=".//use"/><xs:field xpath="@id"/></xs:keyref></xs:element>
<xs:element name="group"><xs:key name="K"><xs:selector xpath="row"/><xs:field xpath="@id"/></xs:key></xs:element>
</xs:schema>
""";
    internal const string Instance="<root><group><row id='01'/><row id='2'/></group><use id='1'/></root>";
    internal const string Invalid="<root><group><row id='01'/><row id='1'/></group><use id='1'/></root>";
    internal static readonly string LongName="根😀"+new string('x',512);
    internal static readonly string LongInstance=$"<root xmlns:p='urn:long'><p:{LongName}/><p:other/></root>";
    internal static readonly string[] Routes=["restricted match hit","restricted match miss","typed equality","long multilingual namespace match hit","long multilingual namespace match miss","identity preparation","owned hook validation","invalid hooks and diagnostics","session setup"];
    private readonly XsdCompiledSchema schema;
    private readonly XsdIdentityProgram program;
    private readonly XmlDocument instance,invalid;
    private readonly Event[] validEvents,invalidEvents;
    private readonly IdentityXPath path;
    private readonly IdentityXPath longPath;
    private readonly XmlDocument names;
    private readonly XmlDatatypeValue one;
    private readonly SchemaCompileOperation primitive=new(new() { MaxOperations=null,Limits=new() { EvaluationWork=null } });
    private long consumed;
    private readonly record struct Event(XmlNode Node,bool Enter,XsdElementDeclaration? Declaration,XsdValidatedValue? Attribute);
    internal IdentityFixture()
    {
        var compile=new SchemaCompileContext();schema=new XmlSchemaCompiler().Compile(compile.Parse(Source).Document,compile.Options,compile.Operation);
        program=new XsdIdentityCompiler().Compile(schema,compile.Operation);
        var registry=new XmlDatatypeRegistry(schema.SimpleTypes.Where(d => d.Name.NamespaceUri != "http://www.w3.org/2001/XMLSchema"),compile.Operation);var library=registry.Find(DatatypeTypes.XsdNamespace)!;
        var context=new SchemaNamespaceContext(new Dictionary<string,string>(),"",null);
        if(!library.TryParse(DatatypeTypes.Name("integer"),"1",context,compile.Operation,out var value,out _))throw new InvalidOperationException();one=value!;
        instance=XmlDocument.Parse(Instance);invalid=XmlDocument.Parse(Invalid);
        validEvents=Events(instance);invalidEvents=Events(invalid);
        path=IdentityXPath.Compile(".//row",false,context,compile.Operation,default);
        names=XmlDocument.Parse(LongInstance);
        longPath=IdentityXPath.Compile("p:"+LongName,false,new(new Dictionary<string,string> { ["p"]="urn:long" },"",null),compile.Operation,default);
        Event[] Events(XmlDocument doc)
        {
            var events=new List<Event>();
            Visit(doc.RootElement);return events.ToArray();
            void Visit(XmlNode node)
            {
                schema.GlobalElements.TryGetValue(new(node.NamespaceUri,node.LocalName),out int id);
                var declaration=schema.GlobalElements.ContainsKey(new(node.NamespaceUri,node.LocalName))?schema.Elements[id]:null;
                events.Add(new(node,true,declaration,null));
                if(node.GetAttribute("id") is {} lexical)
                {
                    if(!library.TryParse(DatatypeTypes.Name("integer"),lexical,context,compile.Operation,out var typed,out _))throw new InvalidOperationException();
                    events.Add(new(node,false,null,new(true,DatatypeTypes.Name("integer"),typed)));
                }
                foreach(var child in node.Children) if(child.Kind==XmlNodeKind.Element)Visit(child);
                events.Add(new(node,false,null,null));
            }
        }
    }
    internal XsdIdentityProgram NewProgram()=>new XsdIdentityCompiler().Compile(schema,new SchemaCompileOperation());
    internal IXsdIdentitySession NewInitializedSession()
    {
        var session=new XsdIdentityFactory().Create(schema,program,instance);
        var op=new SchemaValidationOperation();session.EnterElement(instance.RootElement,schema.Elements[schema.GlobalElements[new("","root")]],op);return session;
    }
    internal void Run(int route)
    {
        switch(route)
        {
            case 0: if(!path.Matches(instance.RootElement,instance.RootElement.FirstChild.FirstChild,null,primitive,default))throw new InvalidOperationException();consumed++;break;
            case 1: if(path.Matches(instance.RootElement,instance.RootElement.FirstChild,null,primitive,default))throw new InvalidOperationException();consumed++;break;
            case 2: if(!DatatypeValues.ValueEquals(one,one,primitive))throw new InvalidOperationException();consumed++;break;
            case 3: if(!longPath.Matches(names.RootElement,names.RootElement.FirstChild,null,primitive,default))throw new InvalidOperationException();consumed++;break;
            case 4: if(longPath.Matches(names.RootElement,names.RootElement.LastChild,null,primitive,default))throw new InvalidOperationException();consumed++;break;
            case 5: GC.KeepAlive(NewProgram());consumed++;break;
            case 6: Validate(instance,validEvents,true);break;
            case 7: Validate(invalid,invalidEvents,false);break;
            case 8: GC.KeepAlive(NewInitializedSession());consumed++;break;
            default:throw new ArgumentOutOfRangeException(nameof(route));
        }
    }
    private void Validate(XmlDocument doc,Event[] events,bool expected)
    {
        var session=new XsdIdentityFactory().Create(schema,program,doc);var op=new SchemaValidationOperation();
        foreach(var e in events)
        {
            if(e.Enter)session.EnterElement(e.Node,e.Declaration,op);
            else if(e.Attribute is {} value)session.Attribute(e.Node,new("","id"),value,op);
            else session.LeaveElement(e.Node,new(true,DatatypeTypes.Name("anyType"),null),op);
        }
        session.Complete(op);var result=op.Complete();if(!result.IsComplete || result.IsValid!=expected)throw new InvalidOperationException("Independent identity outcome failed.");
        consumed+=1+result.Diagnostics.Count;
    }
    internal long Consume()=>consumed;
}
