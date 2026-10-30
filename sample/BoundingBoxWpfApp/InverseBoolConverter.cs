namespace BoundingBoxWpfApp;

/// <summary>
/// Turns <see langword="true"/> into <see langword="false"/> and back.
/// </summary>
public sealed class InverseBoolConverter : System.Windows.Data.IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => value is not true;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => value is not true;
}