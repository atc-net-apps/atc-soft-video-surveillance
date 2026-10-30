namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// Which camera tiles the live view shows, and in a grid of what size: the
/// cameras that match the filter, one page of them at a time when the grid
/// has a fixed size.
/// </summary>
/// <param name="Shown">For each camera, in their order, whether its tile is shown.</param>
/// <param name="Rows">The rows of the grid.</param>
/// <param name="Columns">The columns of the grid.</param>
/// <param name="MatchCount">The number of cameras that match the filter.</param>
/// <param name="PageIndex">The page shown, counted from zero.</param>
/// <param name="PageCount">The number of pages, at least one.</param>
/// <param name="PageLabel">The page as text ("2 / 3"); empty when there is only one page.</param>
public sealed record LiveGridArrangement(
    IReadOnlyList<bool> Shown,
    int Rows,
    int Columns,
    int MatchCount,
    int PageIndex,
    int PageCount,
    string PageLabel)
{
    /// <summary>
    /// Works out the arrangement.
    /// </summary>
    /// <param name="cameras">Each camera's name and description, in the order of the tiles.</param>
    /// <param name="filter">The text typed in the filter box; every word of it has to be in the name or the description.</param>
    /// <param name="preset">The grid's size.</param>
    /// <param name="pageIndex">The page asked for; one that does not exist becomes the nearest that does.</param>
    /// <returns>The arrangement.</returns>
    public static LiveGridArrangement Arrange(
        IReadOnlyList<(string Name, string Description)> cameras,
        string? filter,
        GridPreset preset,
        int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(cameras);

        var words = (filter ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matches = new bool[cameras.Count];
        var matchCount = 0;
        for (var i = 0; i < cameras.Count; i++)
        {
            matches[i] = Matches(cameras[i], words);
            if (matches[i])
            {
                matchCount++;
            }
        }

        var page = GridPager.Calculate(matchCount, preset, pageIndex);

        // The page counts over the cameras that match, not over all of them.
        var shown = new bool[cameras.Count];
        var matchIndex = 0;
        for (var i = 0; i < cameras.Count; i++)
        {
            if (!matches[i])
            {
                continue;
            }

            shown[i] = page.Shows(matchIndex);
            matchIndex++;
        }

        // On automatic the grid is as wide as it is high for the cameras
        // that are shown; a fixed grid keeps its size.
        var (rows, columns) = preset == GridPreset.Auto
            ? GridLayoutHelper.CalculateGridDimensions(matchCount)
            : (page.Rows, page.Columns);

        return new LiveGridArrangement(shown, rows, columns, matchCount, page.PageIndex, page.PageCount, page.Label);
    }

    private static bool Matches(
        (string Name, string Description) camera,
        string[] words)
        => words.All(word =>
            (camera.Name?.Contains(word, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
            (camera.Description?.Contains(word, StringComparison.CurrentCultureIgnoreCase) ?? false));
}