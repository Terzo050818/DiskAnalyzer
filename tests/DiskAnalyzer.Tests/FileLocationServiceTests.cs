using DiskAnalyzer.App.Services;

namespace DiskAnalyzer.Tests;

public sealed class FileLocationServiceTests
{
    [Fact]
    public void CreateStartInfo_PreservesSpacesAndUnicodeAsOneArgument()
    {
        const string fullPath = @"C:\包含 空格\测试文件.iso";

        var startInfo = FileLocationService.CreateStartInfo(fullPath);

        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Single(startInfo.ArgumentList);
        Assert.Equal($"/select,{fullPath}", startInfo.ArgumentList[0]);
    }
}
