using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DiskAnalyzer.App.Converters;

public sealed class FileSizeConverter : IValueConverter
{
    private static readonly string[] Units =
        ["Bytes", "KB", "MB", "GB", "TB", "PB"];

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return value is long bytes
            ? Format(bytes, culture)
            : DependencyProperty.UnsetValue;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public static string Format(
        long bytes,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytes),
                "File size cannot be negative.");
        }

        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes.ToString("N0", culture)} {Units[unitIndex]}"
            : $"{value.ToString("0.##", culture)} {Units[unitIndex]}";
    }
}
