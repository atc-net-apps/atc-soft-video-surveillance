namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// Which of a camera's streams the live view plays: the sub-stream in a grid of several
/// cameras, the main stream when one camera fills the page (a 1×1 layout). The server plays the
/// main stream of a camera without a sub-stream whichever is asked for.
/// </summary>
public static class LiveStreamChoice
{
    public static CameraStream For(
        int rows,
        int columns)
        => rows <= 1 && columns <= 1 ? CameraStream.Main : CameraStream.Sub;
}