namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Camera connection settings. The <see cref="Source"/> discriminator
/// selects between a network-camera shape (<see cref="IpAddress"/>,
/// <see cref="Port"/>, <see cref="Protocol"/>, <see cref="Path"/>) and
/// a USB-camera shape (<see cref="Usb"/>). The two shapes are mutually
/// exclusive — fields belonging to the unused branch are ignored.
/// </summary>
public class ConnectionSettings
{
    /// <summary>
    /// Selects which configuration shape applies. Defaults to
    /// <see cref="CameraSource.Network"/> so cameras stored before USB
    /// support continue to deserialize correctly.
    /// </summary>
    public CameraSource Source { get; set; } = CameraSource.Network;

    [Required(ErrorMessage = "IP Address is required.")]
    public string IpAddress { get; set; } = string.Empty;

    public CameraProtocol Protocol { get; set; } = CameraProtocol.Rtsp;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public int Port { get; set; } = 554;

    public string? Path { get; set; }

    /// <summary>
    /// USB-specific settings. Populated when <see cref="Source"/> equals
    /// <see cref="CameraSource.Usb"/>; <see langword="null"/> otherwise.
    /// </summary>
    public UsbConnectionSettings? Usb { get; set; }

    /// <summary>
    /// The physical link to the camera, for display. Defaults to
    /// <see cref="CameraConnectionType.None"/> so cameras stored before this
    /// property existed deserialize correctly. Read the displayed value through
    /// <see cref="Helpers.CameraConnectionTypeHelper.GetEffective(ConnectionSettings)"/>.
    /// </summary>
    public CameraConnectionType ConnectionType { get; set; } = CameraConnectionType.None;

    /// <inheritdoc />
    public override string ToString()
        => Source == CameraSource.Usb
            ? $"ConnectionSettings {{ Source=Usb, {Usb?.ToString() ?? "(no usb)"} }}"
            : $"ConnectionSettings {{ Source=Network, Protocol={Protocol}, IpAddress='{IpAddress}', Port={Port.ToString(CultureInfo.InvariantCulture)}, ConnectionType={ConnectionType} }}";

    public ConnectionSettings Clone()
        => new()
        {
            Source = Source,
            IpAddress = IpAddress,
            Protocol = Protocol,
            Port = Port,
            Path = Path,
            Usb = Usb?.Clone(),
            ConnectionType = ConnectionType,
        };

    public void CopyFrom(ConnectionSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source.Source;
        IpAddress = source.IpAddress;
        Protocol = source.Protocol;
        Port = source.Port;
        Path = source.Path;
        Usb = source.Usb?.Clone();
        ConnectionType = source.ConnectionType;
    }

    public bool ValueEquals(ConnectionSettings? other)
    {
        if (other is null)
        {
            return false;
        }

        if (Source != other.Source)
        {
            return false;
        }

        var usbEqual = (Usb is null && other.Usb is null) ||
                       (Usb?.ValueEquals(other.Usb) == true);

        return string.Equals(IpAddress, other.IpAddress, StringComparison.Ordinal) &&
               Protocol == other.Protocol &&
               Port == other.Port &&
               string.Equals(Path, other.Path, StringComparison.Ordinal) &&
               ConnectionType == other.ConnectionType &&
               usbEqual;
    }
}