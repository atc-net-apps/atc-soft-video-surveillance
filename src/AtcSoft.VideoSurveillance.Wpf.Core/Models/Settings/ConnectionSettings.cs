using CoreSettings = AtcSoft.VideoSurveillance.Models.Settings;

namespace AtcSoft.VideoSurveillance.Wpf.Core.Models.Settings;

/// <summary>
/// Wraps <see cref="CoreSettings.ConnectionSettings"/> with change notification for WPF binding.
/// </summary>
public partial class ConnectionSettings : ObservableObject
{
    internal CoreSettings.ConnectionSettings Core { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionSettings"/> class.
    /// </summary>
    public ConnectionSettings()
        : this(new CoreSettings.ConnectionSettings())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionSettings"/> class
    /// wrapping the specified Core instance.
    /// </summary>
    internal ConnectionSettings(CoreSettings.ConnectionSettings core)
    {
        Core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Gets or sets the IP address of the camera.
    /// </summary>
    [Required(ErrorMessageResourceType = typeof(Translations), ErrorMessageResourceName = nameof(Translations.IpAddressRequired))]
    public string IpAddress
    {
        get => Core.IpAddress;
        set
        {
            if (string.Equals(Core.IpAddress, value, StringComparison.Ordinal))
            {
                return;
            }

            Core.IpAddress = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the protocol used to connect.
    /// </summary>
    public CameraProtocol Protocol
    {
        get => Core.Protocol;
        set
        {
            if (Core.Protocol == value)
            {
                return;
            }

            Core.Protocol = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the port number.
    /// </summary>
    [Range(1, 65535, ErrorMessageResourceType = typeof(Translations), ErrorMessageResourceName = nameof(Translations.PortRangeError))]
    public int Port
    {
        get => Core.Port;
        set
        {
            if (Core.Port == value)
            {
                return;
            }

            Core.Port = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the stream path.
    /// </summary>
    public string? Path
    {
        get => Core.Path;
        set
        {
            if (string.Equals(Core.Path, value, StringComparison.Ordinal))
            {
                return;
            }

            Core.Path = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the source kind (Network vs USB). USB cameras
    /// surface their device identity through <see cref="Usb"/>.
    /// </summary>
    public CameraSource Source
    {
        get => Core.Source;
        set
        {
            if (Core.Source == value)
            {
                return;
            }

            Core.Source = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveConnectionType));
        }
    }

    /// <summary>
    /// Gets or sets the USB-specific settings. <see langword="null"/>
    /// for network cameras. Mutations are wired straight to Core —
    /// the WPF wrapper doesn't add change notification on the nested
    /// <see cref="UsbConnectionSettings"/> object since the dialog
    /// rebuilds the whole part on Source switch anyway.
    /// </summary>
    public CoreSettings.UsbConnectionSettings? Usb
    {
        get => Core.Usb;
        set
        {
            Core.Usb = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the physical link to the camera, for display. Read the
    /// displayed value through <c>CameraConnectionTypeHelper.GetEffective</c>.
    /// </summary>
    public CameraConnectionType ConnectionType
    {
        get => Core.ConnectionType;
        set
        {
            if (Core.ConnectionType == value)
            {
                return;
            }

            Core.ConnectionType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveConnectionType));
        }
    }

    /// <summary>
    /// Gets the connection type to display (USB cameras are always USB).
    /// </summary>
    public CameraConnectionType EffectiveConnectionType
        => CameraConnectionTypeHelper.GetEffective(Core);

    /// <summary>
    /// Creates a deep copy of this instance.
    /// </summary>
    public ConnectionSettings Clone()
        => new(Core.Clone());

    /// <summary>
    /// Copies values from another instance.
    /// </summary>
    public void CopyFrom(ConnectionSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Source = source.Source;
        IpAddress = source.IpAddress;
        Protocol = source.Protocol;
        Port = source.Port;
        Path = source.Path;
        Usb = source.Usb is null ? null : source.Usb.Clone();
        ConnectionType = source.ConnectionType;
    }

    /// <summary>
    /// Determines whether the specified instance has the same values.
    /// </summary>
    public bool ValueEquals(ConnectionSettings? other)
        => other is not null && Core.ValueEquals(other.Core);
}