using Xemele.Validation;

var schema = RelaxNgSchema.Compile("""
    <element xmlns="http://relaxng.org/ns/structure/1.0" name="quantity">
      <data datatypeLibrary="http://www.w3.org/2001/XMLSchema-datatypes"
            type="positiveInteger"/>
    </element>
    """);

foreach (var input in new[] { "<quantity>3</quantity>", "<quantity>0</quantity>" })
{
    var result = schema.Validate(input);
    Console.WriteLine($"{input}: complete={result.IsComplete}, valid={result.IsValid}");
    foreach (var diagnostic in result.Diagnostics)
        Console.WriteLine(diagnostic);
}
