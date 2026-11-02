namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// The kinds of camera hub the apps can list cameras from.
/// </summary>
public static class CameraHubKinds
{
    /// <summary>
    /// motionEye or motionEyeOS (a web front end for <c>motion</c>, often on a Raspberry Pi).
    /// </summary>
    public const string MotionEye = "motionEye";
}