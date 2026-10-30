namespace AtcSoft.VideoSurveillance.Wpf.Core.ValueConverters;

/// <summary>
/// Multi-value converter that returns the first value if it's not null, otherwise the second value (default).
/// Used to apply per-camera overrides when set, falling back to application defaults.
/// </summary>
/// <remarks>
/// Binding order:
/// - values[0]: Camera override value (nullable) - e.g., Camera.Overrides.ShowOverlayDescription
/// - values[1]: Application default value - e.g., CameraGrid.ShowOverlayDescription
/// </remarks>
public sealed class OverrideOrDefaultMultiValueConverter : IMultiValueConverter
{
    /// <summary>
    /// Gets the singleton instance of the converter.
    /// </summary>
    public static OverrideOrDefaultMultiValueConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(
        object?[]? values,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (values is null || values.Length < 2)
        {
            return DependencyProperty.UnsetValue;
        }

        // values[0] = override value (nullable)
        // values[1] = default value
        var overrideValue = values[0];
        var defaultValue = values[1];

        // If override is set (not null and not UnsetValue), use it
        if (overrideValue is not null && overrideValue != DependencyProperty.UnsetValue)
        {
            return overrideValue;
        }

        // Otherwise use the default
        return defaultValue;
    }

    /// <inheritdoc />
    public object[] ConvertBack(
        object? value,
        Type[] targetTypes,
        object? parameter,
        CultureInfo culture)
        => throw new NotSupportedException();
}