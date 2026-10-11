#!/usr/bin/env python3
"""Generate pinned built-in charset aliases and immutable single-byte maps."""
import pathlib,re,sys
root=pathlib.Path(sys.argv[1]);s=(root/'encoding.c').read_text();maps=(root/'codegen/charset.inc').read_text();groups={}
ids={'UTF8':1,'UTF16LE':2,'UTF16BE':3,'UTF16':2,'ASCII':4,'8859_1':0,'WINDOWS_1252':0}
for n in [2,3,4,5,6,7,8,9,10,11,13,14,15,16]:ids['8859_'+str(n)]=n+10
for name,enum in re.findall(r'\{ "([^"]+)", XML_CHAR_ENCODING_([A-Z0-9_]+) \}',s):
 if enum in ids:groups.setdefault(ids[enum],[]).append(name)
o='// Derived from libxml2 encoding.c/codegen/charset.inc at 96498992efa48d52b0e8b83058bd88dbdaf153c1 (MIT).\nnamespace Xemele.Html;\ninternal static class LegacyCharsets\n{\n internal static int Label(ReadOnlySpan<char> label)\n {\n  if(label.Length>32)return -1;Span<char> folded=stackalloc char[32];for(int i=0;i<label.Length;i++){char c=label[i];folded[i]=c is >=\'A\' and <=\'Z\'?(char)(c+32):c;}var s=folded[..label.Length];\n'
for num,names in groups.items():o+='  if('+ '||'.join('s.SequenceEqual("'+n+'")' for n in names)+')return '+str(num)+';\n'
o+='  return -1;\n }\n internal static int Map(int encoding,int value)\n {\n  if(value<128)return value;return encoding switch\n  {\n'
for n in [2,3,4,5,6,7,8,9,10,11,13,14,15,16]:o+='   '+str(n+10)+' => Iso'+str(n)+'[value-128],\n'
o+='   _ => LegacyParser.MapCodePoint(value)\n  };\n }\n'
for name,data in re.findall(r'xmlunicodetable_ISO8859_(\d+) \[128\] = \{(.*?)\};',maps,re.S):
 nums=re.findall(r'0x[0-9a-f]+',data);o+=' private static ReadOnlySpan<ushort> Iso'+name+' => ['+','.join(nums)+'];\n'
o+='}\n';pathlib.Path('src/Xemele/Html/Legacy/LegacyCharsets.Generated.cs').write_text(o)
