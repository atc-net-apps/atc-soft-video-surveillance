namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// What <see cref="BuiltInMotionDetector"/> saw in one picture, for a person tuning it: how far it
/// got, the foreground after the morphology, and every blob with what became of it. Only made when
/// the detector is asked to keep it (<see cref="BuiltInMotionDetector.KeepDiagnostics"/>).
/// </summary>
public sealed class MotionDiagnostics
{
    private readonly byte[] foreground;
    private readonly byte[]? ignored;

    internal MotionDiagnostics(
        MotionDiagnosticsStage stage,
        int width,
        int height,
        byte[] foreground,
        byte[]? ignored,
        IReadOnlyList<MotionCandidate> candidates)
    {
        Stage = stage;
        Width = width;
        Height = height;
        this.foreground = foreground;
        this.ignored = ignored;
        Candidates = candidates;
    }

    /// <summary>
    /// Gets how far the detector got with the picture.
    /// </summary>
    public MotionDiagnosticsStage Stage { get; }

    /// <summary>
    /// Gets the width of the analysed picture.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the analysed picture.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the blobs found, in analysed pixels, with what became of each; empty unless the
    /// picture was <see cref="MotionDiagnosticsStage.Analysed"/>.
    /// </summary>
    public IReadOnlyList<MotionCandidate> Candidates { get; }

    /// <summary>
    /// Gets whether the camera has ignore zones (<see cref="IsIgnored"/>).
    /// </summary>
    public bool HasIgnored
        => ignored is not null;

    /// <summary>
    /// Determines whether a pixel was foreground after the morphology (the mask the blobs are
    /// found in); none is before the picture is analysed.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when it was foreground.</returns>
    public bool IsForeground(
        int x,
        int y)
    {
        var i = (y * Width) + x;
        return (foreground[i >> 3] & (1 << (i & 7))) != 0;
    }

    /// <summary>
    /// Determines whether a pixel is in an ignore zone: it is never foreground.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when it is ignored.</returns>
    public bool IsIgnored(
        int x,
        int y)
        => ignored is { } mask && mask[(y * Width) + x] != 0;

    // A mask of one byte a pixel packed to one bit a pixel, so a run that keeps every picture's
    // diagnostics costs an eighth of its pictures.
    internal static byte[] Pack(
        byte[] mask,
        int pixels)
    {
        var packed = new byte[(pixels + 7) / 8];
        for (var i = 0; i < pixels; i++)
        {
            if (mask[i] != 0)
            {
                packed[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        return packed;
    }
}