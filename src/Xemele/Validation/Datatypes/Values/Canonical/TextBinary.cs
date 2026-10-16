namespace Xemele.Validation;

internal sealed partial class TextBinaryDatatypeProvider
{
    private static ReadOnlySpan<char> HexAlphabet => "0123456789ABCDEF";
    private static ReadOnlySpan<char> Base64Alphabet => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    internal static void WriteConstraint(DatatypeAtomicValue value, ReadOnlySpan<char> authored, ref ConstraintWriter writer)
    {
        writer.Operation.Charge(1, default);
        if (value is TextValue text)
        {
            writer.Append(text.Text);
            return;
        }
        if (value is BooleanValue boolean)
        {
            writer.Append(boolean.Value ? "true" : "false");
            return;
        }
        if (value is QNameValue)
        {
            writer.Collapsed(authored);
            return;
        }
        var bytes = ((BinaryValue)value).Bytes;
        _ = ConstraintWriter.CheckedLength(value.Primitive == DatatypePrimitive.HexBinary ? (long)bytes.Length * 2 : ((long)bytes.Length + 2) / 3 * 4, writer.Operation);
        if (value.Primitive == DatatypePrimitive.HexBinary)
        {
            foreach (var b in bytes)
            {
                writer.Operation.Charge(1, default);
                writer.Append(HexAlphabet[b >> 4]);
                writer.Append(HexAlphabet[b & 15]);
            }
        }
        else
        {
            for (var i = 0; i < bytes.Length;)
            {
                writer.Operation.Charge(1, default);
                int a = bytes[i++], b = 0, c = 0;
                var second = i < bytes.Length;
                if (second)
                {
                    writer.Operation.Charge(1, default);
                    b = bytes[i++];
                }
                var third = i < bytes.Length;
                if (third)
                {
                    writer.Operation.Charge(1, default);
                    c = bytes[i++];
                }
                writer.Append(Base64Alphabet[a >> 2]);
                writer.Append(Base64Alphabet[((a & 3) << 4) | (b >> 4)]);
                writer.Append(second ? Base64Alphabet[((b & 15) << 2) | (c >> 6)] : '=');
                writer.Append(third ? Base64Alphabet[c & 63] : '=');
            }
        }
    }
}
