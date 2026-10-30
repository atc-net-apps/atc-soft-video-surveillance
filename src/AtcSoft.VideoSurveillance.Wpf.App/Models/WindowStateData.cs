namespace AtcSoft.VideoSurveillance.Wpf.App.Models;

/// <summary>
/// Persisted window position, size, and state.
/// </summary>
public sealed class WindowStateData
{
    public double Left { get; set; } = double.NaN;

    public double Top { get; set; } = double.NaN;

    public double Width { get; set; } = 1500;

    public double Height { get; set; } = 1000;

    public bool IsMaximized { get; set; }
}