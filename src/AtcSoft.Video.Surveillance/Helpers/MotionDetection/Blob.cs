namespace AtcSoft.Video.Surveillance.Helpers.MotionDetection;

/// <summary>
/// A connected group of foreground pixels: its box and how many pixels it has.
/// </summary>
internal sealed record Blob(
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    int Pixels)
{
    public double CenterX
        => (MinX + MaxX + 1) / 2.0;

    public double CenterY
        => (MinY + MaxY + 1) / 2.0;

    public int Width
        => MaxX - MinX + 1;

    public int Height
        => MaxY - MinY + 1;

    /// <summary>
    /// Gets one blob covering both.
    /// </summary>
    public Blob Union(Blob other)
        => new(
            Math.Min(MinX, other.MinX),
            Math.Min(MinY, other.MinY),
            Math.Max(MaxX, other.MaxX),
            Math.Max(MaxY, other.MaxY),
            Pixels + other.Pixels);

    /// <summary>
    /// Determines whether the boxes are within a gap of each other.
    /// </summary>
    /// <summary>
    /// Gets the label of the blob's pixels in the label image it was found in
    /// (<see cref="ConnectedComponents.Find"/>); 0 for a blob made by joining others.
    /// </summary>
    public int Label { get; init; }

    public bool IsNear(
        Blob other,
        int gap)
        => MinX - gap <= other.MaxX && other.MinX - gap <= MaxX &&
           MinY - gap <= other.MaxY && other.MinY - gap <= MaxY;
}