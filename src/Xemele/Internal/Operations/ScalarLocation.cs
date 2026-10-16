using System.Text;
namespace Xemele.Internal.Operations;

// Grammar supplies validated Unicode scalars and their original first-byte
// offsets. CRLF is one normalized newline, including across fragments. The
// decoder owns UTF-16/byte consumption; this tracker counts neither of those.
internal sealed class ScalarLocation(Uri? sourceUri)
{
    private int line = 1;
    private int column = 1;
    private bool afterCr;
    internal int ColumnCapacity => int.MaxValue - column;
    internal int LineCapacity => int.MaxValue - line;
    internal void Seed(int line, int column)
    {
        this.line = line;
        this.column = column;
        afterCr = false;
    }
    internal void AdvanceLinesAndColumns(int lines, int columnsAfterLastNewline)
    {
        line += lines;
        column = 1 + columnsAfterLastNewline;
        afterCr = false;
    }
    internal void AdvanceColumns(int count)
    {
        if ((uint)count > (uint)ColumnCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        column += count;
        if (count != 0)
        {
            afterCr = false;
        }
    }
    internal XmlLocation Current(long? originalByteOffset = null)
    {
        if (originalByteOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originalByteOffset));
        }

        return new(sourceUri, line, column, originalByteOffset);
    }
    internal bool TryAdvance(Rune scalar)
    {
        var value = scalar.Value;
        if (value == '\n' && afterCr)
        {
            afterCr = false;
            return true;
        }
        if (value is '\r' or '\n')
        {
            if (line == int.MaxValue)
            {
                return false;
            }

            line++;
            column = 1;
            afterCr = value == '\r';
        }
        else
        {
            if (column == int.MaxValue)
            {
                return false;
            }

            column++;
            afterCr = false;
        }
        return true;
    }
}
