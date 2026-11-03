namespace AtcSoft.Video.Surveillance.Helpers.MotionDetection;

/// <summary>
/// Tells the ghost of something that left from something that is there. Something that lay still
/// long enough to be learnt (a cat asleep on the floor, a parked car), and then left, leaves a blob
/// of foreground where it was: the scene behind it is new to the background. Along the blob's
/// outline, the learnt background has the thing's edge and the picture has none (floor runs into
/// floor); something that is there has its edge in the picture. Judged once per blob and picture,
/// so a decision cannot creep from pixel to pixel into what is really there.
/// </summary>
internal static class GhostCheck
{
    // The outline's edge, in grey levels, the background must have before the blob can be a ghost:
    // a blob over an even floor with an even background says nothing either way.
    private const double MinBackgroundEdge = 8;

    // A ghost's outline is at most this sharp in the picture, as a share of the background's.
    private const double MaxPictureEdgeShare = 0.5;

    // How long, on average, a blob's pixels must have been foreground before it can be a ghost:
    // a ghost stays; a swaying branch or a snowflake leaves a ghost-like place for a moment only.
    private const float MinSeconds = 1;

    // The edge is the difference this far either side: the cleaned mask may lie a pixel or two
    // outside the thing's edge.
    private const int Reach = 2;

    /// <summary>
    /// Gets whether a blob's pixels have been foreground long enough, on average, to be judged.
    /// </summary>
    /// <param name="blob">The blob, with its <see cref="Blob.Label"/>.</param>
    /// <param name="labels">The label of each pixel.</param>
    /// <param name="roots">The blob each label belongs to.</param>
    /// <param name="ages">How long each pixel has been foreground, in seconds.</param>
    /// <param name="width">The picture's width.</param>
    public static bool HasLasted(
        Blob blob,
        int[] labels,
        int[] roots,
        float[] ages,
        int width)
    {
        double total = 0;
        var count = 0;
        for (var y = blob.MinY; y <= blob.MaxY; y++)
        {
            var row = y * width;
            for (var x = blob.MinX; x <= blob.MaxX; x++)
            {
                var i = row + x;
                if (Inside(labels, roots, i, blob.Label))
                {
                    total += ages[i];
                    count++;
                }
            }
        }

        return count > 0 && total / count >= MinSeconds;
    }

    /// <summary>
    /// Gets whether a blob is a ghost: its outline is an edge in the background and not in the
    /// picture.
    /// </summary>
    /// <param name="blob">The blob, with its <see cref="Blob.Label"/>.</param>
    /// <param name="labels">The label of each pixel, as <see cref="ConnectedComponents.Find"/> left them.</param>
    /// <param name="roots">The blob each label belongs to.</param>
    /// <param name="frame">The (blurred) picture.</param>
    /// <param name="mean">The learnt background.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">The picture's height.</param>
    public static bool IsGhost(
        Blob blob,
        int[] labels,
        int[] roots,
        byte[] frame,
        ReadOnlySpan<float> mean,
        int width,
        int height)
    {
        double pictureEdge = 0;
        double backgroundEdge = 0;
        var outline = 0;
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

                var across = Reach * width;
                pictureEdge += Math.Abs(frame[i + Reach] - frame[i - Reach]) + Math.Abs(frame[i + across] - frame[i - across]);
                backgroundEdge += Math.Abs(mean[i + Reach] - mean[i - Reach]) + Math.Abs(mean[i + across] - mean[i - across]);
                outline++;
            }
        }

        if (outline == 0)
        {
            return false;
        }

        backgroundEdge /= outline;
        pictureEdge /= outline;
        return backgroundEdge >= MinBackgroundEdge && pictureEdge < MaxPictureEdgeShare * backgroundEdge;
    }

    private static bool Inside(
        int[] labels,
        int[] roots,
        int i,
        int label)
        => labels[i] != 0 && roots[labels[i]] == label;
}