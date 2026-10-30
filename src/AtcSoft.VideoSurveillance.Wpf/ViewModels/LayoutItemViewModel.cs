namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// Wraps a generated Layout model for DataGrid binding.
/// </summary>
public sealed class LayoutItemViewModel
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Rows { get; init; }

    public int Columns { get; init; }

    public int CameraCount { get; init; }

    public string GridDescription => $"{Rows}x{Columns}";

    /// <summary>
    /// Creates a <see cref="LayoutItemViewModel"/> from a generated <see cref="Layout"/> model.
    /// </summary>
    public static LayoutItemViewModel FromLayout(Layout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        return new LayoutItemViewModel
        {
            Id = layout.Id,
            Name = layout.Name,
            Rows = layout.Rows,
            Columns = layout.Columns,
            CameraCount = layout.Cameras?.Count ?? 0,
        };
    }
}