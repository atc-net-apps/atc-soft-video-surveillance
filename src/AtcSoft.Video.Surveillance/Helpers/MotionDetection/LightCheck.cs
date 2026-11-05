namespace AtcSoft.Video.Surveillance.Helpers.MotionDetection;

/// <summary>
/// Tells light from things. Light falling on part of a surface, or a reflection on it, makes it
/// brighter or darker but draws no edge of its own: where it ends, the picture's edge is the
/// background's, scaled by the change of brightness, and the change fades out. A thing ends at its
/// own outline. A blob whose outline has (almost) no edge of its own is light only.
/// </summary>
internal static class LightCheck
{
    // The edge is the difference this far either side, as in GhostCheck.
    private const int Reach = 2;

    // Added to both sides of the brightness ratio, so a dark pixel's small change is not a big ratio.
    private const double RatioFloor = 8;

    /// <summary>
    /// Whether the blob is light only: too little of its outline has an edge of its own.
    /// </summary>
    /// <param name="blob">The blob, with its label.</param>
    /// <param name="labels">The label of each pixel.</param>
    /// <param name="roots">The blob each label belongs to.</param>
    /// <param name="frame">The (blurred) picture.</param>
    /// <param name="mean">The background.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">The picture's height.</param>
    /// <param name="zoneOf">The watch zone of each pixel (0 is the camera's), or null without watch zones.</param>
    /// <param name="minEdges">How much an edge must differ from the background's, in grey levels, per watch zone.</param>
    /// <param name="minOutlineShare">The share of its outline a thing has an edge of its own on, at least.</param>
    /// <returns>True for light only.</returns>
    public static bool IsLight(
        Blob blob,
        int[] labels,
        int[] roots,
        byte[] frame,
        ReadOnlySpan<float> mean,
        int width,
        int height,
        byte[]? zoneOf,
        double[] minEdges,
        double minOutlineShare)
    {
        var outline = 0;
        var edges = 0;
        var across = Reach * width;
        for (var y = Math.Max(blob.MinY, Reach); y <= Math.Min(blob.MaxY, height - 1 - Reach); y++)
        {
            var row = y * width;
            for (var x = Math.Max(blob.MinX, Reach); x <= Math.Min(blob.MaxX, width - 1 - Reach); x++)
            {
                var i = row + x;
                if (!Inside(labels, roots, i, blob.Label) ||
                    (Inside(labels, roots, i - 1, blob.Label) && Inside(labels, roots, i + 1, blob.Label) &&
                     Inside(labels, roots, i - width, blob.Label) && Inside(labels, roots, i + width, blob.Label)))
                {
                    continue;
                }

                outline++;

                // The background's edge as this pixel's change of brightness would make it.
                var ratio = (frame[i] + RatioFloor) / (mean[i] + RatioFloor);
                var alongX = frame[i + Reach] - frame[i - Reach] - (ratio * (mean[i + Reach] - mean[i - Reach]));
                var alongY = frame[i + across] - frame[i - across] - (ratio * (mean[i + across] - mean[i - across]));
                if (Math.Abs(alongX) + Math.Abs(alongY) >= minEdges[zoneOf is null ? 0 : zoneOf[i]])
                {
                    edges++;
                }
            }
        }

        // A blob with no outline within reach of the picture's edge has nothing to tell by: kept.
        return outline > 0 && edges < minOutlineShare * outline;
    }

    private static bool Inside(
        int[] labels,
        int[] roots,
        int i,
        int label)
        => labels[i] != 0 && roots[labels[i]] == label;
}