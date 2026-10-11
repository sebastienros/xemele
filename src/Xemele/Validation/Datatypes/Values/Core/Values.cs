using System.Collections.ObjectModel;
namespace Xemele.Validation;

internal static class DatatypeValues
{
    internal static DatatypeFamily Family(DatatypePrimitive primitive) => primitive switch
    {
        DatatypePrimitive.Decimal or DatatypePrimitive.Float or DatatypePrimitive.Double => DatatypeFamily.Numeric,
        >= DatatypePrimitive.Duration and <= DatatypePrimitive.GMonth => DatatypeFamily.Temporal,
        _ => DatatypeFamily.TextBinary
    };
    internal static void RequireName(SchemaName name)
    {
        ArgumentNullException.ThrowIfNull(name.NamespaceUri);
        ArgumentException.ThrowIfNullOrEmpty(name.LocalName);
    }
    internal static void Charge(ISchemaOperation operation, long work = 1)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!operation.Charge(work, default)) throw SchemaSnapshots.Failure(operation);
    }
    private static void Component(ISchemaOperation operation)
    { if (!operation.ChargeComponents(1, default)) throw SchemaSnapshots.Failure(operation); }

    internal static XmlDatatypeValue CreateList(SchemaName type, IReadOnlyList<XmlDatatypeValue> items, ISchemaOperation operation)
    {
        RequireName(type); ArgumentNullException.ThrowIfNull(items); Charge(operation, 0);
        // One dispatch/node component, one retained reference per item. Reserve
        // before allocating the snapshot; each item visit is separately charged.
        int count = items.Count;
        Charge(operation);
        if (count == int.MaxValue || !operation.ChargeComponents(count + 1, default))
        {
            if (count == int.MaxValue) operation.Refuse("SCHEMA.Limit", "Datatype list representation exceeded.", default);
            throw SchemaSnapshots.Failure(operation);
        }
        var snapshot = new XmlDatatypeValue[count];
        for (int i = 0; i < count; i++)
        {
            Charge(operation);
            var item = items[i]; ArgumentNullException.ThrowIfNull(item);
            if (Unwrap(item, operation).Variety != XmlDatatypeVariety.Atomic)
                throw new ArgumentException("List members must be atomic or unions selecting atomic values.", nameof(items));
            snapshot[i] = item;
        }
        if (items.Count != count) throw new InvalidOperationException("Datatype items changed during snapshot.");
        Charge(operation, 0);
        return new ListValue(type, new ReadOnlyCollection<XmlDatatypeValue>(snapshot));
    }
    internal static XmlDatatypeValue CreateUnion(SchemaName type, XmlDatatypeValue member, ISchemaOperation operation)
    {
        RequireName(type); ArgumentNullException.ThrowIfNull(member); Charge(operation);
        _ = RequireValue(Unwrap(member, operation)); Component(operation); Charge(operation, 0);
        return new UnionValue(type, member);
    }
    internal static XmlDatatypeValue Retype(SchemaName type, XmlDatatypeValue value, ISchemaOperation operation)
    {
        RequireName(type); ArgumentNullException.ThrowIfNull(value); Charge(operation);
        // Eliminate successive atomic retype wrappers. Union selected identity
        // and list snapshot identity are preserved, including declared member names.
        if (value is RetypedValue retyped) value = retyped.Source;
        _ = RequireValue(value); Component(operation); Charge(operation, 0);
        return value switch
        {
            ListValue list => new ListValue(type, list.Items),
            UnionValue union => new UnionValue(type, union.SelectedMember),
            DatatypeAtomicValue atomic => new RetypedValue(type, atomic),
            _ => throw new ArgumentException("Value was not produced by the datatype core.", nameof(value))
        };
    }
    internal static XmlDatatypeValue Unwrap(XmlDatatypeValue value, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(value); Charge(operation, 0);
        while (true)
        {
            if (value is UnionValue union) { Charge(operation); value = union.SelectedMember; }
            else if (value is RetypedValue retyped) { Charge(operation); value = retyped.Source; }
            else { Charge(operation, 0); return RequireValue(value); }
        }
    }
    private static XmlDatatypeValue RequireValue(XmlDatatypeValue value) => value is DatatypeAtomicValue or ListValue or UnionValue or RetypedValue
        ? value : throw new ArgumentException("Value was not produced by the datatype core.", nameof(value));

    internal static bool ValueEquals(XmlDatatypeValue left, XmlDatatypeValue right, ISchemaOperation operation)
    {
        Charge(operation); left = Unwrap(left, operation); right = Unwrap(right, operation);
        bool equal;
        if (left is ListValue a && right is ListValue b)
        {
            if (a.Items.Count != b.Items.Count) equal = false;
            else
            {
                equal = true;
                for (int i = 0; i < a.Items.Count; i++)
                    if (!ValueEquals(a.Items[i], b.Items[i], operation)) { equal = false; break; }
            }
        }
        else if (left is DatatypeAtomicValue x && right is DatatypeAtomicValue y && x.Primitive == y.Primitive)
        { RequireSameProvider(x, y); equal = x.Provider.ValueEquals(x, y, operation); }
        else equal = false;
        Charge(operation, 0); return equal;
    }
    internal static XmlValueOrder Compare(XmlDatatypeValue left, XmlDatatypeValue right, ISchemaOperation operation)
    {
        Charge(operation); left = Unwrap(left, operation); right = Unwrap(right, operation);
        XmlValueOrder order;
        if (left is DatatypeAtomicValue x && right is DatatypeAtomicValue y && x.Primitive == y.Primitive)
        { RequireSameProvider(x, y); order = x.Provider.Compare(x, y, operation); }
        else if (left is ListValue && right is ListValue) order = ValueEquals(left, right, operation) ? XmlValueOrder.Equal : XmlValueOrder.Indeterminate;
        else order = XmlValueOrder.Indeterminate;
        if (!Enum.IsDefined(order)) throw new InvalidOperationException("Family returned an invalid order.");
        Charge(operation, 0); return order;
    }
    private static void RequireSameProvider(DatatypeAtomicValue left, DatatypeAtomicValue right)
    {
        if (!ReferenceEquals(left.Provider, right.Provider)) throw new ArgumentException("Values belong to different provider sets.");
    }
    internal static int ValueHashCode(XmlDatatypeValue value, ISchemaOperation operation)
    {
        Charge(operation); value = Unwrap(value, operation);
        int hash;
        if (value is ListValue list)
        {
            hash = 17;
            foreach (var item in list.Items) hash = unchecked(hash * 31 + ValueHashCode(item, operation));
        }
        else
        {
            var atomic = (DatatypeAtomicValue)value;
            hash = unchecked(((int)atomic.Primitive + 1) * 397 ^ atomic.Provider.ValueHashCode(atomic, operation));
        }
        Charge(operation, 0); return hash;
    }
    internal static long? ValueLength(XmlDatatypeValue value, ISchemaOperation operation)
    {
        Charge(operation); value = Unwrap(value, operation);
        long? length = value is ListValue list ? list.Items.Count : ((DatatypeAtomicValue)value).Provider.ValueLength((DatatypeAtomicValue)value, operation);
        if (length < 0) throw new InvalidOperationException("Negative family length.");
        Charge(operation, 0); return length;
    }
    internal static int? TotalDigits(XmlDatatypeValue value, ISchemaOperation operation) => Digits(value, operation, false);
    internal static int? FractionDigits(XmlDatatypeValue value, ISchemaOperation operation) => Digits(value, operation, true);
    private static int? Digits(XmlDatatypeValue value, ISchemaOperation operation, bool fraction)
    {
        Charge(operation); value = Unwrap(value, operation);
        int? result = value is DatatypeAtomicValue atomic ? fraction ? atomic.Provider.FractionDigits(atomic, operation) : atomic.Provider.TotalDigits(atomic, operation) : null;
        if (result < 0) throw new InvalidOperationException("Negative family digit count.");
        Charge(operation, 0); return result;
    }
    private sealed class ListValue(SchemaName type, IReadOnlyList<XmlDatatypeValue> items) : XmlDatatypeValue
    {
        internal override SchemaName TypeName => type;
        internal override XmlDatatypeVariety Variety => XmlDatatypeVariety.List;
        internal override IReadOnlyList<XmlDatatypeValue> Items => items;
        internal override XmlDatatypeValue? SelectedMember => null;
    }
    private sealed class UnionValue(SchemaName type, XmlDatatypeValue member) : XmlDatatypeValue
    {
        internal override SchemaName TypeName => type;
        internal override XmlDatatypeVariety Variety => XmlDatatypeVariety.Union;
        internal override IReadOnlyList<XmlDatatypeValue>? Items => null;
        internal override XmlDatatypeValue SelectedMember => member;
    }
    private sealed class RetypedValue(SchemaName type, DatatypeAtomicValue source) : XmlDatatypeValue
    {
        internal DatatypeAtomicValue Source => source;
        internal override SchemaName TypeName => type;
        internal override XmlDatatypeVariety Variety => XmlDatatypeVariety.Atomic;
        internal override IReadOnlyList<XmlDatatypeValue>? Items => null;
        internal override XmlDatatypeValue? SelectedMember => null;
    }
}
