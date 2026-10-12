using Xemele;
using Xemele.Validation;

const string ns = "http://relaxng.org/ns/structure/1.0";
string Root(string attributes, string name = "r") => "<element xmlns='" + ns + "' name='" + name + "' " + attributes + "><empty/></element>";
int consumed = 0;
foreach (string attributes in new[] { "", "datatypeLibrary='http:ok'", "datatypeLibrary='foo:é space'", "datatypeLibrary='foo://[::1]/'" })
{
    var schema = RelaxNgSchema.Compile(Root(attributes));
    if (!schema.Validate("<r/>").IsValid) throw new Exception("positive");
    consumed++;
}
foreach (string library in new[] { "foo:", "foo:%", "foo:%xx", "foo:x#f" })
{
    try { RelaxNgSchema.Compile(Root("datatypeLibrary='" + library + "'")); throw new Exception("accepted URI"); }
    catch (SchemaCompileException e) { if (e.Diagnostics[0].Code != "RNG.SchemaRestriction") throw; consumed++; }
}
try { RelaxNgSchema.Compile(Root("", "ี")); throw new Exception("accepted name"); }
catch (SchemaCompileException e) { if (e.Diagnostics[0].Code != "RNG.SchemaRestriction") throw; consumed++; }
if (!RelaxNgSchema.Compile(Root("", "aี")).Validate("<aี/>").IsValid) throw new Exception("continuation");
consumed++;
if (consumed != 10) throw new Exception("consumer count");
Console.WriteLine("PASS: 10 consumed lexical/public compile-validation routes.");
