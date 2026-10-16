using Xemele.Validation;

var schema = XmlSchema.Compile("""
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
      <xs:element name="quantity" type="xs:positiveInteger"/>
    </xs:schema>
    """);

foreach (var input in new[] { "<quantity>3</quantity>", "<quantity>0</quantity>" })
{
    var result = schema.Validate(input);
    Console.WriteLine($"{input}: complete={result.IsComplete}, valid={result.IsValid}");
    foreach (var diagnostic in result.Diagnostics)
        Console.WriteLine(diagnostic);
}
