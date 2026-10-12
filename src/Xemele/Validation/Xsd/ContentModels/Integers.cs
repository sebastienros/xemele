using System.Numerics;
namespace Xemele.Validation;

// Compilation algebra uses bounded limb steps rather than a single potentially
// enormous BigInteger multiplication. Byte snapshots and the final result are
// explicit owned scratch/result construction, charged before persistence.
internal static class ContentIntegers
{
    private static byte[] Bytes(BigInteger value, ISchemaOperation op, XmlLocation at)
    {
        int length = value.GetByteCount(isUnsigned: true);
        ContentWork.Retain(op, at, length);
        var bytes = new byte[length];
        if (!value.TryWriteBytes(bytes, out int written, isUnsigned: true) || written != length) throw new InvalidOperationException("Integer snapshot failed.");
        return bytes;
    }
    internal static int Compare(BigInteger a, BigInteger b, ISchemaOperation op, XmlLocation at)
    {
        ContentWork.Integer(op, at, a); ContentWork.Integer(op, at, b);
        if (a.Sign != b.Sign) return a.Sign.CompareTo(b.Sign);
        long abits = a.GetBitLength(), bbits = b.GetBitLength(); if (abits != bbits) return abits.CompareTo(bbits);
        if (abits <= 64) return ((ulong)a).CompareTo((ulong)b);
        var aa = Bytes(a, op, at); var bb = Bytes(b, op, at);
        for (int i = aa.Length - 1; i >= 0; i--)
        { ContentWork.Tick(op, at); if (aa[i] != bb[i]) return aa[i].CompareTo(bb[i]); }
        return 0;
    }
    internal static BigInteger Multiply(BigInteger a, BigInteger b, ISchemaOperation op, XmlLocation at)
    {
        ContentWork.Tick(op, at);
        if (a.IsZero || b.IsZero) return BigInteger.Zero;
        if (a.IsOne) return b; if (b.IsOne) return a;
        if (a.GetBitLength() <= 32 && b.GetBitLength() <= 32)
        { ContentWork.Retain(op, at); return new BigInteger((ulong)a * (ulong)b); }
        var aa = Bytes(a, op, at); var bb = Bytes(b, op, at);
        ContentWork.Retain(op, at, checked(aa.Length + bb.Length)); var result = new byte[checked(aa.Length + bb.Length)];
        for (int i = 0; i < aa.Length; i++)
        {
            int carry = 0;
            for (int j = 0; j < bb.Length; j++)
            { ContentWork.Tick(op, at); int cell = aa[i] * bb[j] + result[i + j] + carry; result[i + j] = (byte)cell; carry = cell >> 8; }
            result[i + bb.Length] = (byte)carry;
        }
        ContentWork.Retain(op, at, result.Length); return new BigInteger(result, isUnsigned: true);
    }
    internal static BigInteger Add(BigInteger a, BigInteger b, ISchemaOperation op, XmlLocation at)
    {
        ContentWork.Tick(op, at);
        if (a.IsZero) return b; if (b.IsZero) return a;
        if (a.GetBitLength() < 63 && b.GetBitLength() < 63)
        { ContentWork.Retain(op, at); return new BigInteger((ulong)a + (ulong)b); }
        var aa = Bytes(a, op, at); var bb = Bytes(b, op, at); int size = checked(Math.Max(aa.Length, bb.Length) + 1);
        ContentWork.Retain(op, at, size); var result = new byte[size]; int carry = 0;
        for (int i = 0; i < size - 1; i++)
        { ContentWork.Tick(op, at); int value = (i < aa.Length ? aa[i] : 0) + (i < bb.Length ? bb[i] : 0) + carry; result[i] = (byte)value; carry = value >> 8; }
        result[^1] = (byte)carry; ContentWork.Retain(op, at, result.Length); return new BigInteger(result, isUnsigned: true);
    }
}
