namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Decides from two pictures whether something moved, and where. Shared by
/// the desktop app and the server, so both find the same motion in the same
/// pictures.
/// </summary>
public static class MotionFrameAnalyzer
{
    /// <summary>
    /// Compares two grayscale pictures of the same size.
    /// </summary>
    /// <param name="previous">The earlier picture, one byte per pixel, row by row.</param>
    /// <param name="current">The later picture, in the same layout.</param>
    /// <param name="analysisWidth">The width of both pictures.</param>
    /// <param name="analysisHeight">The height of both pictures.</param>
    /// <param name="settings">The sensitivity and the rules for the boxes.</param>
    /// <returns>How much of the picture changed, and where.</returns>
    public static MotionAnalysisResult Analyze(
        byte[] previous,
        byte[] current,
        int analysisWidth,
        int analysisHeight,
        MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(settings);

        // Pictures of different sizes cannot be compared: the analysis size
        // was changed between the two.
        if (previous.Length != current.Length || current.Length != analysisWidth * analysisHeight)
        {
            return MotionAnalysisResult.None;
        }

        // Threshold based on sensitivity (0-100)
        // Map sensitivity to a practical threshold range (15-35)
        // Higher sensitivity (100) = lower threshold (15) - detects subtle changes
        // Lower sensitivity (0) = higher threshold (35) - requires larger changes
        var threshold = (int)(35 - (settings.Sensitivity * 0.2)); // Range: 15-35

        // Use a grid-based approach to filter out scattered noise
        // Divide frame into cells and only consider cells with significant motion
        const int cellSize = 20; // Each cell is 20x20 pixels
        var gridWidth = (analysisWidth + cellSize - 1) / cellSize;
        var gridHeight = (analysisHeight + cellSize - 1) / cellSize;
        var cellChangeCounts = new int[gridWidth * gridHeight];
        var isActiveCell = new bool[gridWidth * gridHeight];

        var totalChangedPixels = 0;

        // Count changed pixels per cell
        for (var y = 0; y < analysisHeight; y++)
        {
            for (var x = 0; x < analysisWidth; x++)
            {
                var i = (y * analysisWidth) + x;
                var diff = Math.Abs(current[i] - previous[i]);

                if (diff <= threshold)
                {
                    continue;
                }

                totalChangedPixels++;

                // Determine which cell this pixel belongs to
                var cellX = x / cellSize;
                var cellY = y / cellSize;
                var cellIndex = (cellY * gridWidth) + cellX;
                cellChangeCounts[cellIndex]++;
            }
        }

        var changePercent = (double)totalChangedPixels / previous.Length * 100.0;

        // Minimum changed pixels per cell to consider it as having motion (not just noise)
        // At 20x20 = 400 pixels per cell, require at least 8% (32 pixels) to count as motion
        var minChangedPerCell = Math.Max(8, (cellSize * cellSize) / 12);

        // Mark active cells
        for (var i = 0; i < cellChangeCounts.Length; i++)
        {
            isActiveCell[i] = cellChangeCounts[i] >= minChangedPerCell;
        }

        // Find all connected components (clusters) of active cells using flood fill
        var visited = new bool[gridWidth * gridHeight];
        var allClusters = new List<List<(int X, int Y)>>();

        for (var cellY = 0; cellY < gridHeight; cellY++)
        {
            for (var cellX = 0; cellX < gridWidth; cellX++)
            {
                var cellIndex = (cellY * gridWidth) + cellX;
                if (!isActiveCell[cellIndex] || visited[cellIndex])
                {
                    continue;
                }

                // Found an unvisited active cell - flood fill to find the cluster
                var cluster = FloodFillCluster(
                    cellX,
                    cellY,
                    gridWidth,
                    gridHeight,
                    isActiveCell,
                    visited);

                if (cluster.Count > 0)
                {
                    allClusters.Add(cluster);
                }
            }
        }

        // Create bounding boxes for all clusters that meet the minimum area requirement
        var boundingBoxes = new List<BoundingBox>();

        foreach (var cluster in allClusters)
        {
            // Find bounds of the cluster
            var minCellX = cluster.Min(c => c.X);
            var maxCellX = cluster.Max(c => c.X);
            var minCellY = cluster.Min(c => c.Y);
            var maxCellY = cluster.Max(c => c.Y);

            // Convert cell coordinates back to pixel coordinates
            var pixelMinX = minCellX * cellSize;
            var pixelMinY = minCellY * cellSize;
            var pixelMaxX = Math.Min(analysisWidth - 1, ((maxCellX + 1) * cellSize) - 1);
            var pixelMaxY = Math.Min(analysisHeight - 1, ((maxCellY + 1) * cellSize) - 1);

            var width = pixelMaxX - pixelMinX + 1;
            var height = pixelMaxY - pixelMinY + 1;
            var area = width * height;

            // Only create bounding box if area exceeds minimum
            if (area < settings.BoundingBox.MinArea)
            {
                continue;
            }

            // Add padding
            var padding = settings.BoundingBox.Padding;
            var paddedX = Math.Max(0, pixelMinX - padding);
            var paddedY = Math.Max(0, pixelMinY - padding);
            var paddedWidth = Math.Min(analysisWidth - paddedX, width + (2 * padding));
            var paddedHeight = Math.Min(analysisHeight - paddedY, height + (2 * padding));

            boundingBoxes.Add(new BoundingBox { X = paddedX, Y = paddedY, Width = paddedWidth, Height = paddedHeight });
        }

        // Sort by area (largest first) for consistent ordering
        boundingBoxes.Sort((a, b) => b.Area.CompareTo(a.Area));

        return new MotionAnalysisResult(changePercent, boundingBoxes);
    }

    /// <summary>
    /// Flood fill to find all connected active cells starting from a seed cell.
    /// Uses 8-connectivity (includes diagonal neighbors).
    /// </summary>
    private static List<(int X, int Y)> FloodFillCluster(
        int startX,
        int startY,
        int gridWidth,
        int gridHeight,
        bool[] isActiveCell,
        bool[] visited)
    {
        var cluster = new List<(int X, int Y)>();
        var stack = new Stack<(int X, int Y)>();
        stack.Push((startX, startY));

        // 8-connectivity offsets (including diagonals)
        int[] dx = [-1, 0, 1, -1, 1, -1, 0, 1];
        int[] dy = [-1, -1, -1, 0, 0, 1, 1, 1];

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            var index = (y * gridWidth) + x;

            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            {
                continue;
            }

            if (visited[index] || !isActiveCell[index])
            {
                continue;
            }

            visited[index] = true;
            cluster.Add((x, y));

            // Add all 8 neighbors
            for (var i = 0; i < 8; i++)
            {
                var nx = x + dx[i];
                var ny = y + dy[i];
                if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight)
                {
                    var neighborIndex = (ny * gridWidth) + nx;
                    if (!visited[neighborIndex] && isActiveCell[neighborIndex])
                    {
                        stack.Push((nx, ny));
                    }
                }
            }
        }

        return cluster;
    }
}