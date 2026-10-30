namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The web zone editor: the zones, the picked zone and corner, and a drag on the way. A zone is
/// picked and dragged by its inside, a corner of the picked zone by its handle; a double click on
/// an edge adds a corner there, on a corner removes it. Every change makes new zones and leaves the
/// old ones as they were, so the form can hand them on as they are. Positions are fractions of
/// the picture as shown; sizes are its size on the page, for grabbing.
/// </summary>
public sealed class MotionZoneEditorState
{
    // A drag: the corners as it began, where it began, the corner dragged (-1: the whole zone),
    // and the corners now, kept until the pointer is let go.
    private IReadOnlyList<ZonePoint>? dragOrigin;
    private ZonePoint dragStart = new(0, 0);
    private int dragCorner = -1;
    private IReadOnlyList<ZonePoint>? dragged;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionZoneEditorState"/> class.
    /// </summary>
    /// <param name="zones">The zones to edit.</param>
    public MotionZoneEditorState(IReadOnlyList<MotionZone> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        Zones = zones;
    }

    /// <summary>
    /// Gets the zones as they are now.
    /// </summary>
    public IReadOnlyList<MotionZone> Zones { get; private set; }

    /// <summary>
    /// Gets the picked zone, or -1.
    /// </summary>
    public int SelectedZone { get; private set; } = -1;

    /// <summary>
    /// Gets the picked corner of the picked zone, or -1.
    /// </summary>
    public int SelectedCorner { get; private set; } = -1;

    /// <summary>
    /// Gets a value indicating whether a drag is on the way.
    /// </summary>
    public bool IsDragging
        => dragOrigin is not null;

    /// <summary>
    /// Gets the picked zone.
    /// </summary>
    public MotionZone? Picked
        => SelectedZone >= 0 && SelectedZone < Zones.Count
            ? Zones[SelectedZone]
            : null;

    /// <summary>
    /// Gets a value indicating whether another zone may be added.
    /// </summary>
    public bool CanAddZone
        => MotionZoneEditing.CanAddZone(Zones.Count);

    /// <summary>
    /// Gets a value indicating whether the picked zone may get another corner.
    /// </summary>
    public bool CanAddCorner
        => Picked is { } zone && MotionZoneEditing.CanAddCorner(zone.Points.Count);

    /// <summary>
    /// Gets a value indicating whether the picked corner may be removed.
    /// </summary>
    public bool CanRemoveCorner
        => Picked is { } zone &&
           SelectedCorner >= 0 &&
           SelectedCorner < zone.Points.Count &&
           MotionZoneEditing.CanRemoveCorner(zone.Points.Count);

    /// <summary>
    /// Gets the corners of a zone as they are drawn now: the dragged ones during a drag.
    /// </summary>
    /// <param name="zone">The zone.</param>
    public IReadOnlyList<ZonePoint> Corners(int zone)
        => zone == SelectedZone && dragged is not null
            ? dragged
            : Zones[zone].Points;

    /// <summary>
    /// Takes zones the form gives (turned with a new rotation, say), keeping what is picked when it is still there.
    /// </summary>
    /// <param name="zones">The zones.</param>
    public void Load(IReadOnlyList<MotionZone> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        Zones = zones;
        dragOrigin = null;
        dragged = null;
        if (Picked is not { } zone)
        {
            SelectedZone = -1;
            SelectedCorner = -1;
        }
        else if (SelectedCorner >= zone.Points.Count)
        {
            SelectedCorner = -1;
        }
    }

