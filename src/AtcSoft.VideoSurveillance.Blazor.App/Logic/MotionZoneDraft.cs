namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A camera's motion zones while its form is open. The zones are drawn on the picture as it is
/// shown, so a rotation chosen in the form turns them with the picture. Each turn starts from the
/// zones as they were last drawn, never from the last turn, so turning back and forth gives the
/// same corners again. A copy of Core's <c>MotionZoneDraft</c> and <c>MotionZoneGeometry</c> on
/// the server's <see cref="MotionZone"/>, kept equal by a test.
/// </summary>
public sealed class MotionZoneDraft
{
    private readonly IReadOnlyList<MotionZone> opened;
    private readonly CameraRotation openedRotation;
    private IReadOnlyList<MotionZone> drawn;
    private CameraRotation drawnRotation;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionZoneDraft"/> class.
    /// </summary>
    /// <param name="zones">The camera's zones, as stored.</param>
    /// <param name="rotation">The camera's rotation, as stored.</param>
    public MotionZoneDraft(
        IEnumerable<MotionZone> zones,
        CameraRotation rotation)
    {
        ArgumentNullException.ThrowIfNull(zones);

        opened = Copy(zones);
        openedRotation = rotation;
        drawn = opened;
        drawnRotation = rotation;
        Rotation = rotation;
        Zones = Copy(opened);
    }

    /// <summary>
    /// Gets the rotation chosen in the form.
    /// </summary>
    public CameraRotation Rotation { get; private set; }

    /// <summary>
    /// Gets the zones as they are shown at <see cref="Rotation"/>; the editor's to show, never to change.
    /// </summary>
    public IReadOnlyList<MotionZone> Zones { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the zones differ from the ones the form opened with, turned to
    /// the rotation chosen now. The form leaves unchanged zones out of the update, so the server
    /// turns the stored ones itself.
    /// </summary>
    public bool IsChanged
    {
        get
        {
            var expected = Turn(opened, openedRotation, Rotation);
            return expected.Count != Zones.Count ||
                   expected.Where((zone, i) => !ValueEquals(zone, Zones[i])).Any();
        }
    }

    /// <summary>
    /// Gets the quarter turns clockwise, 0 to 3, from a picture shown at one rotation to the same
    /// picture shown at another. A rotation the client does not know counts as none.
    /// </summary>
    /// <param name="from">The rotation the picture has.</param>
    /// <param name="to">The rotation to show it at.</param>
    public static int QuarterTurns(
        CameraRotation from,
        CameraRotation to)
        => (((Quarters(to) - Quarters(from)) % 4) + 4) % 4;

    /// <summary>
    /// Turns the zones from one rotation of the picture to another.
    /// </summary>
    /// <param name="zones">The zones, drawn at <paramref name="from"/>.</param>
    /// <param name="from">The rotation they were drawn at.</param>
    /// <param name="to">The rotation the picture is shown at now.</param>
    /// <returns>New zones; the same corners when the rotation did not change.</returns>
    public static IReadOnlyList<MotionZone> Turn(
        IEnumerable<MotionZone> zones,
        CameraRotation from,
        CameraRotation to)
    {
        ArgumentNullException.ThrowIfNull(zones);

        var quarters = QuarterTurns(from, to);
        return [.. zones.Select(zone => zone with { Points = [.. zone.Points.Select(p => Turn(p, quarters))] })];
    }

    /// <summary>
    /// Determines whether two zones have the same values.
    /// </summary>
    public static bool ValueEquals(
        MotionZone first,
        MotionZone second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return string.Equals(first.Name, second.Name, StringComparison.Ordinal) &&
               first.Kind.Equals(second.Kind) &&
               first.Points.SequenceEqual(second.Points) &&
               string.Equals(first.Color, second.Color, StringComparison.Ordinal) &&
               first.Sensitivity == second.Sensitivity &&
               EqualityComparer<double?>.Default.Equals(first.SmallestObjectPercent, second.SmallestObjectPercent);
    }

    /// <summary>
    /// Gets the zones a camera form sends: a new camera its zones (none when there are none), an
    /// existing one only zones that changed, so the server keeps and turns the stored ones.
    /// </summary>
    /// <param name="draft">The form's zones.</param>
    /// <param name="isNew">Whether the camera is new.</param>
    [SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "The generated requests take a List")]
    public static List<MotionZone>? ToSend(
        MotionZoneDraft draft,
        bool isNew)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return (isNew ? draft.Zones.Count > 0 : draft.IsChanged)
            ? [.. Copy(draft.Zones)]
            : null;
    }

    /// <summary>
    /// Turns the zones with the picture to the rotation chosen in the form.
    /// </summary>
    /// <param name="rotation">The rotation.</param>
    public void Turn(CameraRotation rotation)
    {
        Rotation = rotation;
        Zones = Turn(drawn, drawnRotation, rotation);
    }

    /// <summary>
    /// Takes the zones as the editor has drawn them, at <see cref="Rotation"/>.
    /// </summary>
    /// <param name="zones">The zones.</param>
    public void Change(IEnumerable<MotionZone> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        drawn = Copy(zones);
        drawnRotation = Rotation;
        Zones = Copy(drawn);
    }

    private static int Quarters(CameraRotation rotation)
        => rotation.Value switch
        {
            CameraRotation.Names.Rotate90 => 1,
            CameraRotation.Names.Rotate180 => 2,
            CameraRotation.Names.Rotate270 => 3,
            _ => 0,
        };

    private static ZonePoint Turn(
        ZonePoint point,
        int quarters)
        => quarters switch
        {
            1 => new ZonePoint(1 - point.Y, point.X),
            2 => new ZonePoint(1 - point.X, 1 - point.Y),
            3 => new ZonePoint(point.Y, 1 - point.X),
            _ => point,
        };

    private static IReadOnlyList<MotionZone> Copy(IEnumerable<MotionZone> zones)
        => [.. zones.Select(zone => zone with { Points = [.. zone.Points] })];
}