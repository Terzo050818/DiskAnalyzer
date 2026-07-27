using DiskAnalyzer.App.Models;
using DiskAnalyzer.App.Services;

namespace DiskAnalyzer.Tests;

public sealed class FileScannerServiceTests
{
    [Fact]
    public async Task ScanAsync_ReturnsFilesFromNestedDirectories()
    {
        using var testDirectory = new TestDirectory();
        var nestedDirectory = Directory.CreateDirectory(
            Path.Combine(testDirectory.Path, "nested"));

        await File.WriteAllBytesAsync(
            Path.Combine(testDirectory.Path, "small.txt"),
            new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(
            Path.Combine(nestedDirectory.FullName, "large.bin"),
            new byte[] { 1, 2, 3, 4, 5 });

        var progress = new RecordingProgress<ScanProgress>();
        var scanner = new FileScannerService();

        var files = await ReadAllAsync(
            scanner.ScanAsync(testDirectory.Path, progress));

        Assert.Equal(2, files.Count);
        Assert.Equal(3, files.Single(file => file.Name == "small.txt").SizeBytes);
        Assert.Equal(5, files.Single(file => file.Name == "large.bin").SizeBytes);

        var finalProgress = Assert.Single(progress.Values.TakeLast(1));
        Assert.Equal(2, finalProgress.FilesDiscovered);
        Assert.Equal(8, finalProgress.BytesDiscovered);
        Assert.Equal(0, finalProgress.SkippedItems);
    }

    [Fact]
    public async Task ScanAsync_WithCancelledToken_ThrowsOperationCancelled()
    {
        using var testDirectory = new TestDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(testDirectory.Path, "file.txt"),
            "content");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var scanner = new FileScannerService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await ReadAllAsync(
                scanner.ScanAsync(
                    testDirectory.Path,
                    cancellationToken: cancellation.Token)));
    }

    [Fact]
    public async Task ScanAsync_WhenCancelledDuringEnumeration_StopsPromptly()
    {
        using var testDirectory = new TestDirectory();

        for (var index = 0; index < 100; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory.Path, $"file-{index:D3}.txt"),
                "content");
        }

        using var cancellation = new CancellationTokenSource();
        var scanner = new FileScannerService();
        var receivedFiles = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                await foreach (var _ in scanner.ScanAsync(
                                   testDirectory.Path,
                                   cancellationToken: cancellation.Token))
                {
                    receivedFiles++;
                    cancellation.Cancel();
                }
            });

        Assert.Equal(1, receivedFiles);
    }

    [Fact]
    public async Task ScanAsync_DoesNotTraverseDirectoryReparsePoint()
    {
        using var testDirectory = new TestDirectory();
        var targetDirectory = Directory.CreateDirectory(
            Path.Combine(testDirectory.Path, "target"));
        await File.WriteAllTextAsync(
            Path.Combine(targetDirectory.FullName, "target-file.txt"),
            "content");

        var linkPath = Path.Combine(testDirectory.Path, "target-link");
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetDirectory.FullName);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var scanner = new FileScannerService();

        var files = await ReadAllAsync(scanner.ScanAsync(testDirectory.Path));

        Assert.Single(files);
        Assert.Equal(
            Path.Combine(targetDirectory.FullName, "target-file.txt"),
            files[0].FullPath);
    }

    [Fact]
    public async Task ScanAsync_WithMissingRoot_ThrowsDirectoryNotFound()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"DiskAnalyzer-missing-{Guid.NewGuid():N}");

        var scanner = new FileScannerService();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            async () => await ReadAllAsync(scanner.ScanAsync(missingPath)));
    }

    [Fact]
    public async Task ScanAsync_WithMultipleWorkers_ReturnsEveryFileOnce()
    {
        using var testDirectory = new TestDirectory();
        const int directoryCount = 20;
        const int filesPerDirectory = 15;

        for (var directoryIndex = 0;
             directoryIndex < directoryCount;
             directoryIndex++)
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(testDirectory.Path, $"dir-{directoryIndex:D2}"));

            for (var fileIndex = 0;
                 fileIndex < filesPerDirectory;
                 fileIndex++)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(directory.FullName, $"file-{fileIndex:D2}.dat"),
                    "x");
            }
        }

        var scanner = new FileScannerService(
            new FileScannerOptions
            {
                DirectoryWorkerCount = 4
            });

        var files = await ReadAllAsync(scanner.ScanAsync(testDirectory.Path));

        Assert.Equal(directoryCount * filesPerDirectory, files.Count);
        Assert.Equal(files.Count, files.Select(file => file.FullPath).Distinct().Count());
    }

    [Fact]
    public async Task ScanAsync_PreservesUnicodeAndSpacesInPaths()
    {
        using var testDirectory = new TestDirectory();
        var directory = Directory.CreateDirectory(
            Path.Combine(testDirectory.Path, "包含 空格的目录"));
        var expectedPath = Path.Combine(directory.FullName, "大型 文件.iso");
        await File.WriteAllTextAsync(expectedPath, "content");

        var scanner = new FileScannerService();

        var files = await ReadAllAsync(scanner.ScanAsync(testDirectory.Path));

        var file = Assert.Single(files);
        Assert.Equal(expectedPath, file.FullPath);
        Assert.Equal("大型 文件.iso", file.Name);
        Assert.Equal(".iso", file.Extension);
    }

    private static async Task<List<FileItem>> ReadAllAsync(
        IAsyncEnumerable<FileItem> source)
    {
        var files = new List<FileItem>();

        await foreach (var file in source)
        {
            files.Add(file);
        }

        return files;
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = new();

        public void Report(T value)
        {
            Values.Add(value);
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"DiskAnalyzer-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
