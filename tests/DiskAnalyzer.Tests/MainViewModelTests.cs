using DiskAnalyzer.App.ViewModels;

namespace DiskAnalyzer.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void NewViewModel_IsReady()
    {
        var viewModel = new MainViewModel();

        Assert.Equal("DiskAnalyzer", viewModel.AppTitle);
        Assert.NotNull(viewModel.FilesView);
        Assert.False(viewModel.IsScanning);
    }
}
