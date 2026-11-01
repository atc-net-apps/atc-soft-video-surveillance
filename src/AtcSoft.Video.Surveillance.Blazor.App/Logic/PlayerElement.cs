namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The id of the video element that plays a camera's live stream.
/// </summary>
public static class PlayerElement
{
    private const string Prefix = "video-";

    public static string IdFor(Guid cameraId)
        => Prefix + cameraId.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the camera a video element plays, or <see langword="null"/> for
    /// an id that is not a camera's.
    /// </summary>
    public static Guid? CameraOf(string? elementId)
        => elementId is not null &&
           elementId.StartsWith(Prefix, StringComparison.Ordinal) &&
           Guid.TryParse(elementId.AsSpan(Prefix.Length), CultureInfo.InvariantCulture, out var cameraId)
            ? cameraId
            : null;
}