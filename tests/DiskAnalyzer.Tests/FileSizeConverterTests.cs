using System.Globalization;
using DiskAnalyzer.App.Converters;

namespace DiskAnalyzer.Tests;

public sealed class FileSizeConverterTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0, "0 Bytes")]
    [InlineData(1023, "1,023 Bytes")]
    [InlineData(1024, "1 KB")]
    [InlineData(10485760, "10 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1099511627776, "1 TB")]
    public void Format_UsesReadableBinaryUnit(long bytes, string expected)
    {
        Assert.Equal(expected, FileSizeConverter.Format(bytes, Invariant));
    }

    [Fact]
    public void Format_WithNegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FileSizeConverter.Format(-1, Invariant));
    }
}
