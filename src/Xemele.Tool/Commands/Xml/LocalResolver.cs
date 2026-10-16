namespace Xemele.Tool;
// Invocation file access is separate from this deliberately narrow resource grant.
internal sealed class LocalResolver : XmlResourceResolver
{
    private readonly string? root;
    internal LocalResolver(string? allowedRoot)
    {
        if (allowedRoot is null)
        {
            return;
        }

        try
        {
            root = RealPath(allowedRoot);
            if (!Directory.Exists(root))
            {
                throw new InvocationException("--allow-root must name an existing directory.");
            }
        }
        catch (IOException ex) { throw new InvocationException(ex.Message); }
        catch (UnauthorizedAccessException ex) { throw new InvocationException(ex.Message); }
        catch (ArgumentException ex) { throw new InvocationException(ex.Message); }
        catch (NotSupportedException ex) { throw new InvocationException(ex.Message); }
    }
    internal static string RealPath(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null)
            {
                current = info.ResolveLinkTarget(true)?.FullName ?? throw new IOException("Unresolved symbolic link.");
            }
        }
        return Path.TrimEndingDirectorySeparator(current);
    }
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (root is null || !request.Uri.IsFile || request.Uri.Host.Length != 0 && request.Uri.Host != "localhost" || request.Uri.Query.Length != 0 || request.Uri.Fragment.Length != 0)
        {
            return null;
        }

        try
        {
            var path = RealPath(request.Uri.LocalPath);
            var relative = Path.GetRelativePath(root, path);
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return null;
            }

            return new XmlResolvedResource(new GuardedFileInput(File.OpenRead(path), false), new Uri(path));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
