# SAX, push, pull, and async contracts — M3

Decision owner: `M3-contracts`. Dependencies: completed M2 XML engine. SAX is a required product surface. The public data/handler lane finishes before independent adapters start.

## Callback shapes

```csharp
namespace Xemele.Streaming;
public delegate void XmlStartElementCallback(XmlStartElement element);
public delegate void XmlEndElementCallback(XmlEndElement element);
public delegate void XmlTextCallback(ReadOnlySpan<char> text);

public interface IXmlSaxHandler
{
    void StartDocument();
    void EndDocument();
    void StartElement(XmlStartElement element);
    void EndElement(XmlEndElement element);
    void Text(ReadOnlySpan<char> text);
    void Comment(ReadOnlySpan<char> text);
    void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data);
    void StartCData();
    void EndCData();
    void Diagnostic(XmlDiagnostic diagnostic);
}
public abstract class XmlSaxHandler : IXmlSaxHandler
{
    // One public virtual no-op implementation per interface method.
}
public sealed class XmlSaxCallbacks : XmlSaxHandler
{
    // Delegate properties cannot share the inherited method names.
    public XmlStartElementCallback? OnStartElement { get; init; }
    public XmlEndElementCallback? OnEndElement { get; init; }
    public XmlTextCallback? OnText { get; init; }
    // Matching optional On* delegates for all notifications.
}
public readonly ref struct XmlStartElement
{
    public ReadOnlySpan<char> LocalName { get; }
    public ReadOnlySpan<char> Prefix { get; }
    public ReadOnlySpan<char> NamespaceUri { get; }
    public XmlAttributeView Attributes { get; }
    public XmlNamespaceView NamespaceDeclarations { get; }
    public XmlLocation Location { get; }
}
```

`XmlEndElement` contains the corresponding name/location. Attribute/namespace collections are borrowed `readonly ref struct` views with concrete enumerators; indexers return borrowed item views. Do not use `ReadOnlySpan<T>` with a ref-struct item type. Attribute items include local name/prefix/URI/value and defaulted status. M3 contract fixtures enumerate every type/member, plus DTD/declaration callbacks and skipped entities, before the interface freezes. These event records can wrap the existing internal event data without exposing parser state.

This refines the earlier design: `On*` delegate properties allow a single handler inheritance model without property/method name collisions. Example:

```csharp
XmlSaxParser.Parse(xml, new XmlSaxCallbacks
{
    OnStartElement = element => Console.WriteLine(element.LocalName.ToString()),
    OnText = text => Console.WriteLine(text.ToString())
});
```

## Entry points and state machines

```csharp
public static partial class XmlSaxParser
{
    public static void Parse(string xml, IXmlSaxHandler handler,
        XmlParseOptions? options = null, CancellationToken cancellationToken = default);
    public static void Parse(ReadOnlySpan<byte> xml, IXmlSaxHandler handler,
        XmlParseOptions? options = null, CancellationToken cancellationToken = default);
    public static void Parse(Stream input, IXmlSaxHandler handler,
        XmlParseOptions? options = null, CancellationToken cancellationToken = default);
    public static Task ParseAsync(Stream input, IXmlSaxHandler handler,
        XmlParseOptions? options = null, CancellationToken cancellationToken = default);
}
public sealed class XmlPushParser : IDisposable
{
    public XmlPushParser(IXmlSaxHandler handler, XmlParseOptions? options = null);
    public void Append(ReadOnlySpan<byte> input);
    public void Complete();
}
public sealed partial class XmlReader : IDisposable
{
    public static XmlReader Create(Stream input, XmlParseOptions? options = null);
    public bool Read();
    public ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default);
    public XmlTokenKind TokenKind { get; }
    public ReadOnlySpan<char> ValueSpan { get; }
    public XmlLocation Location { get; }
}
```

Reader name/attribute/namespace access and complete token kinds are fixed in the M3 contract fixture. Its current token is valid until the next read/disposal. Async reader/DOM methods are added by the transport task in separate partial files, using an internal pump contract approved before the reader implementation.

The shape shows the completed M3 surface. The synchronous reader task ships `Read` and `IDisposable`; the transport task adds `ReadAsync` and `IAsyncDisposable`. Earlier task branches must compile without their later partials.

Push states: created → accepting → completed, or faulted/disposed. Empty chunks are legal. Append parses immediately; Complete signals EOF once. Append after completion, duplicate Complete, and concurrent/reentrant operations throw `InvalidOperationException`. Callback exceptions fault the parser and propagate unchanged; no further content callbacks occur. A faulted push context cannot resume.

Callbacks are sequential, namespace-aware, and invoked before EOF as input permits. Empty elements produce both start/end. CDATA content goes through Text with lexical boundaries; text can split arbitrarily. EndDocument means successful completion only. Strict failure/cancellation cannot emit it. Recovery results must clearly communicate repaired or incomplete input; contract review specifies that result path without changing strict void entry points.

Async changes I/O, not callback signatures: callbacks remain synchronous with borrowed lifetimes; no `async void`. Cancellation uses `OperationCanceledException`; independent contexts can run concurrently. No fixed thread is promised. Reader callbacks/pull adapters must not accumulate a chunk's unbounded events. A sink's pause signal provides backpressure.

## Task boundaries and acceptance

`M3-callbacks` owns borrowed view adapters, delegates, and handler classes. `M3-sax`, `M3-push`, `M3-reader`, and `M3-writer` run independently after their declared dependencies. `M3-async` follows SAX/reader and owns only async partials/transport. No worker modifies a peer's core file.

Required checks: delegate/subclass equivalence, minimal handler usability, lifetime/ordering, namespaces/defaults, exception propagation, cancellation, reentrancy, all chunk splits, early callbacks, sync/async equality, and the 1 GiB bounded-memory case from the performance plan. No M4 query or later validator is needed.
