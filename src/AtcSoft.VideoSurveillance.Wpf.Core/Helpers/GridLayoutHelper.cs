namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// Helper class for calculating optimal grid layout dimensions.
/// </summary>
public static class GridLayoutHelper
{
    /// <summary>
    /// Default screen aspect ratio (4:3).
    /// </summary>
    private const double DefaultAspectRatio = 4.0 / 3.0;

    /// <summary>
    /// Calculates the optimal number of rows for a grid based on the number of items
    /// and a 4:3 screen aspect ratio.
    /// </summary>
    /// <param name="itemCount">The number of items to display in the grid.</param>
    /// <returns>The optimal number of rows.</returns>
    public static int CalculateRowCount(int itemCount)
        => CalculateRowCount(itemCount, DefaultAspectRatio);

    /// <summary>
    /// Calculates the optimal number of rows for a grid based on the number of items
    /// and a specified aspect ratio.
    /// </summary>
    /// <param name="itemCount">The number of items to display in the grid.</param>
    /// <param name="aspectRatio">The target aspect ratio (width/height).</param>
    /// <returns>The optimal number of rows.</returns>
    public static int CalculateRowCount(
        int itemCount,
        double aspectRatio)
    {
        switch (itemCount)
        {
            case <= 0:
                return 0;
            case <= 2:
                return 1;
            case <= 4:
                return 2;
        }

        // Calculate optimal rows based on aspect ratio
        // For a 4:3 aspect ratio, we want more columns than rows
        var sqrt = Math.Sqrt(itemCount / aspectRatio);
        var rows = (int)Math.Ceiling(sqrt);

        // Ensure we have enough cells
        var cols = (int)Math.Ceiling((double)itemCount / rows);
        while (rows * cols < itemCount)
        {
            rows++;
        }

        return rows;
    }

    /// <summary>
    /// Calculates the optimal number of columns for a grid based on the number of items
    /// and a 4:3 screen aspect ratio.
    /// </summary>
    /// <param name="itemCount">The number of items to display in the grid.</param>
    /// <returns>The optimal number of columns.</returns>
    public static int CalculateColumnCount(int itemCount)
        => CalculateColumnCount(itemCount, DefaultAspectRatio);

    /// <summary>
    /// Calculates the optimal number of columns for a grid based on the number of items
    /// and a specified aspect ratio.
    /// </summary>
    /// <param name="itemCount">The number of items to display in the grid.</param>
    /// <param name="aspectRatio">The target aspect ratio (width/height).</param>
    /// <returns>The optimal number of columns.</returns>
    public static int CalculateColumnCount(
        int itemCount,
        double aspectRatio)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        var rows = CalculateRowCount(itemCount, aspectRatio);
        return (int)Math.Ceiling((double)itemCount / rows);
    }

    /// <summary>
    /// Calculates both row and column counts for optimal grid layout.
    /// </summary>
    /// <param name="itemCount">The number of items to display in the grid.</param>
    /// <returns>A tuple containing (rows, columns).</returns>
    public static (int Rows, int Columns) CalculateGridDimensions(int itemCount)
        => CalculateGridDimensions(itemCount, DefaultAspectRatio);

    /// <summary>
    /// Calculates both row and column counts for optimal grid layout.
    /// </summary>
    /// <param name="itemCount">The number of items to display in the grid.</param>
    /// <param name="aspectRatio">The target aspect ratio (width/height).</param>
    /// <returns>A tuple containing (rows, columns).</returns>
    public static (int Rows, int Columns) CalculateGridDimensions(
        int itemCount,
        double aspectRatio)
    {
        var rows = CalculateRowCount(itemCount, aspectRatio);
        var columns = CalculateColumnCount(itemCount, aspectRatio);
        return (rows, columns);
    }
}