namespace DiskAnalyzer.App.Models;

public sealed record ScanProgress(
    string CurrentPath,
    long FilesDiscovered,
    long BytesDiscovered,
    long SkippedItems,
    TimeSpan Elapsed);
