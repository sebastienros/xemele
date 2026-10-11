using System.Text;
using Xemele.Resolution.XInclude;
using Xunit;
namespace Xemele.Tests.M6XInclude;
public sealed class ContinuationTests
{
    private const string Xi = "http://www.w3.org/2001/XInclude";
    private sealed class ChainResolver(int count) : XmlResourceResolver
    {
        internal int Disposals;
        internal int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls++;
            int at = int.Parse(request.Uri.AbsolutePath.AsSpan(1));
            string xml = at == count ? "<done/>" : $"<xi:include xmlns:xi='{Xi}' href='{at + 1}'/>";
            return new(new Content(Encoding.UTF8.GetBytes(xml), this), request.Uri);
        }
        private sealed class Content(byte[] bytes, ChainResolver owner) : MemoryStream(bytes)
        {
            protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); }
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DeepSynchronousCompletionChainUsesContinuationFrames(bool asynchronous)
    {
        const int count = 4096;
        var input = XmlDocument.Parse($"<r xmlns:xi='{Xi}'><xi:include href='1'/></r>", cancellationToken: TestContext.Current.CancellationToken);
        long revision = input.ContentRevision; var resolver = new ChainResolver(count);
        var unlimited = new XmlResourceLimits { InclusionDepth = null, ExternalResources = null, InternedNames = null };
        var options = new XIncludeOptions { BaseUri = new("https://test/main"), Resolver = resolver, Limits = unlimited, ParseOptions = new() { Limits = unlimited }, FixupBaseUris = false };
        var result = asynchronous ? await XIncludeProcessor.ProcessAsync(input, options, TestContext.Current.CancellationToken) : XIncludeProcessor.Process(input, options, TestContext.Current.CancellationToken);
        Assert.True(result.Success); Assert.Equal("done", result.Document!.RootElement.FirstChild.LocalName);
        Assert.Equal(count, result.IncludedResources); Assert.Equal(count, resolver.Disposals); Assert.Equal(count, resolver.Calls);
        Assert.Equal(revision, input.ContentRevision); Assert.Equal("include", input.RootElement.FirstChild.LocalName);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void PointerCancellationInterruptsWorkAfterEntry(bool shorthand)
    {
        var document = new XmlDocument(); var source = new CancellationTokenSource();
        var operation = new IncludeOperation(document, null, source.Token, false);
        int checkpoints = 0;
        operation.PointerCheckpointObserver = () => { if (++checkpoints == 4) source.Cancel(); };
        string expression = shorthand ? new string('a', 65536) : "element(" + string.Concat(Enumerable.Repeat("/1", 8192)) + ")";
        var exception = Assert.Throws<OperationCanceledException>(() => operation.ParsePointer(expression));
        Assert.Equal(source.Token, exception.CancellationToken); Assert.Equal(4, checkpoints);
    }
    [Fact] public void CanonicalPathCancellationInterruptsWorkAfterEntry()
    {
        var document = new XmlDocument(new() { Depth = null }); var node = document.CreateElement("r"); document.DocumentNode.AppendChild(node);
        for (int i = 0; i < 1024; i++) { var child = document.CreateElement("n"); node.AppendChild(child); node = child; }
        long revision = document.ContentRevision; var source = new CancellationTokenSource();
        var operation = new IncludeOperation(document, null, source.Token, false); int checkpoints = 0;
        operation.PointerCheckpointObserver = () => { if (++checkpoints == 4) source.Cancel(); };
        Assert.Throws<OperationCanceledException>(() => operation.Path(node)); Assert.Equal(4, checkpoints); Assert.Equal(revision, document.ContentRevision);
    }
}
