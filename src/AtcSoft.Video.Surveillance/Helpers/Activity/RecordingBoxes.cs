namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// The motion boxes of a recording, picture by picture, for showing them over its playback: the
/// recording is run through the detector with its camera's settings (as *Analyse recording* reads
/// it), and a player asks for the boxes at its position. The activity index keeps how much moved
/// each second, not where; these are where, and are kept in memory only.
/// </summary>
public sealed class RecordingBoxes
{
    // A position further than this past the last analysed picture before it has no boxes: the
    // analysis has not reached it, or the picture there had none.
    private static readonly TimeSpan Reach = TimeSpan.FromSeconds(0.5);

    private readonly TimeSpan[] positions;
    private readonly IReadOnlyList<BoundingBox>[] boxes;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingBoxes"/> class.
    /// </summary>
    /// <param name="analysisWidth">The width of the pictures the boxes are in.</param>
    /// <param name="analysisHeight">Their height.</param>
    /// <param name="pictures">Each analysed picture's position in the recording and its boxes, in order.</param>
    public RecordingBoxes(
        int analysisWidth,
        int analysisHeight,
        IReadOnlyList<(TimeSpan Position, IReadOnlyList<BoundingBox> Boxes)> pictures)
    {
        ArgumentNullException.ThrowIfNull(pictures);

        AnalysisWidth = analysisWidth;
        AnalysisHeight = analysisHeight;
        positions = [.. pictures.Select(p => p.Position)];
        boxes = [.. pictures.Select(p => p.Boxes)];
    }

    /// <summary>
    /// Gets the width of the pictures the boxes are in.
    /// </summary>
    public int AnalysisWidth { get; }

    /// <summary>
    /// Gets their height.
    /// </summary>
    public int AnalysisHeight { get; }

    /// <summary>
    /// Gets how many pictures were analysed.
    /// </summary>
    public int Count
        => positions.Length;

    /// <summary>
    /// Runs a recording's pictures through the detector and keeps each picture's boxes.
    /// </summary>
    /// <param name="pictures">The recording's pictures from its start, grey at the analysis size, with how far in each is.</param>
    /// <param name="width">The analysis width.</param>
    /// <param name="height">The analysis height.</param>
    /// <param name="detector">The motion detector, with the camera's settings.</param>
    /// <param name="cancellationToken">Stops the analysis.</param>
    /// <returns>The boxes.</returns>
    public static RecordingBoxes Analyse(
        IEnumerable<(TimeSpan Position, byte[] Pixels)> pictures,
        int width,
        int height,
        IMotionDetector detector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pictures);
        ArgumentNullException.ThrowIfNull(detector);

        var found = new List<(TimeSpan Position, IReadOnlyList<BoundingBox> Boxes)>();
        detector.Reset();
        foreach (var (position, pixels) in pictures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = detector.Process(new MotionFrame(pixels, width, height, DateTimeOffset.UnixEpoch + position));
            found.Add((position, result.Boxes));
        }

        return new RecordingBoxes(width, height, found);
    }

    /// <summary>
    /// Gets the boxes at a position: those of the last analysed picture at or before it, when it is
    /// close enough; none otherwise.
    /// </summary>
    /// <param name="position">The position in the recording.</param>
    public IReadOnlyList<BoundingBox> At(TimeSpan position)
    {
        var index = Array.BinarySearch(positions, position);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return index >= 0 && position - positions[index] <= Reach
            ? boxes[index]
            : [];
    }
}