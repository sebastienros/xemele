using Xemele.Streaming;
using Xunit;
namespace Xemele.Tests.Callbacks;

public sealed class AllocationTests(ITestOutputHelper output)
{
    [Fact]
    public void WarmSixteenNotificationsAndCollectionIterationAllocateZero()
    {
        var counter = new Counter();
        IXmlSaxHandler[] handlers = [counter, new XmlSaxCallbacks
        {
            OnStartDocument = counter.StartDocument,
            OnEndDocument = counter.EndDocument,
            OnStartElement = counter.StartElement,
            OnEndElement = counter.EndElement,
            OnText = counter.Text,
            OnComment = counter.Comment,
            OnProcessingInstruction = counter.ProcessingInstruction,
            OnStartCData = counter.StartCData,
            OnEndCData = counter.EndCData,
            OnDeclaration = counter.Declaration,
            OnStartDtd = counter.StartDtd,
            OnDtdDeclaration = counter.DtdDeclaration,
            OnEndDtd = counter.EndDtd,
            OnSkippedEntity = counter.SkippedEntity,
            OnIncompleteAttributeDefault = counter.IncompleteAttributeDefault,
            OnDiagnostic = counter.Diagnostic,
        }, new XmlSaxCallbacks()];
        foreach (var handler in handlers)
        {
            for (var i = 0; i < 4096; i++)
            {
                CallbackTests.Dispatch(handler);
            }

            var initial = counter.Total;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 8192; i++)
            {
                CallbackTests.Dispatch(handler);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
            Assert.Equal(handler is XmlSaxCallbacks { OnText: null } ? 0 : 8192 * 16, counter.Total - initial);
            output.WriteLine($"{handler.GetType().Name}: 8192 x 16 notifications, 4096 warmups, {allocated} B total; setup/owned retention excluded");
        }
        var start = GC.GetAllocatedBytesForCurrentThread();
        var positive = new byte[64];
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(positive);
        Assert.True(bytes >= 64);
        output.WriteLine($"Positive control: {bytes} B");
    }
    private sealed class Counter : XmlSaxHandler
    {
        internal int Total;
        public override void StartDocument()
        {
            Total++;
        }
        public override void EndDocument()
        {
            Total++;
        }
        public override void StartElement(XmlStartElement element)
        {
            foreach (var a in element.Attributes)
            {
                if (a.LocalName.IsEmpty)
                {
                    throw new InvalidOperationException();
                }
            }
            foreach (var n in element.NamespaceDeclarations)
            {
                if (n.NamespaceUri.IsEmpty)
                {
                    throw new InvalidOperationException();
                }
            }
            Total++;
        }
        public override void EndElement(XmlEndElement element)
        {
            Total++;
        }
        public override void Text(ReadOnlySpan<char> text)
        {
            Total++;
        }
        public override void Comment(ReadOnlySpan<char> text)
        {
            Total++;
        }
        public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data)
        {
            Total++;
        }
        public override void StartCData()
        {
            Total++;
        }
        public override void EndCData()
        {
            Total++;
        }
        public override void Declaration(XmlDeclaration declaration)
        {
            Total++;
        }
        public override void StartDtd(XmlDocumentTypeView documentType)
        {
            Total++;
        }
        public override void DtdDeclaration(XmlDtdDeclaration declaration)
        {
            Total++;
        }
        public override void EndDtd()
        {
            Total++;
        }
        public override void SkippedEntity(XmlSkippedEntity entity)
        {
            Total++;
        }
        public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration)
        {
            Total++;
        }
        public override void Diagnostic(XmlDiagnostic diagnostic)
        {
            Total++;
        }
    }
}
