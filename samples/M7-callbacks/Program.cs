using Xemele.Html;

// Keep selected owned snapshots; processing does not require a document or a token list.
HtmlToken? instruction = null;
int starts = 0, eof = 0;
Html5Tokenizer.Tokenize("<?build v1?><table>outside<tr><td>inside</table>", token =>
{
    if (token.Kind == HtmlTokenKind.StartTag) starts++;
    if (token.Kind == HtmlTokenKind.ProcessingInstruction) instruction = token;
    if (token.Kind == HtmlTokenKind.EndOfFile) eof++;
});
if (starts != 3 || eof != 1 || instruction is not { Name: "build", Data: "v1" })
    throw new InvalidOperationException("Callback contract failed.");
Console.WriteLine($"Lexical starts={starts}; EOF={eof}; retained PI={instruction.Name}:{instruction.Data}");
