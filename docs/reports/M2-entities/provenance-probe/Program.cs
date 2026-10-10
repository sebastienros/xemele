using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Buffers.Binary;

var result = args.Select(path =>
{
    string full = Path.GetFullPath(path);
    using var stream = File.OpenRead(full);
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    string? sourceVersion = null;
    foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
    {
        var attribute = reader.GetCustomAttribute(handle);
        if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
        var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        if (member.Parent.Kind != HandleKind.TypeReference) continue;
        var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (reader.GetString(type.Namespace) != "System.Reflection" || reader.GetString(type.Name) != "AssemblyInformationalVersionAttribute") continue;
        var blob = reader.GetBlobReader(attribute.Value);
        if (blob.ReadUInt16() != 1) throw new InvalidDataException("Invalid assembly version attribute.");
        sourceVersion = blob.ReadSerializedString();
    }
    using var il = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    byte[] length = new byte[4];
    foreach (var handle in reader.MethodDefinitions)
    {
        var method = reader.GetMethodDefinition(handle);
        if (method.RelativeVirtualAddress == 0) continue;
        byte[] body = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
        BinaryPrimitives.WriteInt32LittleEndian(length, body.Length);il.AppendData(length);il.AppendData(body);
    }
    return new { path = full, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant(),
        sourceVersion, mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid), ilSha256 = Convert.ToHexString(il.GetHashAndReset()).ToLowerInvariant(),
        runtime = Environment.Version.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        coreLibrary = typeof(object).Assembly.Location,
        coreLibrarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(object).Assembly.Location))).ToLowerInvariant() };
});
Console.WriteLine(JsonSerializer.Serialize(result));
