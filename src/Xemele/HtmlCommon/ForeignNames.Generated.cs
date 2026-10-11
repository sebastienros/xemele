#nullable enable
namespace Xemele.Html;
internal static class HtmlForeignNames
{
 internal const string SvgNamespace="http://www.w3.org/2000/svg";
 internal const string MathMlNamespace="http://www.w3.org/1998/Math/MathML";
 internal static string? AdjustSvgElement(ReadOnlySpan<char> name)
 {
  if(name.Length==6) {
   if(name.SequenceEqual("fetile")) return "feTile";
  }
  if(name.Length==7) {
   if(name.SequenceEqual("feblend")) return "feBlend";
   if(name.SequenceEqual("feflood")) return "feFlood";
   if(name.SequenceEqual("fefunca")) return "feFuncA";
   if(name.SequenceEqual("fefuncb")) return "feFuncB";
   if(name.SequenceEqual("fefuncg")) return "feFuncG";
   if(name.SequenceEqual("fefuncr")) return "feFuncR";
   if(name.SequenceEqual("feimage")) return "feImage";
   if(name.SequenceEqual("femerge")) return "feMerge";
  }
  if(name.Length==8) {
   if(name.SequenceEqual("altglyph")) return "altGlyph";
   if(name.SequenceEqual("clippath")) return "clipPath";
   if(name.SequenceEqual("feoffset")) return "feOffset";
   if(name.SequenceEqual("glyphref")) return "glyphRef";
   if(name.SequenceEqual("textpath")) return "textPath";
  }
  if(name.Length==11) {
   if(name.SequenceEqual("altglyphdef")) return "altGlyphDef";
   if(name.SequenceEqual("fecomposite")) return "feComposite";
   if(name.SequenceEqual("femergenode")) return "feMergeNode";
   if(name.SequenceEqual("fespotlight")) return "feSpotLight";
  }
  if(name.Length==12) {
   if(name.SequenceEqual("altglyphitem")) return "altGlyphItem";
   if(name.SequenceEqual("animatecolor")) return "animateColor";
   if(name.SequenceEqual("fedropshadow")) return "feDropShadow";
   if(name.SequenceEqual("femorphology")) return "feMorphology";
   if(name.SequenceEqual("fepointlight")) return "fePointLight";
   if(name.SequenceEqual("feturbulence")) return "feTurbulence";
  }
  if(name.Length==13) {
   if(name.SequenceEqual("animatemotion")) return "animateMotion";
   if(name.SequenceEqual("fecolormatrix")) return "feColorMatrix";
   if(name.SequenceEqual("foreignobject")) return "foreignObject";
  }
  if(name.Length==14) {
   if(name.SequenceEqual("fedistantlight")) return "feDistantLight";
   if(name.SequenceEqual("fegaussianblur")) return "feGaussianBlur";
   if(name.SequenceEqual("lineargradient")) return "linearGradient";
   if(name.SequenceEqual("radialgradient")) return "radialGradient";
  }
  if(name.Length==16) {
   if(name.SequenceEqual("animatetransform")) return "animateTransform";
   if(name.SequenceEqual("feconvolvematrix")) return "feConvolveMatrix";
  }
  if(name.Length==17) {
   if(name.SequenceEqual("fediffuselighting")) return "feDiffuseLighting";
   if(name.SequenceEqual("fedisplacementmap")) return "feDisplacementMap";
  }
  if(name.Length==18) {
   if(name.SequenceEqual("fespecularlighting")) return "feSpecularLighting";
  }
  if(name.Length==19) {
   if(name.SequenceEqual("fecomponenttransfer")) return "feComponentTransfer";
  }
  return null;
 }
 internal static string? AdjustSvgAttribute(ReadOnlySpan<char> name)
 {
  if(name.Length==4) {
   if(name.SequenceEqual("refx")) return "refX";
   if(name.SequenceEqual("refy")) return "refY";
  }
  if(name.Length==7) {
   if(name.SequenceEqual("targetx")) return "targetX";
   if(name.SequenceEqual("targety")) return "targetY";
   if(name.SequenceEqual("viewbox")) return "viewBox";
  }
  if(name.Length==8) {
   if(name.SequenceEqual("calcmode")) return "calcMode";
   if(name.SequenceEqual("edgemode")) return "edgeMode";
   if(name.SequenceEqual("glyphref")) return "glyphRef";
   if(name.SequenceEqual("keytimes")) return "keyTimes";
  }
  if(name.Length==9) {
   if(name.SequenceEqual("keypoints")) return "keyPoints";
   if(name.SequenceEqual("maskunits")) return "maskUnits";
   if(name.SequenceEqual("pointsatx")) return "pointsAtX";
   if(name.SequenceEqual("pointsaty")) return "pointsAtY";
   if(name.SequenceEqual("pointsatz")) return "pointsAtZ";
   if(name.SequenceEqual("repeatdur")) return "repeatDur";
  }
  if(name.Length==10) {
   if(name.SequenceEqual("keysplines")) return "keySplines";
   if(name.SequenceEqual("numoctaves")) return "numOctaves";
   if(name.SequenceEqual("pathlength")) return "pathLength";
   if(name.SequenceEqual("textlength")) return "textLength";
   if(name.SequenceEqual("viewtarget")) return "viewTarget";
   if(name.SequenceEqual("zoomandpan")) return "zoomAndPan";
  }
  if(name.Length==11) {
   if(name.SequenceEqual("baseprofile")) return "baseProfile";
   if(name.SequenceEqual("filterunits")) return "filterUnits";
   if(name.SequenceEqual("markerunits")) return "markerUnits";
   if(name.SequenceEqual("markerwidth")) return "markerWidth";
   if(name.SequenceEqual("repeatcount")) return "repeatCount";
   if(name.SequenceEqual("startoffset")) return "startOffset";
   if(name.SequenceEqual("stitchtiles")) return "stitchTiles";
   if(name.SequenceEqual("tablevalues")) return "tableValues";
  }
  if(name.Length==12) {
   if(name.SequenceEqual("kernelmatrix")) return "kernelMatrix";
   if(name.SequenceEqual("lengthadjust")) return "lengthAdjust";
   if(name.SequenceEqual("markerheight")) return "markerHeight";
   if(name.SequenceEqual("patternunits")) return "patternUnits";
   if(name.SequenceEqual("spreadmethod")) return "spreadMethod";
   if(name.SequenceEqual("stddeviation")) return "stdDeviation";
   if(name.SequenceEqual("surfacescale")) return "surfaceScale";
  }
  if(name.Length==13) {
   if(name.SequenceEqual("attributename")) return "attributeName";
   if(name.SequenceEqual("attributetype")) return "attributeType";
   if(name.SequenceEqual("basefrequency")) return "baseFrequency";
   if(name.SequenceEqual("clippathunits")) return "clipPathUnits";
   if(name.SequenceEqual("gradientunits")) return "gradientUnits";
   if(name.SequenceEqual("preservealpha")) return "preserveAlpha";
  }
  if(name.Length==14) {
   if(name.SequenceEqual("primitiveunits")) return "primitiveUnits";
   if(name.SequenceEqual("systemlanguage")) return "systemLanguage";
  }
  if(name.Length==15) {
   if(name.SequenceEqual("diffuseconstant")) return "diffuseConstant";
  }
  if(name.Length==16) {
   if(name.SequenceEqual("kernelunitlength")) return "kernelUnitLength";
   if(name.SequenceEqual("maskcontentunits")) return "maskContentUnits";
   if(name.SequenceEqual("patterntransform")) return "patternTransform";
   if(name.SequenceEqual("requiredfeatures")) return "requiredFeatures";
   if(name.SequenceEqual("specularconstant")) return "specularConstant";
   if(name.SequenceEqual("specularexponent")) return "specularExponent";
   if(name.SequenceEqual("xchannelselector")) return "xChannelSelector";
   if(name.SequenceEqual("ychannelselector")) return "yChannelSelector";
  }
  if(name.Length==17) {
   if(name.SequenceEqual("gradienttransform")) return "gradientTransform";
   if(name.SequenceEqual("limitingconeangle")) return "limitingConeAngle";
  }
  if(name.Length==18) {
   if(name.SequenceEqual("requiredextensions")) return "requiredExtensions";
  }
  if(name.Length==19) {
   if(name.SequenceEqual("patterncontentunits")) return "patternContentUnits";
   if(name.SequenceEqual("preserveaspectratio")) return "preserveAspectRatio";
  }
  return null;
 }
 internal static string? AdjustMathMlAttribute(ReadOnlySpan<char> name)
 {
  if(name.Length==13) {
   if(name.SequenceEqual("definitionurl")) return "definitionURL";
  }
  return null;
 }
 internal static string AdjustSvgElement(string name) { return AdjustSvgElement(name.AsSpan()) ?? name; }
 internal static string AdjustSvgAttribute(string name) { return AdjustSvgAttribute(name.AsSpan()) ?? name; }
 internal static string AdjustMathMlAttribute(string name) { return AdjustMathMlAttribute(name.AsSpan()) ?? name; }
 internal static HtmlAttribute AdjustForeignAttribute(HtmlAttribute attribute)
 {
  return attribute.LocalName switch {
   "xlink:actuate" => attribute with { LocalName="actuate", NamespaceUri="http://www.w3.org/1999/xlink", Prefix="xlink" },
   "xlink:arcrole" => attribute with { LocalName="arcrole", NamespaceUri="http://www.w3.org/1999/xlink", Prefix="xlink" },
   "xlink:href" => attribute with { LocalName="href", NamespaceUri="http://www.w3.org/1999/xlink", Prefix="xlink" },
   "xlink:role" => attribute with { LocalName="role", NamespaceUri="http://www.w3.org/1999/xlink", Prefix="xlink" },
   "xlink:show" => attribute with { LocalName="show", NamespaceUri="http://www.w3.org/1999/xlink", Prefix="xlink" },
   "xlink:title" => attribute with { LocalName="title", NamespaceUri="http://www.w3.org/1999/xlink", Prefix="xlink" },
   "xlink:type" => attribute with { LocalName="type", NamespaceUri="http://www.w3.org/1999/xlink", Prefix="xlink" },
   "xml:lang" => attribute with { LocalName="lang", NamespaceUri="http://www.w3.org/XML/1998/namespace", Prefix="xml" },
   "xml:space" => attribute with { LocalName="space", NamespaceUri="http://www.w3.org/XML/1998/namespace", Prefix="xml" },
   "xmlns" => attribute with { LocalName="xmlns", NamespaceUri="http://www.w3.org/2000/xmlns/", Prefix="" },
   "xmlns:xlink" => attribute with { LocalName="xlink", NamespaceUri="http://www.w3.org/2000/xmlns/", Prefix="xmlns" },
   _ => attribute
  };
 }
}
