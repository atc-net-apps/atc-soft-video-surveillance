namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// What the camera list says about a camera's sub-stream: nothing for a camera without one.
/// </summary>
public static class SubStreamStatusText
{
    public static string For(CameraSubStreamStatus status)
        => status.Value switch
        {
            CameraSubStreamStatus.Names.InUse => Translations.SubStreamInUse,
            CameraSubStreamStatus.Names.Unavailable => Translations.SubStreamUnavailable,
            _ => string.Empty,
        };
}