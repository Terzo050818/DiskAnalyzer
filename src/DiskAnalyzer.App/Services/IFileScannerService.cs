using DiskAnalyzer.App.Models;

namespace DiskAnalyzer.App.Services;

public interface IFileScannerService
{
    IAsyncEnumerable<FileItem> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
