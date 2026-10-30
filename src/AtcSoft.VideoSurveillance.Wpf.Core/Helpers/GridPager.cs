namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// Works out what a camera grid shows: how many rows and columns, and which
/// of the cameras are on the page. With <see cref="GridPreset.Auto"/> the
/// grid grows with the cameras and shows them all; a fixed grid shows one
/// page of them at a time.
/// </summary>
public static class GridPager
{
    /// <summary>
    /// Calculates the page a grid shows.
    /// </summary>
    /// <param name="cameraCount">The number of cameras to spread over the pages.</param>
    /// <param name="preset">The grid's size.</param>
    /// <param name="pageIndex">The page asked for, counted from zero. One that does not exist becomes the nearest that does.</param>
    /// <returns>The grid's size and the cameras on the page.</returns>
    public static GridPage Calculate(
        int cameraCount,
        GridPreset preset,
        int pageIndex)
    {
        cameraCount = Math.Max(0, cameraCount);

        if (preset == GridPreset.Auto)
        {
            return new GridPage(
                Rows: GridLayoutHelper.CalculateRowCount(cameraCount),
                Columns: 0,
                PageCount: 1,
                PageIndex: 0,
                FirstVisible: 0,
                VisibleCount: cameraCount);
        }

        var side = (int)preset;
        var perPage = side * side;
        var pageCount = Math.Max(1, (int)Math.Ceiling((double)cameraCount / perPage));
        var page = Math.Clamp(pageIndex, 0, pageCount - 1);
        var first = page * perPage;

        return new GridPage(
            Rows: side,
            Columns: side,
            PageCount: pageCount,
            PageIndex: page,
            FirstVisible: first,
            VisibleCount: Math.Clamp(cameraCount - first, 0, perPage));
    }

    /// <summary>
    /// Gets the preset for a number of cameras along a side ("2" is 2 x 2).
    /// Anything else is <see cref="GridPreset.Auto"/>.
    /// </summary>
    public static GridPreset ParsePreset(string? side)
        => int.TryParse(side, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
           Enum.IsDefined((GridPreset)value)
            ? (GridPreset)value
            : GridPreset.Auto;
}