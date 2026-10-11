using Xemele.Html;
var tokens = Html5Tokenizer.Tokenize("<!doctype html><p class='demo'>a&amp;b</p>");
if (tokens.Count != 5 || tokens[0].Kind != HtmlTokenKind.Doctype || tokens[2].Data != "a&b" || tokens[^1].Kind != HtmlTokenKind.EndOfFile) throw new Exception("Unexpected tokenizer output.");
foreach (var token in tokens) Console.WriteLine($"{token.Kind}: {token.Name}{token.Data} @ {token.Location.LineNumber}:{token.Location.LinePosition}");
