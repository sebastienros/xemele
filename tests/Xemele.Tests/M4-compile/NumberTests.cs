using System.Globalization;
using System.Numerics;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;
namespace Xemele.Tests.M4Compile;
public sealed class NumberTests
{
    [Fact]
    public void BoundedDecimalConversionPreservesExactFrameworkRounding()
    {
        var tiny=BigInteger.Pow(5,1075).ToString(CultureInfo.InvariantCulture);
        string midpoint="0."+new string('0',1075-tiny.Length)+tiny;
        string nearOne="1."+(BigInteger.Pow(5,53)).ToString(CultureInfo.InvariantCulture).PadLeft(53,'0');
        string infinity=((BigInteger.One<<1024)-(BigInteger.One<<970)).ToString(CultureInfo.InvariantCulture);
        var cases=new List<string>{"0","00000.00",".5","5.","1.00001",midpoint,midpoint+new string('0',10000)+"1",nearOne,nearOne+new string('0',10000)+"1",infinity,infinity+".1","0."+new string('0',10000)+"1",new string('9',10000),"000"+midpoint,"000"+nearOne};
        var random=new Random(314159);
        for(int n=0;n<400;n++)
        {
            int size=random.Next(1,3000); var characters=new char[size]; for(int i=0;i<size;i++)characters[i]=(char)('0'+random.Next(10));
            string digits=new(characters); int point=random.Next(size+1);cases.Add(digits[..point]+"."+digits[point..]);
        }
        foreach(string input in cases)
        {
            double expected=double.Parse(input,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
            double actual=XPathCompileNumber.Parse(input,TestContext.Current.CancellationToken);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected),BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.Equal(double.Epsilon,XPathCompileNumber.Parse(midpoint+new string('0',10000)+"1",TestContext.Current.CancellationToken));
        Assert.Equal(0,XPathCompileNumber.Parse(midpoint,TestContext.Current.CancellationToken));
        var ast=Assert.IsType<XPathLiteral>(new XPathCompiler().Compile(nearOne+new string('0',10000)+"1",XPathStaticContext.FromOptions(null,TestContext.Current.CancellationToken),TestContext.Current.CancellationToken));
        Assert.Equal(Math.BitIncrement(1),ast.Value.AsNumber());
    }
}
