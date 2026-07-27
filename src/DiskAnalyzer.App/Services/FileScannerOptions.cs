namespace DiskAnalyzer.App.Services;

public sealed class FileScannerOptions
{
    public int DirectoryWorkerCount { get; init; } = 2;

    public int ResultBufferCapacity { get; init; } = 4096;

    public TimeSpan ProgressReportInterval { get; init; } =
        TimeSpan.FromMilliseconds(150);

    internal void Validate()
    {
        if (DirectoryWorkerCount is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DirectoryWorkerCount),
                "Directory worker count must be between 1 and 16.");
        }

        if (ResultBufferCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ResultBufferCapacity),
                "Result buffer capacity must be positive.");
        }

        if (ProgressReportInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ProgressReportInterval),
                "Progress report interval cannot be negative.");
        }
    }
}
