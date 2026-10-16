using System.Reflection;

internal static class HookInventory
{
    internal static void Validate(MethodInfo method)
    {
        if (!method.IsAssembly || method.IsStatic)
            throw new FormatException("access");
        if (method.Name != "BindCancellationToken" || method.ReturnType != typeof(void))
            throw new FormatException("method");
        var parameters = method.GetParameters();
        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(CancellationToken))
            throw new FormatException("type");
        if (parameters[0].Name != "token")
            throw new FormatException("name");
        if (parameters[0].IsOptional || parameters[0].HasDefaultValue)
            throw new FormatException("default");
        if (new NullabilityInfoContext().Create(parameters[0]).ReadState != NullabilityState.NotNull)
            throw new FormatException("nullability");
    }
    internal static void VerifyCompiledMutations()
    {
        Validate(typeof(Exact).GetMethod("BindCancellationToken", BindingFlags.Instance | BindingFlags.NonPublic)!);
        Reject(typeof(PublicAccess), "access");
        Reject(typeof(PrivateAccess), "access");
        Reject(typeof(OptionalDefault), "default");
        Reject(typeof(RenamedParameter), "name");
        Console.WriteLine("Compiled inventory control and four access/default/name mutations passed.");
    }
    private static void Reject(Type type, string expected)
    {
        var method = type.GetMethod("BindCancellationToken", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        try
        {
            Validate(method);
        }
        catch (FormatException error) when (error.Message == expected) { return; }
        throw new InvalidOperationException("Inventory failed to reject " + type.Name);
    }
    private sealed class Exact
    {
        internal void BindCancellationToken(CancellationToken token)
        {
        }
    }
    private sealed class PublicAccess
    {
        public void BindCancellationToken(CancellationToken token)
        {
        }
    }
    private sealed class PrivateAccess
    {
        private void BindCancellationToken(CancellationToken token)
        {
        }
    }
    private sealed class OptionalDefault
    {
        internal void BindCancellationToken(CancellationToken token = default)
        {
        }
    }
    private sealed class RenamedParameter
    {
        internal void BindCancellationToken(CancellationToken other)
        {
        }
    }
}
