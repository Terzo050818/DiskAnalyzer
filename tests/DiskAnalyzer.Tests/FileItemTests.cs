using DiskAnalyzer.App.Models;

namespace DiskAnalyzer.Tests;

public sealed class FileItemTests
{
    [Fact]
    public void NameAndExtension_AreDerivedFromFullPath()
    {
        var file = new FileItem(
            @"C:\data\archive.tar.gz",
            42,
            DateTime.UnixEpoch,
            DateTime.UnixEpoch);

        Assert.Equal("archive.tar.gz", file.Name);
        Assert.Equal(".gz", file.Extension);
    }
}
