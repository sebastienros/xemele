using Xemele;
using Xemele.Validation;
var schema = RelaxNgSchema.Compile("<element xmlns='http://relaxng.org/ns/structure/1.0' name='order'><attribute name='id'><data datatypeLibrary='http://www.w3.org/2001/XMLSchema-datatypes' type='positiveInteger'/></attribute><oneOrMore><element name='item'><text/></element></oneOrMore></element>");
var instance = XmlDocument.Parse("<order id='1'><item>Tea</item></order>");
string snapshot = instance.ToXmlString();
if (!schema.Validate(instance).IsValid || schema.Validate("<order id='0'><item>Tea</item></order>").IsValid || snapshot != instance.ToXmlString()) throw new Exception("Consumer expectations failed.");
Console.WriteLine("PASS: immutable RELAX NG schema; valid order accepted; invalid positiveInteger rejected; instance unchanged.");
