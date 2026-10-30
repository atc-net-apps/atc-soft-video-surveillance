using CoreModels = AtcSoft.VideoSurveillance.Models;

namespace AtcSoft.VideoSurveillance.Wpf.Core.Models;

/// <summary>
/// Wraps <see cref="CoreModels.CameraConfiguration"/> with change notification for WPF binding.
/// </summary>
public partial class CameraConfiguration : ObservableObject
{
    internal CoreModels.CameraConfiguration Core { get; }

    private ConnectionSettings connection;
    private AuthenticationSettings authentication;
    private CameraDisplaySettings display;
    private StreamSettings stream;

    [JsonIgnore]
    [ObservableProperty]
    private bool canSwapLeft;

    [JsonIgnore]
    [ObservableProperty]
    private bool canSwapRight;

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraConfiguration"/> class.
    /// </summary>
    public CameraConfiguration()
        : this(new CoreModels.CameraConfiguration())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraConfiguration"/> class
    /// wrapping the specified Core instance.
    /// </summary>
    internal CameraConfiguration(CoreModels.CameraConfiguration core)
    {
        Core = core ?? throw new ArgumentNullException(nameof(core));

        connection = new ConnectionSettings(Core.Connection);
        authentication = new AuthenticationSettings(Core.Authentication);
        display = new CameraDisplaySettings(Core.Display);
        stream = new StreamSettings(Core.Stream);

        SubscribeToNestedChanges(connection);
        SubscribeToNestedChanges(authentication);
        SubscribeToNestedChanges(display);
        SubscribeToNestedChanges(stream);
    }

    /// <summary>
    /// Gets or sets the unique identifier for the camera.
    /// </summary>
    public Guid Id
    {
        get => Core.Id;
        set => Core.Id = value;
    }

    /// <summary>
    /// Gets or sets the connection settings.
    /// </summary>
    public ConnectionSettings Connection
    {
        get => connection;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(connection, value))
            {
                return;
            }

            connection = value;
            Core.Connection = value.Core;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveConnectionType));
            SubscribeToNestedChanges(connection);
        }
    }

    /// <summary>
    /// Gets the connection type to display (USB cameras are always USB).
    /// Changes to the connection type alone are raised under this name, not
    /// under <see cref="Connection"/>, so they don't restart the stream.
    /// </summary>
    public CameraConnectionType EffectiveConnectionType
        => Connection.EffectiveConnectionType;

    /// <summary>
    /// Gets or sets the authentication settings.
    /// </summary>
    public AuthenticationSettings Authentication
    {
        get => authentication;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(authentication, value))
            {
                return;
            }

            authentication = value;
            Core.Authentication = value.Core;
            OnPropertyChanged();
            SubscribeToNestedChanges(authentication);
        }
    }

    /// <summary>
    /// Gets or sets the display settings.
    /// </summary>
    public CameraDisplaySettings Display
    {
        get => display;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(display, value))
            {
                return;
            }

            display = value;
            Core.Display = value.Core;
            OnPropertyChanged();
            SubscribeToNestedChanges(display);
        }
    }

    /// <summary>
    /// Gets or sets the streaming settings.
    /// </summary>
    public StreamSettings Stream
    {
        get => stream;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(stream, value))
            {
                return;
            }

            stream = value;
            Core.Stream = value.Core;
            OnPropertyChanged();
            SubscribeToNestedChanges(stream);
        }
    }

    /// <summary>
    /// Gets or sets the per-camera overrides.
    /// </summary>
    public CameraOverrides Overrides
    {
        get => Core.Overrides;
        set
        {
            if (ReferenceEquals(Core.Overrides, value))
            {
                return;
            }

            Core.Overrides = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the camera's motion zones, drawn on the picture as it is shown. Replace the
    /// list to change them, so the change is announced.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Replaced as a whole, which raises PropertyChanged")]
    [SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "The Core model's list")]
    public List<MotionZone> MotionZones
    {
        get => Core.MotionZones;
        set
        {
            if (ReferenceEquals(Core.MotionZones, value))
            {
                return;
            }

            Core.MotionZones = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Builds the camera stream URI based on the configuration.
    /// Throws on USB cameras — use
    /// <see cref="BuildSourceLocator"/> instead.
    /// </summary>
    /// <returns>The constructed URI for the camera stream.</returns>
    public Uri BuildUri()
        => Core.BuildUri();

    /// <summary>
    /// Builds an FFmpeg-ready locator that handles both network and
    /// USB cameras. Prefer this over <see cref="BuildUri"/> in new
    /// code.
    /// </summary>
    public AtcSoft.VideoSurveillance.Helpers.SourceLocator BuildSourceLocator()
        => AtcSoft.VideoSurveillance.Helpers.CameraUriHelper.BuildSourceLocator(Core);

    /// <summary>
    /// Returns the display name of the camera.
    /// </summary>
    /// <returns>The display name.</returns>
    public override string ToString()
        => Display.DisplayName;

    /// <summary>
    /// Creates a deep copy of this camera configuration.
    /// </summary>
    /// <returns>A new instance with the same values.</returns>
    public CameraConfiguration Clone()
    {
        var clone = new CameraConfiguration(Core.Clone())
        {
            CanSwapLeft = CanSwapLeft,
            CanSwapRight = CanSwapRight,
        };
        return clone;
    }

    /// <summary>
    /// Copies values from another camera configuration.
    /// </summary>
    /// <param name="source">The source configuration to copy from.</param>
    public void CopyFrom(CameraConfiguration source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Note: Id is intentionally not copied - we're copying values, not identity
        Connection.CopyFrom(source.Connection);
        Authentication.CopyFrom(source.Authentication);
        Display.CopyFrom(source.Display);
        Stream.CopyFrom(source.Stream);

        // Replace Overrides object to trigger PropertyChanged notification
        // (CameraOverrides doesn't implement INotifyPropertyChanged)
        Overrides = source.Overrides.Clone();
        MotionZones = [.. source.MotionZones.Select(z => z.Clone())];
    }

    /// <summary>
    /// Determines whether the specified instance has the same values (excluding Id and swap flags).
    /// </summary>
    public bool ValueEquals(CameraConfiguration? other)
    {
        if (other is null)
        {
            return false;
        }

        return Core.ValueEquals(other.Core);
    }

    private void SubscribeToNestedChanges(INotifyPropertyChanged? nested)
    {
        if (nested is not null)
        {
            nested.PropertyChanged += OnNestedPropertyChanged;
        }
    }

    private void OnNestedPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        // Forward the change notification for the parent property
        // WPF bindings to nested paths (e.g., Camera.Connection.IpAddress) work automatically
        // because WPF subscribes to each object's PropertyChanged in the path.
        // The connection type is display-only, so it is forwarded under its own
        // name instead of Connection (which makes the tile recreate its player).
        if (sender is ConnectionSettings &&
            e.PropertyName is nameof(ConnectionSettings.ConnectionType) or nameof(ConnectionSettings.EffectiveConnectionType))
        {
            if (e.PropertyName == nameof(ConnectionSettings.EffectiveConnectionType))
            {
                OnPropertyChanged(nameof(EffectiveConnectionType));
            }

            return;
        }

        var parentPropertyName = sender switch
        {
            ConnectionSettings => nameof(Connection),
            AuthenticationSettings => nameof(Authentication),
            CameraDisplaySettings => nameof(Display),
            StreamSettings => nameof(Stream),
            _ => null,
        };

        if (parentPropertyName is not null)
        {
            OnPropertyChanged(parentPropertyName);
        }
    }
}