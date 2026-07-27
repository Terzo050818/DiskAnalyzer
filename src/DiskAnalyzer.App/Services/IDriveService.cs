using DiskAnalyzer.App.Models;

namespace DiskAnalyzer.App.Services;

public interface IDriveService
{
    IReadOnlyList<DriveItem> GetFixedDrives();
}
