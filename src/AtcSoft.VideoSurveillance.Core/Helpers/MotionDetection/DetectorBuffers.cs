namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The working arrays of one detector, kept from picture to picture. At 800×600 each is larger
/// than the .NET large-object threshold; allocating them for every picture of every camera,
/// all day, would churn the large-object heap. A detector is used one picture at a time, so one
/// set per detector is enough.
/// </summary>
internal sealed class DetectorBuffers
{
    public byte[] Blurred { get; private set; } = [];

    public ushort[] Rows { get; private set; } = [];

    public byte[] Mask { get; private set; } = [];

    public byte[] Temp { get; private set; } = [];

    /// <summary>
    /// Gets the pixels of the objects shown in the last picture that have travelled for certain
    /// (1), kept out of the background's flicker.
    /// </summary>
    public byte[] Held { get; private set; } = [];

    public int[] Labels { get; private set; } = [];

    public List<int> Parent { get; } = [];

    public int[] Roots { get; private set; } = [];

    public int[] MinX { get; private set; } = [];

    public int[] MinY { get; private set; } = [];

    public int[] MaxX { get; private set; } = [];

    public int[] MaxY { get; private set; } = [];

    public int[] Count { get; private set; } = [];

    /// <summary>
    /// Makes sure the picture-sized arrays fit a picture of this many pixels.
    /// </summary>
    public void EnsurePixels(int pixels)
    {
        if (Blurred.Length != pixels)
        {
            Blurred = new byte[pixels];
            Rows = new ushort[pixels];
            Mask = new byte[pixels];
            Temp = new byte[pixels];
            Held = new byte[pixels];
            Labels = new int[pixels];
        }
    }

    /// <summary>
    /// Makes sure the per-label arrays fit this many labels, and clears the counts.
    /// </summary>
    public void EnsureLabels(int labels)
    {
        if (Roots.Length < labels)
        {
            var size = Math.Max(labels, Roots.Length * 2);
            Roots = new int[size];
            MinX = new int[size];
            MinY = new int[size];
            MaxX = new int[size];
            MaxY = new int[size];
            Count = new int[size];
        }

        Array.Clear(Count, 0, labels);
    }
}