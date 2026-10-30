namespace AtcSoft.VideoSurveillance.Wpf.Core.Events;

/// <summary>
/// Event arguments for motion zones changed in the editor.
/// </summary>
public sealed class MotionZonesEditedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MotionZonesEditedEventArgs"/> class.
    /// </summary>
    /// <param name="zones">The zones as they are now: new objects, the old ones are left as they were.</param>
    public MotionZonesEditedEventArgs(IReadOnlyList<MotionZone> zones)
    {
        Zones = zones ?? throw new ArgumentNullException(nameof(zones));
    }

    /// <summary>
    /// Gets the zones as they are now.
    /// </summary>
    public IReadOnlyList<MotionZone> Zones { get; }
}