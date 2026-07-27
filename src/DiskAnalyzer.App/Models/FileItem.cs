using System.IO;

namespace DiskAnalyzer.App.Models;

public sealed class FileItem
{
    private string? _name;
    private string? _extension;

    public FileItem(
        string fullPath,
        long sizeBytes,
        DateTime creationTime,
        DateTime lastWriteTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);

        FullPath = fullPath;
        SizeBytes = sizeBytes;
        CreationTime = creationTime;
        LastWriteTime = lastWriteTime;
    }

    public string Name => _name ??= Path.GetFileName(FullPath);

    public string FullPath { get; }

    public long SizeBytes { get; }

    public string Extension => _extension ??= Path.GetExtension(FullPath);

    public DateTime CreationTime { get; }

    public DateTime LastWriteTime { get; }
}
