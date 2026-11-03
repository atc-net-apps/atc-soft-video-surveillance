namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// A camera of a hub in the hub dialog: its name, size, picture, and why it cannot be added.
/// </summary>
public sealed class HubCameraRowViewModel
{
    public HubCameraRowViewModel(
        HubCameraInfo camera,
        string? problem)
    {
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        Problem = problem;
        Picture = Decode(camera.Picture);
    }

    public HubCameraInfo Camera { get; }

    public string Name
        => Camera.Name;

    /// <summary>
    /// Gets its size and frame rate, as the hub says them (<c>1280x720, 5 fps, USB</c>).
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
    /// Gets why it cannot be added, or <see langword="null"/>.
    /// </summary>
    public string? Problem { get; }

    public bool CanAdd
        => Problem is null;

    public BitmapSource? Picture { get; }

    private static BitmapSource? Decode(byte[]? picture)
    {
        if (picture is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(picture);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException)
        {
            return null;
        }
    }
}