using DiskAnalyzer.App.Services;

namespace DiskAnalyzer.Tests;

public sealed class FileScannerOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Constructor_WithInvalidWorkerCount_Throws(int workerCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FileScannerService(
                new FileScannerOptions
                {
                    DirectoryWorkerCount = workerCount
                }));
    }

    [Fact]
    public void Constructor_WithEmptyResultBuffer_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FileScannerService(
                new FileScannerOptions
                {
                    ResultBufferCapacity = 0
                }));
    }

    [Fact]
    public void Constructor_WithNegativeProgressInterval_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FileScannerService(
                new FileScannerOptions
                {
                    ProgressReportInterval = TimeSpan.FromMilliseconds(-1)
                }));
    }
}
