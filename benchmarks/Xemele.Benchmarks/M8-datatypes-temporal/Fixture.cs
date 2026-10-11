using System.Text.Json;
using Xemele.Validation;

namespace Xemele.Benchmarks.M8DatatypesTemporal;

internal sealed class Fixture
{
    internal const string InputPath = "tests/fixtures/M8-datatypes-temporal/measurement-inputs.json";
    internal static readonly string[] Names = ["warm calendar equality", "warm long time comparison", "warm duration partial order", "warm duration equality", "warm calendar hash", "warm duration hash", "owned zoned calendar", "owned absent-zone bounds", "owned arbitrary year/fraction", "owned arbitrary duration endpoints", "invalid long fractional time", "operation/provider/context setup"];
    internal readonly Dictionary<string, string> Inputs = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(InputPath))!;
    internal readonly TemporalDatatypeProvider Provider = new();
    internal readonly SchemaNamespaceContext Context = new(new Dictionary<string, string>(), "", null);
    internal readonly SchemaCompileOperation Operation = NewOperation();
    private readonly DatatypeAtomicValue calendar, timeLong, timeLongOther, duration, durationOther;
    internal static SchemaCompileOperation NewOperation() => new(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null } });
    internal Fixture()
    {
        calendar = Parse("dateTime", Inputs["calendarZ"]); timeLong = Parse("time", Inputs["timeLong"]); timeLongOther = Parse("time", Inputs["timeLongOther"]);
        duration = Parse("duration", Inputs["duration"]); durationOther = Parse("duration", Inputs["durationOther"]);
    }
    internal DatatypeAtomicValue Parse(string type, string input)
    {
        if (!Provider.TryParse(DatatypeTypes.All[type], input, Context, Operation, out var value, out var error) || value is null || error != null)
            throw new InvalidOperationException("Probe parse failed.");
        return value;
    }
    internal void Run(int route)
    {
        switch (route)
        {
            case 0: if (!DatatypeValues.ValueEquals(calendar, calendar, Operation)) throw new InvalidOperationException(); break;
            case 1: if (DatatypeValues.Compare(timeLong, timeLongOther, Operation) != XmlValueOrder.Less) throw new InvalidOperationException(); break;
            case 2: if (DatatypeValues.Compare(duration, durationOther, Operation) != XmlValueOrder.Indeterminate) throw new InvalidOperationException(); break;
            case 3: if (!DatatypeValues.ValueEquals(duration, duration, Operation)) throw new InvalidOperationException(); break;
            case 4: if (DatatypeValues.ValueHashCode(calendar, Operation) != DatatypeValues.ValueHashCode(calendar, Operation)) throw new InvalidOperationException(); break;
            case 5: if (DatatypeValues.ValueHashCode(duration, Operation) != DatatypeValues.ValueHashCode(duration, Operation)) throw new InvalidOperationException(); break;
            case 6: GC.KeepAlive(Parse("dateTime", Inputs["calendarZ"])); break;
            case 7: GC.KeepAlive(Parse("dateTime", Inputs["calendarAbsent"])); break;
            case 8: GC.KeepAlive(Parse("dateTime", Inputs["calendarLong"])); break;
            case 9: GC.KeepAlive(Parse("duration", Inputs["durationLong"])); break;
            case 10:
                if (Provider.TryParse(DatatypeTypes.All["time"], Inputs["invalid"], Context, Operation, out var value, out var error) || value != null || error != DatatypeErrors.InvalidLexical)
                    throw new InvalidOperationException(); break;
            case 11: GC.KeepAlive(NewOperation()); GC.KeepAlive(new TemporalDatatypeProvider()); GC.KeepAlive(new SchemaNamespaceContext(new Dictionary<string, string>(), "", null)); break;
            default: throw new ArgumentOutOfRangeException(nameof(route));
        }
    }
}
