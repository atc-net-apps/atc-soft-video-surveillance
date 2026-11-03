namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// A camera of a hub, as the camera form lists it: its name, its size, a picture once it came,
/// and why it cannot be used, if so.
/// </summary>
public sealed partial class HubCameraItem : ObservableObject
{
    [ObservableProperty]
    private ImageSource? picture;

    public HubCameraItem(
        AtcSoft.Video.Surveillance.Services.Hubs.HubCamera camera,
        string? problem)
    {
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        Problem = problem;
    }

    /// <summary>
    /// Gets the camera on the hub.
    /// </summary>
    public AtcSoft.Video.Surveillance.Services.Hubs.HubCamera Camera { get; }

    public string Name
        => Camera.Name;

    /// <summary>
    /// Gets its size and frame rate, as the hub says them (<c>1280x720, 5 fps</c>).
    /// </summary>
    public string Details
        => string.Join(
            ", ",
            new[]
            {
                Camera.Resolution,
                Camera.FramesPerSecond is { } fps ? fps.ToString(CultureInfo.CurrentCulture) + " fps" : null,
                Camera.IsUsb ? "USB" : null,
            }.Where(s => !string.IsNullOrEmpty(s)));

    /// <summary>
    /// Gets why it cannot be used, or <see langword="null"/>.
    /// </summary>
    public string? Problem { get; }

    public bool CanUse
        => Problem is null && Camera.Stream is not null;
}