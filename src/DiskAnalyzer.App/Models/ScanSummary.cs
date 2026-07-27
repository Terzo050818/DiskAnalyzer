namespace DiskAnalyzer.App.Models;

public sealed record ScanSummary(
    long FilesDiscovered,
    long BytesDiscovered,
    long SkippedItems,
    TimeSpan Elapsed,
    bool WasCancelled);
