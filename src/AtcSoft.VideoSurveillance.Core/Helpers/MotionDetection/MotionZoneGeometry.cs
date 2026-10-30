namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// Keeps a camera's motion zones on the same part of the scene when the picture is turned: the
/// zones are drawn on the picture as it is shown, so a new rotation turns them with it.
/// </summary>
public static class MotionZoneGeometry
{
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
        return
        [
            .. zones.Select(zone =>
            {
                var turned = zone.Clone();
                turned.Points = [.. zone.Points.Select(p => Turn(p, quarters))];
                return turned;
            }),
        ];
    }

    /// <summary>
    /// Gets the quarter turns clockwise, 0 to 3, from a picture shown at one rotation to the same
    /// picture shown at another.
    /// </summary>
    /// <param name="from">The rotation the picture has.</param>
    /// <param name="to">The rotation to show it at.</param>
    public static int QuarterTurns(
        CameraRotation from,
        CameraRotation to)
        => (((((int)to - (int)from) / 90) % 4) + 4) % 4;

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
}