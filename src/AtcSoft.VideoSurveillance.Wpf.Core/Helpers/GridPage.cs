namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// What a camera grid shows.
/// </summary>
/// <param name="Rows">The rows of the grid.</param>
/// <param name="Columns">The columns of the grid; zero when they follow from the rows and the number of cameras.</param>
/// <param name="PageCount">The number of pages, at least one.</param>
/// <param name="PageIndex">The page shown, counted from zero.</param>
/// <param name="FirstVisible">The index of the first camera on the page.</param>
/// <param name="VisibleCount">The number of cameras on the page.</param>
public sealed record GridPage(
    int Rows,
    int Columns,
    int PageCount,
    int PageIndex,
    int FirstVisible,
    int VisibleCount)
{
    /// <summary>
    /// Gets the page as text ("2 / 3"); empty when there is only one page.
    /// </summary>
    public string Label
        => PageCount > 1
            ? string.Create(CultureInfo.InvariantCulture, $"{PageIndex + 1} / {PageCount}")
            : string.Empty;

    /// <summary>
    /// Gets whether the camera at an index is on the page.
    /// </summary>
    public bool Shows(int cameraIndex)
        => cameraIndex >= FirstVisible && cameraIndex < FirstVisible + VisibleCount;
}