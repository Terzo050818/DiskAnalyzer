using System.Diagnostics;
using DiskAnalyzer.App.Services;

if (args.Length is < 1 or > 2 ||
    !Directory.Exists(args[0]) ||
    (args.Length == 2 && !int.TryParse(args[1], out _)))
{
    Console.Error.WriteLine(
        "Usage: DiskAnalyzer.Benchmarks <existing-directory> [worker-count]");
    return 2;
}

var rootPath = Path.GetFullPath(args[0]);
var workerCount = args.Length == 2 ? int.Parse(args[1]) : 2;
var scanner = new FileScannerService(
    new FileScannerOptions
    {
        DirectoryWorkerCount = workerCount
    });

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
var retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
var stopwatch = Stopwatch.StartNew();
long fileCount = 0;
long totalBytes = 0;
var retainedFiles = new List<DiskAnalyzer.App.Models.FileItem>();

await foreach (var file in scanner.ScanAsync(rootPath))
{
    retainedFiles.Add(file);
    fileCount++;
    totalBytes += file.SizeBytes;
}

stopwatch.Stop();
var allocatedBytes =
    GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
var retainedBytes =
    GC.GetTotalMemory(forceFullCollection: true) - retainedBefore;
var filesPerSecond = fileCount / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);

Console.WriteLine($"Root: {rootPath}");
Console.WriteLine($"Workers: {workerCount}");
Console.WriteLine($"Files: {fileCount:N0}");
Console.WriteLine($"Bytes: {totalBytes:N0}");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalMilliseconds:N0} ms");
Console.WriteLine($"Throughput: {filesPerSecond:N0} files/s");
Console.WriteLine($"Managed allocations: {allocatedBytes / 1024d / 1024d:N1} MB");
Console.WriteLine($"Retained managed memory: {retainedBytes / 1024d / 1024d:N1} MB");

GC.KeepAlive(retainedFiles);

return 0;
