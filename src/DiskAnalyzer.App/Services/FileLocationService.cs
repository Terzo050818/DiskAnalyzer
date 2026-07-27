using System.Diagnostics;
using System.IO;

namespace DiskAnalyzer.App.Services;

public sealed class FileLocationService : IFileLocationService
{
    public void OpenContainingFolder(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected file no longer exists.", fullPath);
        }

        var startInfo = CreateStartInfo(fullPath);

        Process.Start(startInfo);
    }

    internal static ProcessStartInfo CreateStartInfo(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };

        startInfo.ArgumentList.Add($"/select,{fullPath}");
        return startInfo;
    }
}
