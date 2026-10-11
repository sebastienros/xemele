#!/usr/bin/env python3
"""Deterministically regenerate borrowed immutable tables from pinned owned JSON."""
import json,pathlib
root=pathlib.Path(__file__).resolve().parents[3]
fixture=pathlib.Path(__file__).resolve().parent
out=root/'src/Xemele/HtmlCommon'
q=lambda s:json.dumps(s,ensure_ascii=True)
entries=sorted((k[1:],v['characters']) for k,v in json.loads((fixture/'entities.json').read_text()).items())
p=out/'NamedReferences.Generated.cs'
# Keep reviewed matcher implementation; replace only deterministic data segment.
s=p.read_text();start=s.index(' private static readonly string[] Names = [');end=s.index(' internal static bool TryGet',start)
block=' private static readonly string[] Names = [\n'+''.join('  '+q(k)+',\n' for k,v in entries)+' ];\n private static readonly string[] Values = [\n'+''.join('  '+q(v)+',\n' for k,v in entries)+' ];\n'
p.write_text(s[:start]+block+s[end:])
f=json.loads((fixture/'foreign.json').read_text())
lines=['#nullable enable','namespace Xemele.Html;','internal static class HtmlForeignNames','{',' internal const string SvgNamespace="http://www.w3.org/2000/svg";',' internal const string MathMlNamespace="http://www.w3.org/1998/Math/MathML";']
for label,table in [('AdjustSvgElement',f['svgElements']),('AdjustSvgAttribute',f['svgAttributes']),('AdjustMathMlAttribute',f['mathmlAttributes'])]:
 lines+=[' internal static string? '+label+'(ReadOnlySpan<char> name)',' {']
 for length in sorted(set(map(len,table))):
  lines+=['  if(name.Length=='+str(length)+') {']
  lines+=['   if(name.SequenceEqual('+q(k)+')) return '+q(v)+';' for k,v in table.items() if len(k)==length];lines+=['  }']
 lines+=['  return null;',' }']
for label in ['AdjustSvgElement','AdjustSvgAttribute','AdjustMathMlAttribute']:
 lines+=[' internal static string '+label+'(string name) { return '+label+'(name.AsSpan()) ?? name; }']
lines+=[' internal static HtmlAttribute AdjustForeignAttribute(HtmlAttribute attribute)',' {','  return attribute.LocalName switch {']
for key,(local,ns,prefix) in f['foreignAttributes'].items():lines+=['   '+q(key)+' => attribute with { LocalName='+q(local)+', NamespaceUri='+q(ns)+', Prefix='+q(prefix)+' },']
lines+=['   _ => attribute','  };',' }','}'];(out/'ForeignNames.Generated.cs').write_text('\n'.join(lines)+'\n')
