using Xemele.ReferenceProtocol;

AttributeValue[] inputs = [new("urn:\uE000", "x", "bmp-ns"), new("urn:\U00010000", "x", "supplementary-ns"),
    new("", "\uE000", "bmp-name"), new("", "\U00010000", "supplementary-name"), new("", "é", "accent"), new("", "z", "ascii")];
string[] actual = AttributeNormalization.Sort(inputs).Select(a => a.Value).ToArray();
string[] expected = ["ascii", "accent", "supplementary-name", "bmp-name", "supplementary-ns", "bmp-ns"];
if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("Production sorter does not use UTF16 ordinal ordering.");
Console.WriteLine("Production UTF16 ordinal sorter passed: ASCII, accent, supplementary vs BMP in namespace and local name.");

byte[] minimal = System.Text.Encoding.UTF8.GetBytes("<r/>");
if (DocumentEnvelope.UnsupportedReason(minimal, null, "unknown-native-decoder") is null)
    throw new InvalidOperationException("Ambiguous native encoding must not claim success.");
if (DocumentEnvelope.UnsupportedReason(minimal, null, "UTF-8") is not null)
    throw new InvalidOperationException("Minimal supported envelope failed.");
Console.WriteLine("Ambiguous native encoding fails explicitly; supported minimal envelope passes.");
