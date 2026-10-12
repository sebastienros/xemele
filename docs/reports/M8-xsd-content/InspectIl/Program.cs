using System.Reflection;
using System.Reflection.Emit;

if (args is not [var path]) throw new ArgumentException("Supply the built runtime assembly.");
var assembly = Assembly.LoadFrom(Path.GetFullPath(path));
var method = assembly.GetType("Xemele.Validation.ContentNamespaces", throwOnError: true)!
    .GetMethod("Allows", BindingFlags.NonPublic | BindingFlags.Static)!;
var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(c => unchecked((ushort)c.Value));
byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
for (int offset = 0; offset < il.Length;)
{
    int start = offset; ushort key = il[offset++];
    if (key == 0xfe) key = (ushort)(0xfe00 | il[offset++]);
    var code = codes[key];
    if (code.OperandType == OperandType.InlineMethod)
        Console.WriteLine($"IL_{start:x4} {code.Name} {method.Module.ResolveMethod(BitConverter.ToInt32(il, offset))}");
    offset += code.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
        _ => 4
    };
}
