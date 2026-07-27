namespace DiskAnalyzer.App.Models;

public sealed record DriveItem(
    string Name,
    string VolumeLabel,
    long TotalBytes,
    long FreeBytes)
{
    public long UsedBytes => TotalBytes - FreeBytes;
}
