using System.Reflection;
using System.Text;
using System.Xml;
using Xemele.Validation;
using Xemele.Serialization;
using Xunit;
namespace Xemele.Differential.Tests.DtdCanonical;
public sealed class ReferenceTests
{
    [Theory]
    [InlineData("<!ELEMENT r EMPTY>","<r/>",true)]
    [InlineData("<!ELEMENT r EMPTY>","<r> </r>",false)]
    [InlineData("<!ELEMENT r EMPTY>","<r><!--c--></r>",false)]
    [InlineData("<!ELEMENT r (a+)><!ELEMENT a EMPTY>","<r><a/></r>",true)]
    [InlineData("<!ELEMENT r (a+)><!ELEMENT a EMPTY>","<r/>",false)]
    [InlineData("<!ELEMENT r EMPTY><!ATTLIST r a CDATA #REQUIRED>","<r/>",false)]
    [InlineData("<!ELEMENT r EMPTY><!ATTLIST r a (x|y) #IMPLIED>","<r a='x'/>",true)]
    [InlineData("<!ELEMENT r EMPTY><!ATTLIST r a (x|y) #IMPLIED>","<r a='z'/>",false)]
    public void AuthoredDtdValidityMatchesBclAndPinnedExpectation(string dtd,string body,bool expected)
    {
        string xml="<!DOCTYPE r ["+dtd+"]>"+body;
        var product=Xemele.XmlDocument.Parse(xml,new(){DtdProcessing=XmlDtdProcessing.Parse},TestContext.Current.CancellationToken);
        var result=DtdSchema.FromDocument(product).Validate(product,new(){CancellationToken=TestContext.Current.CancellationToken});Assert.True(result.IsComplete);Assert.Equal(expected,result.IsValid);
        bool valid=true;var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Parse,ValidationType=ValidationType.DTD,XmlResolver=null};settings.ValidationEventHandler+=(_,e)=>{if(e.Severity==System.Xml.Schema.XmlSeverityType.Error)valid=false;};using var reader=XmlReader.Create(new StringReader(xml),settings);while(reader.Read()){}Assert.Equal(expected,valid);
    }
    [Theory]
    [InlineData(0,false)] [InlineData(0,true)] [InlineData(2,false)] [InlineData(2,true)]
    public void CommonCanonicalBytesMatchIndependentFrameworkAlgorithm(int mode,bool comments)
    {
        const string xml="<?p?><!--outer--><r xmlns:p='urn:p' z='2' a='1'><p:x>根😀&amp;&lt;&gt;&#xD;</p:x><!--inner--></r>";
        using var output=new MemoryStream();XmlCanonicalizer.Canonicalize(Xemele.XmlDocument.Parse(xml,cancellationToken:TestContext.Current.CancellationToken),output,new(){Mode=(XmlCanonicalizationMode)mode,IncludeComments=comments,CancellationToken=TestContext.Current.CancellationToken});
        Assert.Equal(ReferenceAlgorithm.Reference(xml,mode,comments),output.ToArray());
    }
}
