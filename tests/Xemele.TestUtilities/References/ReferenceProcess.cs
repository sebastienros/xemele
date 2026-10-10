using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Xemele.TestUtilities.References;

public sealed class ReferenceFailureException(string message) : Exception(message);

public static class ReferenceProcess
{
    public const int MaximumCharacters = 1_048_576;
    public static string BuildConfiguration(string assemblyDirectory) =>
        assemblyDirectory.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Contains("Release", StringComparer.Ordinal) ? "Release" : "Debug";

    // A fresh owned child per request isolates state and makes EOF/exit part of validation.
    public static async Task<string> ExecuteAsync(string executable, IEnumerable<string> arguments,
        string request, TimeSpan timeout, CancellationToken cancellationToken = default,
        Func<JsonElement, bool>? validateSuccessPayload = null)
    {
        if (request.Length > MaximumCharacters || request.Contains('\n') || request.Contains('\r'))
            throw new ReferenceFailureException("Invalid request framing or length.");
        using var input = JsonDocument.Parse(request);
        var expectedCase = input.RootElement.GetProperty("caseId").GetString();
        var expectedOperation = input.RootElement.GetProperty("operation").GetString();
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false, true),
            StandardOutputEncoding = new UTF8Encoding(false, true), StandardErrorEncoding = new UTF8Encoding(false, true) };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        try { if (!process.Start()) throw new ReferenceFailureException("Child failed to start."); }
        catch (System.ComponentModel.Win32Exception ex) { throw new ReferenceFailureException("Child failed to start: " + ex.Message); }
        Task<string>? outputTask = null, errorTask = null;
        Task? exchangeTask = null;
        try
        {
            Task<string> output = outputTask = ReadBounded(process.StandardOutput, linked.Token);
            Task<string> errors = errorTask = ReadBounded(process.StandardError, linked.Token);
            // Observe pipe faults immediately, even while the child is stalled writing another pipe.
            Task exchange = exchangeTask = Exchange();
            await Task.WhenAny(exchange, output, errors).WaitAsync(linked.Token);
            if (output.IsFaulted) await output;
            if (errors.IsFaulted) await errors;
            await exchange.WaitAsync(linked.Token);
            string stdout = await output;
            string stderr = await errors;
            if (process.ExitCode != 0) throw new ReferenceFailureException($"Child exit {process.ExitCode}: {stderr}");
            if (!stdout.EndsWith('\n')) throw new ReferenceFailureException("Unterminated response.");
            string line = stdout[..^1];
            if (line.EndsWith('\r')) line = line[..^1];
            if (line.Contains('\n') || line.Contains('\r')) throw new ReferenceFailureException("Multiple responses.");
            using var response = JsonDocument.Parse(line);
            var root = response.RootElement;
            var version = root.GetProperty("protocolVersion");
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1
                || root.GetProperty("caseId").GetString() != expectedCase
                || root.GetProperty("operation").GetString() != expectedOperation)
                throw new ReferenceFailureException("Response identity mismatch.");
            if (root.GetProperty("outcome").GetString() is not ("success" or "parse-rejected" or "runner-failure"))
                throw new ReferenceFailureException("Unknown outcome.");
            foreach (var diagnostic in root.GetProperty("diagnostics").EnumerateArray())
            {
                RequireString(diagnostic, "category");
                RequireString(diagnostic, "message");
            }
            foreach (string field in new[] { "name", "version", "runtime", "build" })
                if (string.IsNullOrEmpty(root.GetProperty("engine").GetProperty(field).GetString()))
                    throw new ReferenceFailureException("Missing engine metadata.");
            if (root.GetProperty("outcome").GetString() == "success")
            {
                if (expectedOperation == "xml.events")
                    foreach (var item in root.GetProperty("events").EnumerateArray()) ValidateEvent(item);
                else if (validateSuccessPayload is null)
                    throw new ReferenceFailureException("Unsupported success payload operation.");
                if (validateSuccessPayload is not null && !validateSuccessPayload(root))
                    throw new ReferenceFailureException("Adapter rejected success payload.");
            }
            else if (root.TryGetProperty("events", out _))
                throw new ReferenceFailureException("Failure response must omit events.");
            return line;

            async Task Exchange()
            {
                await process.StandardInput.WriteLineAsync(request.AsMemory(), linked.Token);
                process.StandardInput.Close();
                await process.WaitForExitAsync(linked.Token);
                await Task.WhenAll(output, errors);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ReferenceFailureException("Reference process timed out."); }
        catch (JsonException ex) { throw new ReferenceFailureException("Malformed response: " + ex.Message); }
        catch (DecoderFallbackException ex) { throw new ReferenceFailureException("Malformed response encoding: " + ex.Message); }
        catch (EncoderFallbackException ex) { throw new ReferenceFailureException("Malformed request encoding: " + ex.Message); }
        catch (KeyNotFoundException ex) { throw new ReferenceFailureException("Incomplete response: " + ex.Message); }
        catch (InvalidOperationException ex) { throw new ReferenceFailureException("Invalid response or process state: " + ex.Message); }
        finally
        {
            linked.Cancel();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            // Observe every started async operation before disposing its pipes/token source.
            foreach (Task? task in new Task?[] { exchangeTask, outputTask, errorTask })
                if (task is not null)
                    try { await task; } catch (Exception) { /* Original operation carries the failure. */ }
        }
    }

    private static string RequireString(JsonElement value, string name, bool allowEmpty = true)
    {
        var field = value.GetProperty(name);
        if (field.ValueKind != JsonValueKind.String || (!allowEmpty && field.GetString()!.Length == 0))
            throw new ReferenceFailureException("Invalid string field: " + name);
        return field.GetString()!;
    }

    private static void ValidateEvent(JsonElement item)
    {
        string kind = RequireString(item, "kind", allowEmpty: false);
        switch (kind)
        {
            case "start":
            case "end":
                RequireString(item, "namespaceUri");
                RequireString(item, "localName", allowEmpty: false);
                RejectField(item, "value");
                if (kind == "end") RejectField(item, "attributes");
                else
                    foreach (var attribute in item.GetProperty("attributes").EnumerateArray())
                    {
                        RequireString(attribute, "namespaceUri");
                        RequireString(attribute, "localName", allowEmpty: false);
                        RequireString(attribute, "value");
                    }
                break;
            case "text":
                RequireString(item, "value");
                RejectField(item, "namespaceUri");
                RejectField(item, "localName");
                RejectField(item, "attributes");
                break;
            default: throw new ReferenceFailureException("Unknown xml.events kind.");
        }
    }

    private static void RejectField(JsonElement item, string field)
    {
        if (item.TryGetProperty(field, out _)) throw new ReferenceFailureException("Inappropriate event field: " + field);
    }

    private static async Task<string> ReadBounded(StreamReader reader, CancellationToken token)
    {
        var result = new StringBuilder();
        char[] buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (result.Length + count > MaximumCharacters)
                throw new ReferenceFailureException("Child output exceeds limit.");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
}
