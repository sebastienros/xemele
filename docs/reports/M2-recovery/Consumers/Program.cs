using System.Reflection;
using System.Security.Cryptography;
using Xemele;
using var stream = new MemoryStream("<r>"u8.ToArray());
Usage.Recovery.Use(stream);
var result = XmlDocument.ParseWithDiagnostics("<r>", new() { Recover = true });
if (!result.IsComplete || result.IsWellFormed || result.Value?.RootElement.LocalName != "r" || result.Diagnostics.Count != 1) throw new Exception("recovery consumer oracle");
var assembly = typeof(XmlDocument).Assembly;
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { result = "passed", api = "frozen four-factory usage plus runtime result oracle", assembly = assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant(), revision = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion }));
