"""Reproduce frozen factual range tables from downloaded normative sources (ignored artifacts)."""
from pathlib import Path
import re,hashlib,json
p=Path('artifacts/M8-regex'); xml=(p/'xml').read_text()
def ranges(name):
 s=xml[xml.index('name="NT-'+name+'"'):];s=s[:s.index('</tr>')]
 return [(int(a,16),int(b or a,16)) for a,b in re.findall(r'#x([0-9A-Fa-f]+)(?:-#x([0-9A-Fa-f]+))?',s)]
def merge(rs):
 out=[]
 for a,b in sorted(rs):
  if out and a<=out[-1][1]+1:out[-1]=(out[-1][0],max(b,out[-1][1]))
  else:out.append((a,b))
 return out
start=merge(ranges('BaseChar')+ranges('Ideographic')+[(58,58),(95,95)])
name=merge(start+ranges('CombiningChar')+ranges('Digit')+ranges('Extender')+[(45,46)])
blocks=[]
for line in (p/'blocks').read_text().splitlines():
 m=re.match(r'([0-9A-F]+)\.\.([0-9A-F]+); (.+)',line)
 if m and 'Surrogates' not in m[3]: blocks.append((m[3].replace(' ',''),int(m[1],16),int(m[2],16)))
blocks += [('Greek',0x370,0x3ff),('CombiningMarksforSymbols',0x20d0,0x20ff),('PrivateUse',0xe000,0xf8ff)]
categories=['Lu','Ll','Lt','Lm','Lo','Mn','Mc','Me','Nd','Nl','No','Zs','Zl','Zp','Cc','Cf','Cs','Co','Pc','Pd','Ps','Pe','Pi','Pf','Po','Sm','Sc','Sk','So','Cn']
code='''using System.Text;
namespace Xemele.Validation;
// Unicode 17 blocks; Unicode categories follow the pinned .NET runtime's database.
// XML name escapes deliberately use the referenced XML 1.0 Second Edition productions.
internal static class RegexCharacters
{
    internal static bool IsXml(int c) => c is 9 or 10 or 13 or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;
    internal static uint Category(ReadOnlySpan<char> name) => name switch
    {
'''
for cat in [x for x in categories if x!='Cs']+[ 'L','M','N','P','Z','S','C']:
 mask=sum(1<<i for i,x in enumerate(categories) if x==cat or len(cat)==1 and x.startswith(cat) and x!='Cs')
 code+=f'        "{cat}" => 0x{mask:X}u,\n'
code+='''        _ => 0
    };
    internal static bool Block(ReadOnlySpan<char> name, ISchemaOperation operation, XmlLocation at, out int first, out int last)
    {
        foreach (var block in Blocks)
        {
            if (!operation.Charge(1, at)) throw SchemaSnapshots.Failure(operation);
            if (name.SequenceEqual(block.Name)) { first = block.First; last = block.Last; return true; }
        }
        first = last = 0; return false;
    }
    private static readonly (string Name, int First, int Last)[] Blocks =
    [
'''
for n,a,b in blocks:code+=f'        ("{n}", 0x{a:X}, 0x{b:X}),\n'
code+='''    ];
    internal static bool IsName(int c, bool continuation, ISchemaOperation operation, XmlLocation at)
    {
        var ranges = continuation ? NameRanges : StartRanges;
        int lo = 0, hi = ranges.Length / 2 - 1;
        while (lo <= hi)
        {
            if (!operation.Charge(1, at)) return false;
            int mid = lo + (hi - lo) / 2;
            if (c < ranges[mid * 2]) hi = mid - 1;
            else if (c > ranges[mid * 2 + 1]) lo = mid + 1;
            else return true;
        }
        return false;
    }
'''
for n,rs in [('StartRanges',start),('NameRanges',name)]:
 code+=f'    private static ReadOnlySpan<int> {n} =>\n    [\n'
 for a,b in rs:code+=f'        0x{a:X}, 0x{b:X},\n'
 code+='    ];\n'
code+='}\n';Path('src/Xemele/Validation/Regex/Characters.cs').write_text(code)
Path('tests/fixtures/M8-regex/sources.json').write_text(json.dumps({'sources':[{'url':u,'sha256':hashlib.sha256((p/f).read_bytes()).hexdigest()} for f,u in [('xml','https://www.w3.org/TR/2000/REC-xml-20001006'),('blocks','https://www.unicode.org/Public/17.0.0/ucd/Blocks.txt')]],'blockCount':len(blocks),'nameStartRanges':len(start),'nameCharRanges':len(name)},indent=2)+'\n')
