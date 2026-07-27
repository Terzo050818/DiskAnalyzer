using DiskAnalyzer.App.Services;

namespace DiskAnalyzer.Tests;

public sealed class DriveServiceTests
{
    [Fact]
    public void GetFixedDrives_ReturnsConsistentCapacityValues()
    {
        var service = new DriveService();

        var drives = service.GetFixedDrives();

        Assert.All(
            drives,
            drive =>
            {
                Assert.NotEmpty(drive.Name);
                Assert.True(drive.TotalBytes >= 0);
                Assert.InRange(drive.FreeBytes, 0, drive.TotalBytes);
                Assert.Equal(
                    drive.TotalBytes - drive.FreeBytes,
                    drive.UsedBytes);
            });
    }
}
