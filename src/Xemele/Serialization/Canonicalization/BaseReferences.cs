using System.Text;
namespace Xemele.Serialization;

// C14N 1.1 section 2.4: RFC3986 joining with relative bases, preserved leading
// ../, collapsed slashes and fragments removed. Owned strings/segment lists
// exist only for synthesized xml:base attributes during preflight.
internal static class BaseReferences
{
    private sealed record Parts(string? Scheme, string? Authority, string Path, string? Query);
    internal static string Join(string basis, string reference, Action<long> charge)
    {
        var b = Parse(basis, charge); var r = Parse(reference, charge);
        string? scheme, authority, query; string path;
        if (r.Scheme is not null) { scheme = r.Scheme; authority = r.Authority; path = Normalize(r.Path, charge); query = r.Query; }
        else
        {
            scheme = b.Scheme;
            if (r.Authority is not null) { authority = r.Authority; path = Normalize(r.Path, charge); query = r.Query; }
            else
            {
                authority = b.Authority;
                if (r.Path.Length == 0) { path = b.Path; query = r.Query ?? b.Query; }
                else
                {
                    if (r.Path[0] == '/') path = Normalize(r.Path, charge);
                    else
                    {
                        string basePath = b.Path;
                        if (basePath == ".." || basePath.EndsWith("/..", StringComparison.Ordinal)) basePath += "/";
                        int slash = basePath.LastIndexOf('/'); charge(basePath.Length);
                        string prefix = slash < 0 ? (b.Authority is not null ? "/" : "") : basePath[..(slash + 1)];
                        path = Normalize(string.Concat(prefix, r.Path), charge);
                    }
                    query = r.Query;
                }
            }
        }
        return string.Concat(scheme is null ? "" : scheme + ":", authority is null ? "" : "//" + authority, path, query is null ? "" : "?" + query);
    }
    private static Parts Parse(string value, Action<long> charge)
    {
        int length = value.Length, hash = length, query = length, scheme = -1;
        for (int i = 0; i < length; i++) { charge(1); if (value[i] == '#') { hash = i; break; } }
        for (int i = 0; i < hash; i++) { charge(1); if (value[i] == '?') { query = i; break; } }
        int end = Math.Min(hash, query);
        for (int i = 0; i < end; i++)
        {
            charge(1); char c = value[i];
            if (c == ':' && i > 0) { scheme = i; break; }
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' || i > 0 && c is >= '0' and <= '9' or '+' or '-' or '.')) break;
        }
        int start = scheme < 0 ? 0 : scheme + 1;
        string? authority = null;
        if (end - start >= 2 && value[start] == '/' && value[start + 1] == '/')
        {
            int a = start + 2; start = a;
            while (start < end && value[start] != '/') { charge(1); start++; }
            authority = value[a..start];
        }
        return new(scheme < 0 ? null : value[..scheme], authority, value[start..end], query < hash ? value[(query + 1)..hash] : null);
    }
    private static string Normalize(string path, Action<long> charge)
    {
        bool absolute = path.StartsWith('/');
        bool trailing = path.EndsWith('/') || path.EndsWith("/.", StringComparison.Ordinal) || path.EndsWith("/..", StringComparison.Ordinal) || path is "." or "..";
        var segments = new List<string>();
        for (int start = 0; start < path.Length;)
        {
            int end = start; while (end < path.Length && path[end] != '/') { charge(1); end++; }
            ReadOnlySpan<char> segment = path.AsSpan(start, end - start); charge(1);
            if (segment.SequenceEqual(".."))
            {
                if (segments.Count > 0 && segments[^1] != "..") segments.RemoveAt(segments.Count - 1);
                else if (!absolute) segments.Add("..");
            }
            else if (!segment.IsEmpty && !segment.SequenceEqual(".")) segments.Add(new string(segment));
            start = end + 1;
        }
        var output = new StringBuilder();
        if (absolute) output.Append('/');
        for (int i = 0; i < segments.Count; i++) { charge(1); if (i != 0) output.Append('/'); output.Append(segments[i]); }
        if (trailing && segments.Count != 0) output.Append('/');
        return output.ToString();
    }
}
