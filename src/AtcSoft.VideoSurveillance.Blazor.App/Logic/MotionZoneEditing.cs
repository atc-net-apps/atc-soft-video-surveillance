namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The editing of a motion zone's corners, shared by the zone editors: a new zone, moving a corner
/// or the whole zone, adding and removing corners, and finding what is under the pointer. Corners
/// are fractions of the picture as it is shown and are kept from 0 to 1. A copy of Core's
/// <c>MotionZoneEditing</c> on the server's <see cref="ZonePoint"/>, kept equal by a test.
/// </summary>
public static class MotionZoneEditing
{
    /// <summary>
    /// The most zones a camera has (the spec's limit).
    /// </summary>
    public const int MaxZones = 16;

    /// <summary>
    /// The fewest corners of a zone (the spec's limit).
    /// </summary>
    public const int MinCorners = 3;

    /// <summary>
    /// The most corners of a zone (the spec's limit).
    /// </summary>
    public const int MaxCorners = 32;

    /// <summary>
    /// The longest name of a zone (the spec's limit).
    /// </summary>
    public const int MaxNameLength = 64;

    /// <summary>
    /// The shape of the picture, width over height, while there is no picture to draw on.
    /// </summary>
    public const double DefaultAspectRatio = 16d / 9d;

    /// <summary>
    /// How near the pointer has to be to a corner or an edge to take it, in pixels of the picture as shown.
    /// </summary>
    public const double GrabDistance = 8;

