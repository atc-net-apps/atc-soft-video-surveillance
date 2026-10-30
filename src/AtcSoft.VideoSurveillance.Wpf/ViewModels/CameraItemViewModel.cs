namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// Wraps a generated Camera model for DataGrid binding with in-place updates from the server's events.
/// </summary>
public partial class CameraItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid id;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string ipAddress = string.Empty;

    [ObservableProperty]
    private int port;

    [ObservableProperty]
    private CameraProtocol protocol;

    [ObservableProperty]
    private string connectionState = "disconnected";

    [ObservableProperty]
    private bool isRecording;

    /// <summary>
    /// Creates a <see cref="CameraItemViewModel"/> from a generated <see cref="Camera"/> model.
    /// </summary>
    /// <summary>
    /// Gets a value indicating whether the camera matches a filter text.
    /// Every word of the filter has to occur in the name, the description,
    /// the address or the connection state; no filter matches everything.
    /// </summary>
    /// <param name="filter">The text typed in the filter box.</param>
    /// <returns><c>true</c> when the camera is to be shown.</returns>
    public bool Matches(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        string[] fields = [DisplayName, Description, IpAddress, ConnectionState];

        return filter
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(word => fields.Any(field =>
                field is not null &&
                field.Contains(word, StringComparison.CurrentCultureIgnoreCase)));
    }

    public static CameraItemViewModel FromCamera(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        return new CameraItemViewModel
        {
            Id = camera.Id,
            DisplayName = camera.DisplayName,
            Description = camera.Description ?? string.Empty,
            IpAddress = camera.IpAddress ?? string.Empty,
            Port = camera.Port,
            Protocol = camera.Protocol,
            ConnectionState = HubStates.ToText(camera.ConnectionState),
            IsRecording = camera.IsRecording,
        };
    }
}