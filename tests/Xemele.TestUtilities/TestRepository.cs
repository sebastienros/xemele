namespace Xemele.TestUtilities;

public static class TestRepository
{
    public static string Root
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Xemele.slnx")))
                {
                    return directory.FullName;
                }
            }
            throw new InvalidOperationException("Tests require the repository checkout.");
        }
    }

    public static string CorpusResources => Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES")
        ?? Path.Combine(Root, "tests", "corpora", "xml");
}