    /// <summary>
    /// Reads a watch zone's sensitivity as typed: empty is none (the camera's), a whole number is
    /// kept from 0 to 100.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="culture">The culture it is typed in.</param>
    /// <param name="value">The sensitivity, or <see langword="null"/> for the camera's.</param>
    /// <returns><see langword="false"/> when the text is not a number (yet).</returns>
    public static bool TryReadSensitivity(
        string? text,
        IFormatProvider culture,
        out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer, culture, out var read))
        {
            return false;
        }

        value = Math.Clamp(read, 0, 100);
        return true;
    }

    /// <summary>
    /// Reads a watch zone's smallest object as typed, in percent of the picture: empty is none (the
    /// default), a number is kept from 0 to 100.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="culture">The culture it is typed in.</param>
    /// <param name="value">The percent, or <see langword="null"/> for the default.</param>
    /// <returns><see langword="false"/> when the text is not a number (yet).</returns>
    public static bool TryReadSmallestObjectPercent(
        string? text,
        IFormatProvider culture,
        out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, culture, out var read) ||
            double.IsNaN(read) ||
            double.IsInfinity(read))
        {
            return false;
        }

        value = Math.Clamp(read, 0, 100);
        return true;
    }

    /// <summary>
    /// Gets a value indicating whether another zone may be added.
    /// </summary>
    /// <param name="count">The number of zones.</param>
    public static bool CanAddZone(int count)
        => count < MaxZones;

    /// <summary>
    /// Gets a value indicating whether a zone may get another corner.
    /// </summary>
    /// <param name="count">The number of corners.</param>
    public static bool CanAddCorner(int count)
        => count < MaxCorners;

    /// <summary>
    /// Gets a value indicating whether a corner may be removed from a zone.
    /// </summary>
    /// <param name="count">The number of corners.</param>
    public static bool CanRemoveCorner(int count)
        => count > MinCorners;

    /// <summary>
    /// Gets the corners of a new zone: a square of almost a third of the picture in the middle, moved
    /// down and to the right for each zone already there, so it is not hidden under the last one.
    /// </summary>
    /// <param name="existing">The number of zones already there.</param>
    public static IReadOnlyList<ZonePoint> NewZoneCorners(int existing)
    {
        var start = 0.25 + ((Math.Max(existing, 0) % 5) * 0.05);
        var end = start + 0.3;
        return
        [
            new ZonePoint(start, start),
            new ZonePoint(end, start),
            new ZonePoint(end, end),
            new ZonePoint(start, end),
        ];
    }

    /// <summary>
    /// Gets the lowest number from 1 whose name is not taken yet, for the name of a new zone.
    /// </summary>
    /// <param name="names">The names of the zones already there.</param>
    /// <param name="nameFor">The name a number gives.</param>
    public static int NextNumber(
        IEnumerable<string> names,
        Func<int, string> nameFor)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(nameFor);

        var taken = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = 1;
        while (taken.Contains(nameFor(number)))
        {
            number++;
        }

        return number;
    }

    /// <summary>
    /// Keeps a corner on the picture.
    /// </summary>
    /// <param name="x">From the left edge.</param>
    /// <param name="y">From the top edge.</param>
    public static ZonePoint Clamp(
        double x,
        double y)
        => new(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));

    /// <summary>
    /// Moves one corner, kept on the picture.
    /// </summary>
    /// <param name="corners">The zone's corners.</param>
    /// <param name="index">The corner.</param>
    /// <param name="x">Where to, from the left edge.</param>
    /// <param name="y">Where to, from the top edge.</param>
    /// <returns>New corners.</returns>
    public static IReadOnlyList<ZonePoint> MoveCorner(
        IReadOnlyList<ZonePoint> corners,
        int index,
        double x,
        double y)
    {
        ArgumentNullException.ThrowIfNull(corners);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, corners.Count);

        var moved = corners.ToList();
        moved[index] = Clamp(x, y);
        return moved;
    }

    /// <summary>
    /// Moves the whole zone, no further than the edge of the picture, so it keeps its shape.
    /// </summary>
    /// <param name="corners">The zone's corners.</param>
    /// <param name="dx">How far to the right.</param>
    /// <param name="dy">How far down.</param>
    /// <returns>New corners.</returns>
    public static IReadOnlyList<ZonePoint> MoveZone(
        IReadOnlyList<ZonePoint> corners,
        double dx,
        double dy)
    {
        ArgumentNullException.ThrowIfNull(corners);

        if (corners.Count == 0)
        {
            return [];
        }

        dx = Math.Min(Math.Max(dx, -corners.Min(p => p.X)), 1 - corners.Max(p => p.X));
        dy = Math.Min(Math.Max(dy, -corners.Min(p => p.Y)), 1 - corners.Max(p => p.Y));
        return [.. corners.Select(p => new ZonePoint(p.X + dx, p.Y + dy))];
    }

    /// <summary>
    /// Adds a corner in the middle of an edge; edge <c>i</c> runs from corner <c>i</c> to the next.
    /// </summary>
    /// <param name="corners">The zone's corners.</param>
    /// <param name="edge">The edge.</param>
    /// <returns>New corners; the same corners when the zone has as many as it may have.</returns>
    public static IReadOnlyList<ZonePoint> AddCorner(
        IReadOnlyList<ZonePoint> corners,
        int edge)
    {
        ArgumentNullException.ThrowIfNull(corners);
        ArgumentOutOfRangeException.ThrowIfNegative(edge);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(edge, corners.Count);

        var added = corners.ToList();
        if (!CanAddCorner(corners.Count))
        {
            return added;
        }

        var from = corners[edge];
        var to = corners[(edge + 1) % corners.Count];
        added.Insert(edge + 1, new ZonePoint((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        return added;
    }

    /// <summary>
    /// Removes a corner.
    /// </summary>
    /// <param name="corners">The zone's corners.</param>
    /// <param name="index">The corner.</param>
    /// <returns>New corners; the same corners when the zone has as few as it may have.</returns>
    public static IReadOnlyList<ZonePoint> RemoveCorner(
        IReadOnlyList<ZonePoint> corners,
        int index)
    {
        ArgumentNullException.ThrowIfNull(corners);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, corners.Count);

        var removed = corners.ToList();
        if (CanRemoveCorner(corners.Count))
        {
            removed.RemoveAt(index);
        }

        return removed;
    }

    /// <summary>
    /// Finds the longest edge on the picture as shown, where a corner added without a pointer goes.
    /// </summary>
    /// <param name="corners">The zone's corners.</param>
    /// <param name="width">The width of the picture as shown.</param>
    /// <param name="height">The height of the picture as shown.</param>
    /// <returns>The edge, or -1 when the zone has no corners.</returns>
    public static int LongestEdge(
        IReadOnlyList<ZonePoint> corners,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(corners);

        var longest = -1;
        var length = -1d;
        for (var i = 0; i < corners.Count; i++)
        {
            var next = corners[(i + 1) % corners.Count];
            var dx = (next.X - corners[i].X) * width;
            var dy = (next.Y - corners[i].Y) * height;
            var edge = (dx * dx) + (dy * dy);
            if (edge > length)
            {
                length = edge;
                longest = i;
            }
        }

        return longest;
    }

    /// <summary>
    /// Finds the corner under the pointer: the nearest within <see cref="GrabDistance"/>.
    /// </summary>
    /// <param name="corners">The zone's corners.</param>
    /// <param name="x">The pointer, from the left edge, as a fraction.</param>
    /// <param name="y">The pointer, from the top edge, as a fraction.</param>
    /// <param name="width">The width of the picture as shown.</param>
    /// <param name="height">The height of the picture as shown.</param>
    /// <returns>The corner, or <see langword="null"/>.</returns>
    public static int? FindCorner(
        IReadOnlyList<ZonePoint> corners,
        double x,
        double y,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(corners);

        int? found = null;
        var nearest = GrabDistance * GrabDistance;
        for (var i = 0; i < corners.Count; i++)
        {
            var dx = (corners[i].X - x) * width;
            var dy = (corners[i].Y - y) * height;
            var distance = (dx * dx) + (dy * dy);
            if (distance <= nearest)
            {
                nearest = distance;
                found = i;
            }
        }

        return found;
    }

    /// <summary>
    /// Finds the edge under the pointer: the nearest within <see cref="GrabDistance"/>.
    /// </summary>
    /// <param name="corners">The zone's corners.</param>
    /// <param name="x">The pointer, from the left edge, as a fraction.</param>
    /// <param name="y">The pointer, from the top edge, as a fraction.</param>
    /// <param name="width">The width of the picture as shown.</param>
    /// <param name="height">The height of the picture as shown.</param>
    /// <returns>The edge, or <see langword="null"/>.</returns>
    public static int? FindEdge(
        IReadOnlyList<ZonePoint> corners,
        double x,
        double y,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(corners);

        int? found = null;
        var nearest = GrabDistance * GrabDistance;
        for (var i = 0; i < corners.Count; i++)
        {
            var next = corners[(i + 1) % corners.Count];
            var distance = SquaredDistanceToEdge(
                x * width,
                y * height,
                corners[i].X * width,
                corners[i].Y * height,
                next.X * width,
                next.Y * height);
            if (distance <= nearest)
            {
                nearest = distance;
                found = i;
            }
        }

        return found;
    }

    /// <summary>
    /// Gets a value indicating whether a point is inside a zone, by the even-odd rule the mask uses.
    /// </summary>
    /// <param name="corners">The zone's corners.</param>
    /// <param name="x">From the left edge, as a fraction.</param>
    /// <param name="y">From the top edge, as a fraction.</param>
    public static bool Contains(
        IReadOnlyList<ZonePoint> corners,
        double x,
        double y)
    {
        ArgumentNullException.ThrowIfNull(corners);

        var inside = false;
        for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
        {
            var a = corners[i];
            var b = corners[j];
            if ((a.Y > y) != (b.Y > y) &&
                x < (((b.X - a.X) * (y - a.Y)) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Finds the zone under the pointer: the last one drawn, which is on top.
    /// </summary>
    /// <param name="zones">The corners of each zone, in the order they are drawn.</param>
    /// <param name="x">From the left edge, as a fraction.</param>
    /// <param name="y">From the top edge, as a fraction.</param>
    /// <returns>The zone, or <see langword="null"/>.</returns>
    public static int? FindZone(
        IReadOnlyList<IReadOnlyList<ZonePoint>> zones,
        double x,
        double y)
    {
        ArgumentNullException.ThrowIfNull(zones);

        for (var i = zones.Count - 1; i >= 0; i--)
        {
            if (Contains(zones[i], x, y))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets where a picture is shown in an area: as large as it fits, keeping its shape, in the middle.
    /// </summary>
    /// <param name="areaWidth">The width of the area.</param>
    /// <param name="areaHeight">The height of the area.</param>
    /// <param name="aspectRatio">The picture's width over its height.</param>
    /// <returns>The left and top edges and the size of the picture in the area.</returns>
    public static (double X, double Y, double Width, double Height) Fit(
        double areaWidth,
        double areaHeight,
        double aspectRatio)
    {
        if (areaWidth <= 0 || areaHeight <= 0)
        {
            return (0, 0, 0, 0);
        }

        if (aspectRatio <= 0 || double.IsNaN(aspectRatio) || double.IsInfinity(aspectRatio))
        {
            aspectRatio = DefaultAspectRatio;
        }

        var width = Math.Min(areaWidth, areaHeight * aspectRatio);
        var height = width / aspectRatio;
        return ((areaWidth - width) / 2, (areaHeight - height) / 2, width, height);
    }

    private static double SquaredDistanceToEdge(
        double px,
        double py,
        double ax,
        double ay,
        double bx,
        double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var length = (dx * dx) + (dy * dy);
        var t = length == 0 ? 0 : Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / length, 0, 1);
        var nx = ax + (t * dx) - px;
        var ny = ay + (t * dy) - py;
        return (nx * nx) + (ny * ny);
    }
}