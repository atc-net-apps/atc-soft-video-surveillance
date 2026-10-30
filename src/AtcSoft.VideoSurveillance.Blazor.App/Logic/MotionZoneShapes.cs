namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Where the web zone editor draws: the picture is <see cref="Height"/> high and as wide as its
/// shape gives, the zones and handles on it. Numbers are written for SVG through <see cref="SvgNumber"/>.
/// </summary>
public static class MotionZoneShapes
{
    /// <summary>
    /// The height of the drawing, in its own units.
    /// </summary>
    public const double Height = 1000;

    /// <summary>
    /// The size of a corner's handle, in the drawing's units.
    /// </summary>
    public const double HandleSize = 16;

    /// <summary>
    /// Gets the width of the drawing for a picture of the given shape.
    /// </summary>
    /// <param name="aspectRatio">The picture's width over its height as shown.</param>
    public static double Width(double aspectRatio)
        => Height * (aspectRatio > 0 ? aspectRatio : MotionZoneEditing.DefaultAspectRatio);

    /// <summary>
    /// Gets the shape of the picture as shown: the snapshot's, turned by the quarter turns, or the
    /// shape of a picture while there is none.
    /// </summary>
    /// <param name="size">The snapshot's size, or <see langword="null"/>.</param>
    /// <param name="quarters">The quarter turns from the snapshot to the picture as shown.</param>
    public static double AspectRatio(
        (int Width, int Height)? size,
        int quarters)
        => size is { Width: > 0, Height: > 0 } picture
            ? quarters % 2 == 1
                ? (double)picture.Height / picture.Width
                : (double)picture.Width / picture.Height
            : MotionZoneEditing.DefaultAspectRatio;

    /// <summary>
    /// Gets where the snapshot goes in the drawing: its size before the turn, and the turn about the
    /// middle that makes it fill the drawing.
    /// </summary>
    /// <param name="width">The width of the drawing.</param>
    /// <param name="quarters">The quarter turns from the snapshot to the picture as shown.</param>
    public static (double Width, double Height, string Transform) Picture(
        double width,
        int quarters)
    {
        var (imageWidth, imageHeight) = quarters % 2 == 1 ? (Height, width) : (width, Height);
        var transform = string.Create(
            CultureInfo.InvariantCulture,
            $"translate({SvgNumber.Format(width / 2)} {SvgNumber.Format(Height / 2)}) rotate({quarters * 90}) translate({SvgNumber.Format(-imageWidth / 2)} {SvgNumber.Format(-imageHeight / 2)})");
        return (imageWidth, imageHeight, transform);
    }

    /// <summary>
    /// Gets a zone's corners as an SVG polygon's points.
    /// </summary>
    /// <param name="corners">The corners.</param>
    /// <param name="width">The width of the drawing.</param>
    public static string Points(
        IEnumerable<ZonePoint> corners,
        double width)
    {
        ArgumentNullException.ThrowIfNull(corners);

        return string.Join(' ', corners.Select(c => $"{SvgNumber.Format(c.X * width)},{SvgNumber.Format(c.Y * Height)}"));
    }

    /// <summary>
    /// Gets the handle of a corner, centred on it.
    /// </summary>
    /// <param name="corner">The corner.</param>
    /// <param name="width">The width of the drawing.</param>
    public static TimelineRect Handle(
        ZonePoint corner,
        double width)
    {
        ArgumentNullException.ThrowIfNull(corner);

        return new TimelineRect((corner.X * width) - (HandleSize / 2), (corner.Y * Height) - (HandleSize / 2), HandleSize, HandleSize);
    }

    /// <summary>
    /// Gets where a zone's name is written: inside its top left.
    /// </summary>
    /// <param name="corners">The corners.</param>
    /// <param name="width">The width of the drawing.</param>
    public static (double X, double Y) Label(
        IReadOnlyList<ZonePoint> corners,
        double width)
    {
        ArgumentNullException.ThrowIfNull(corners);

        return corners.Count == 0
            ? (0, 0)
            : ((corners.Min(c => c.X) * width) + 8, (corners.Min(c => c.Y) * Height) + 36);
    }

    /// <summary>
    /// How much of a zone's colour its fill takes, so the picture shows through.
    /// </summary>
    public const double FillOpacity = 0.35;

    /// <summary>
    /// Gets a zone's colour: red for an ignored zone; a watched one in its own colour, as on the
    /// timeline, else blue. Filled at <see cref="FillOpacity"/>.
    /// </summary>
    /// <param name="zone">The zone.</param>
    public static string Colour(MotionZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (zone.Kind.Value != MotionZoneKind.Names.Watch)
        {
            return "#E53935";
        }

        return string.IsNullOrWhiteSpace(zone.Color) ? "#1E88E5" : ColorChoices.ToCss(zone.Color);
    }

    /// <summary>
    /// Gets a zone's outline: white when picked, else its colour.
    /// </summary>
    /// <param name="zone">The zone.</param>
    /// <param name="picked">Whether the zone is picked.</param>
    public static string Stroke(
        MotionZone zone,
        bool picked)
        => picked ? "#FFFFFF" : Colour(zone);

    /// <summary>
    /// Reads the size of a PNG picture from its header.
    /// </summary>
    /// <param name="png">The picture.</param>
    /// <returns>The size, or <see langword="null"/> when it is not a PNG.</returns>
    public static (int Width, int Height)? PngSize(byte[]? png)
    {
        // The signature, the length and type of the first chunk (IHDR), then width and height big-endian.
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (png is null || png.Length < 24 || !png.AsSpan(0, 8).SequenceEqual(signature))
        {
            return null;
        }

        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        return width > 0 && height > 0 ? (width, height) : null;
    }
}