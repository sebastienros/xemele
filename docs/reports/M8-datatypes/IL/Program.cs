using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;
using Xemele.Validation;
var assembly = typeof(SchemaCompileOptions).Assembly;
var core = assembly.GetType("Xemele.Validation.DatatypeValues")!;
var hashCalls = Inspect(core.GetMethod("ValueHashCode", BindingFlags.NonPublic | BindingFlags.Static)!);
if (args.Contains("--assert-indexed") && hashCalls.Contains("GetEnumerator")) throw new InvalidOperationException("List hash still obtains an enumerator.");
Inspect(typeof(ReadOnlyCollection<int>).GetMethod("GetEnumerator")!);
List<string> Inspect(MethodInfo method)
{
    var calls = new List<string>();
    Console.WriteLine(method.DeclaringType + "." + method.Name);
    var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
    var instructions = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));
    for (int offset = 0; offset < bytes.Length;)
    {
        int position = offset;
        ushort code = bytes[offset++]; if (code == 0xfe) code = (ushort)(0xfe00 | bytes[offset++]);
        var opcode = instructions[code];
        int size = opcode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + BitConverter.ToInt32(bytes, offset) * 4,
            _ => 4
        };
        if (opcode.OperandType == OperandType.InlineMethod)
        {
            var member = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset), method.DeclaringType!.GetGenericArguments(), method.GetGenericArguments());
            Console.WriteLine($"IL_{position:x4} {opcode.Name} {member!.DeclaringType}.{member.Name}");
            calls.Add(member.Name);
        }
        offset += size;
    }
    return calls;
}
