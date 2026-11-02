namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// A camera on a hub.
/// </summary>
/// <param name="Id">The camera's id on the hub.</param>
/// <param name="Name">Its name on the hub.</param>
/// <param name="Stream">What to play, without credentials; <see langword="null"/> when it cannot be played (see <paramref name="Problem"/>).</param>
/// <param name="StreamKind">What the stream is.</param>
/// <param name="StreamUserName">The stream's user, when the hub tells it.</param>
/// <param name="StreamPassword">The stream's password, when the hub tells it.</param>
/// <param name="Resolution">Its size, as the hub says it (<c>1280x720</c>), or <see langword="null"/>.</param>
/// <param name="FramesPerSecond">Its frame rate, or <see langword="null"/>.</param>
/// <param name="IsUsb">Whether it is a USB camera on the hub (not a network camera the hub reads).</param>
/// <param name="Problem">Why it cannot be added as it is.</param>
public sealed record HubCamera(
    string Id,
    string Name,
    Uri? Stream,
    HubStreamKind StreamKind,
    string? StreamUserName,
    string? StreamPassword,
    string? Resolution,
    int? FramesPerSecond,
    bool IsUsb,
    HubCameraProblem Problem);