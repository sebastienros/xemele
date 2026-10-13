using System.Runtime.CompilerServices;

Console.WriteLine(Control.PositiveOrNegative(1));
internal static class Control
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int PositiveOrNegative(int value)
    {
        if (value > 0) return 1;
        return -1;
    }
}