    /// <summary>
    /// The pointer is pressed: picks a corner of the picked zone or a zone, and starts dragging it.
    /// </summary>
    /// <param name="x">From the left edge, as a fraction.</param>
    /// <param name="y">From the top edge, as a fraction.</param>
    /// <param name="width">The width of the picture on the page.</param>
    /// <param name="height">The height of the picture on the page.</param>
    public void PointerDown(
        double x,
        double y,
        double width,
        double height)
    {
        // A press without the last one let go (the pointer left the page) starts afresh.
        dragOrigin = null;
        dragged = null;

        if (Picked is { } zone &&
            MotionZoneEditing.FindCorner(zone.Points, x, y, width, height) is { } corner)
        {
            SelectedCorner = corner;
            StartDrag(zone.Points, x, y, corner);
            return;
        }

        // On the edge, just outside the zone, it stays picked: the first click of a double click
        // there must not let it go.
        if (Picked is { } edged &&
            MotionZoneEditing.FindEdge(edged.Points, x, y, width, height) is not null)
        {
            SelectedCorner = -1;
            StartDrag(edged.Points, x, y, corner: -1);
            return;
        }

        var hit = MotionZoneEditing.FindZone([.. Zones.Select(z => (IReadOnlyList<ZonePoint>)z.Points)], x, y);
        SelectedZone = hit ?? -1;
        SelectedCorner = -1;
        if (hit is { } picked)
        {
            StartDrag(Zones[picked].Points, x, y, corner: -1);
        }
    }

    /// <summary>
    /// The pointer moves: the dragged corner or zone follows it, kept on the picture.
    /// </summary>
    /// <param name="x">From the left edge, as a fraction.</param>
    /// <param name="y">From the top edge, as a fraction.</param>
    public void PointerMove(
        double x,
        double y)
    {
        if (dragOrigin is null)
        {
            return;
        }

        dragged = dragCorner >= 0
            ? MotionZoneEditing.MoveCorner(dragOrigin, dragCorner, x, y)
            : MotionZoneEditing.MoveZone(dragOrigin, x - dragStart.X, y - dragStart.Y);
    }

    /// <summary>
    /// The pointer is let go: the drag ends where it is.
    /// </summary>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool PointerUp()
    {
        var corners = dragged;
        dragOrigin = null;
        dragged = null;
        return corners is not null && Change(SelectedZone, z => z with { Points = [.. corners] });
    }

