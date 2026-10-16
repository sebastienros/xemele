using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xemele.ReferenceProtocol;

public sealed record Request(int ProtocolVersion, string CaseId, string Operation, string InputBase64,
    string? EncodingHint, string Profile, Dictionary<string, JsonElement>? Options,
    Dictionary<string, string>? OfflineResources);
public sealed record Diagnostic(string Category, string Message);
public sealed record Engine(string Name, string Version, string Runtime, string Build);
public sealed record Response(int ProtocolVersion, string? CaseId, string? Operation, Engine Engine,
    string Outcome, Diagnostic[] Diagnostics, Event[]? Events);
public sealed record AttributeValue(string NamespaceUri, string LocalName, string Value);
public sealed record Event(string Kind, string? NamespaceUri = null, string? LocalName = null,
    string? Value = null, AttributeValue[]? Attributes = null);
public static class Wire
{
    public const int Version = 1;
    public const int MaximumLineCharacters = 1_048_576;
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
