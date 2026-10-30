namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// A camera's motion zones while its form is open. The zones are drawn on the picture as it is
/// shown, so a rotation chosen in the form turns them with the picture. Each turn starts from the
/// zones as they were last drawn, never from the last turn, so turning back and forth gives the
/// same corners again.
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

        opened = [.. zones.Select(z => z.Clone())];
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
    /// the rotation chosen now. A form that saves through the server leaves unchanged zones out, so
    /// the server turns the stored ones itself.
    /// </summary>
    public bool IsChanged
    {
        get
        {
            var expected = MotionZoneGeometry.Turn(opened, openedRotation, Rotation);
            return expected.Count != Zones.Count ||
                   expected.Where((zone, i) => !zone.ValueEquals(Zones[i])).Any();
        }
    }

    /// <summary>
    /// Turns the zones with the picture to the rotation chosen in the form.
    /// </summary>
    /// <param name="rotation">The rotation.</param>
    public void Turn(CameraRotation rotation)
    {
        Rotation = rotation;
        Zones = MotionZoneGeometry.Turn(drawn, drawnRotation, rotation);
    }

    /// <summary>
    /// Takes the zones as the editor has drawn them, at <see cref="Rotation"/>.
    /// </summary>
    /// <param name="zones">The zones.</param>
    public void Change(IEnumerable<MotionZone> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        drawn = [.. zones.Select(z => z.Clone())];
        drawnRotation = Rotation;
        Zones = Copy(drawn);
    }

    private static IReadOnlyList<MotionZone> Copy(IEnumerable<MotionZone> zones)
        => [.. zones.Select(z => z.Clone())];
}