namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Tells whether an edit of a camera changes how it is opened or recorded, so what runs for it
/// must be opened again: an open pipeline keeps the address, sign-in and stream options it was
/// opened with, and a recording the codec it started with.
/// </summary>
public static class CameraPipelineChange
{
    /// <summary>
    /// Gets whether the camera's pipeline and recording must be opened again.
    /// </summary>
    /// <param name="before">The camera as it was.</param>
    /// <param name="after">The camera as it is now.</param>
    /// <returns><c>true</c> when the address, sign-in, stream options or recording codec changed.</returns>
    public static bool NeedsReopen(
        AtcSoft.Video.Surveillance.Models.CameraConfiguration before,
        AtcSoft.Video.Surveillance.Models.CameraConfiguration after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        // The connection type is only shown; it never changes the stream.
        var connection = before.Connection.Clone();
        connection.ConnectionType = after.Connection.ConnectionType;

        return !connection.ValueEquals(after.Connection) ||
               !before.Authentication.ValueEquals(after.Authentication) ||
               !before.Stream.ValueEquals(after.Stream) ||
               !before.Overrides.Connection.ValueEquals(after.Overrides.Connection) ||
               !before.Overrides.Performance.ValueEquals(after.Overrides.Performance) ||
               before.Overrides.Recording.TranscodeVideoCodec != after.Overrides.Recording.TranscodeVideoCodec;
    }
}