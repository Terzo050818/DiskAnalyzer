using System.IO;
using DiskAnalyzer.App.Models;

namespace DiskAnalyzer.App.Services;

public sealed class DriveService : IDriveService
{
    public IReadOnlyList<DriveItem> GetFixedDrives()
    {
        var drives = new List<DriveItem>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                {
                    continue;
                }

                drives.Add(new DriveItem(
                    drive.Name,
                    drive.VolumeLabel,
                    drive.TotalSize,
                    drive.AvailableFreeSpace));
            }
            catch (IOException)
            {
                // A drive can disappear or become unavailable while it is queried.
            }
            catch (UnauthorizedAccessException)
            {
                // A single inaccessible drive must not prevent application startup.
            }
        }

        return drives;
    }
}