    /// <summary>
    /// A double click: removes the corner of the picked zone under the pointer, or adds one on the edge under it.
    /// </summary>
    /// <param name="x">From the left edge, as a fraction.</param>
    /// <param name="y">From the top edge, as a fraction.</param>
    /// <param name="width">The width of the picture on the page.</param>
    /// <param name="height">The height of the picture on the page.</param>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool DoubleClick(
        double x,
        double y,
        double width,
        double height)
    {
        dragOrigin = null;
        dragged = null;
        if (Picked is not { } zone)
        {
            return false;
        }

        if (MotionZoneEditing.FindCorner(zone.Points, x, y, width, height) is { } corner)
        {
            SelectedCorner = -1;
            return MotionZoneEditing.CanRemoveCorner(zone.Points.Count) &&
                   Change(SelectedZone, z => z with { Points = [.. MotionZoneEditing.RemoveCorner(z.Points, corner)] });
        }

        if (MotionZoneEditing.CanAddCorner(zone.Points.Count) &&
            MotionZoneEditing.FindEdge(zone.Points, x, y, width, height) is { } edge)
        {
            SelectedCorner = edge + 1;
            return Change(
                SelectedZone,
                z => z with { Points = [.. MotionZoneEditing.MoveCorner(MotionZoneEditing.AddCorner(z.Points, edge), edge + 1, x, y)] });
        }

        return false;
    }

    /// <summary>
    /// Adds a zone in the middle, named with the lowest free number, and picks it.
    /// </summary>
    /// <param name="nameFor">The name a number gives.</param>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool AddZone(Func<int, string> nameFor)
    {
        ArgumentNullException.ThrowIfNull(nameFor);

        if (!CanAddZone)
        {
            return false;
        }

        var number = MotionZoneEditing.NextNumber(Zones.Select(z => z.Name), nameFor);
        Zones =
        [
            .. Zones,
            new MotionZone(
                nameFor(number),
                new MotionZoneKind(MotionZoneKind.Names.Ignore),
                [.. MotionZoneEditing.NewZoneCorners(Zones.Count)],
                Color: null),
        ];
        SelectedZone = Zones.Count - 1;
        SelectedCorner = -1;
        return true;
    }

    /// <summary>
    /// Removes the picked zone.
    /// </summary>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool RemoveZone()
    {
        if (Picked is null)
        {
            return false;
        }

        Zones = [.. Zones.Where((_, i) => i != SelectedZone)];
        SelectedZone = -1;
        SelectedCorner = -1;
        return true;
    }

    /// <summary>
    /// Adds a corner to the picked zone in the middle of its longest edge, where it is easiest to grab, and picks it.
    /// </summary>
    /// <param name="width">The width of the picture on the page.</param>
    /// <param name="height">The height of the picture on the page.</param>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool AddCorner(
        double width,
        double height)
    {
        if (!CanAddCorner)
        {
            return false;
        }

        var edge = MotionZoneEditing.LongestEdge(Picked!.Points, width, height);
        SelectedCorner = edge + 1;
        return Change(SelectedZone, z => z with { Points = [.. MotionZoneEditing.AddCorner(z.Points, edge)] });
    }

    /// <summary>
    /// Removes the picked corner.
    /// </summary>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool RemoveCorner()
    {
        if (!CanRemoveCorner)
        {
            return false;
        }

        var corner = SelectedCorner;
        SelectedCorner = -1;
        return Change(SelectedZone, z => z with { Points = [.. MotionZoneEditing.RemoveCorner(z.Points, corner)] });
    }

    /// <summary>
    /// Names the picked zone, cut to the longest name the server takes.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool Rename(string? name)
    {
        name ??= string.Empty;
        if (name.Length > MotionZoneEditing.MaxNameLength)
        {
            name = name[..MotionZoneEditing.MaxNameLength];
        }

        return Picked is { } zone &&
               !string.Equals(zone.Name, name, StringComparison.Ordinal) &&
               Change(SelectedZone, z => z with { Name = name });
    }

    /// <summary>
    /// Makes the picked zone an ignore zone (without values of its own) or a watch zone.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool SetKind(MotionZoneKind kind)
    {
        if (Picked is not { } zone || zone.Kind.Equals(kind))
        {
            return false;
        }

        return kind.Value == MotionZoneKind.Names.Watch
            ? Change(SelectedZone, z => z with { Kind = kind })
            : Change(SelectedZone, z => z with { Kind = kind, Sensitivity = null, SmallestObjectPercent = null, Color = null });
    }

    /// <summary>
    /// Gives the picked zone a colour by name, for its events on the timeline, or none.
    /// </summary>
    /// <param name="colour">The colour's name, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool SetColour(string? colour)
        => Picked is { } zone &&
           !string.Equals(zone.Color, colour, StringComparison.Ordinal) &&
           Change(SelectedZone, z => z with { Color = colour });

    /// <summary>
    /// Gives the picked zone its own sensitivity, or none (the camera's).
    /// </summary>
    /// <param name="sensitivity">The sensitivity, 0 to 100, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool SetSensitivity(int? sensitivity)
        => Picked is { } zone &&
           zone.Sensitivity != sensitivity &&
           Change(SelectedZone, z => z with { Sensitivity = sensitivity });

    /// <summary>
    /// Gives the picked zone its own smallest object, or none (the default).
    /// </summary>
    /// <param name="percent">The percent of the picture, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the zones changed.</returns>
    public bool SetSmallestObjectPercent(double? percent)
        => Picked is { } zone &&
           !EqualityComparer<double?>.Default.Equals(zone.SmallestObjectPercent, percent) &&
           Change(SelectedZone, z => z with { SmallestObjectPercent = percent });

    private void StartDrag(
        IReadOnlyList<ZonePoint> corners,
        double x,
        double y,
        int corner)
    {
        dragOrigin = [.. corners];
        dragStart = new ZonePoint(x, y);
        dragCorner = corner;
        dragged = null;
    }

    private bool Change(
        int index,
        Func<MotionZone, MotionZone> change)
    {
        if (index < 0 || index >= Zones.Count)
        {
            return false;
        }

        Zones = [.. Zones.Select((zone, i) => i == index ? change(zone) : zone)];
        return true;
    }
}