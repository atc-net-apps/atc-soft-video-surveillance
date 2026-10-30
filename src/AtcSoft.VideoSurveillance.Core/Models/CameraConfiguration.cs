namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// Represents the configuration for a network camera (Core POCO).
/// </summary>
public class CameraConfiguration
{
    /// <summary>
    /// Gets or sets the unique identifier for the camera.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the connection settings.
    /// </summary>
    public ConnectionSettings Connection { get; set; } = new();

    /// <summary>
    /// Gets or sets the authentication settings.
    /// </summary>
    public AuthenticationSettings Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the display settings.
    /// </summary>
    public CameraDisplaySettings Display { get; set; } = new();

    /// <summary>
    /// Gets or sets the streaming settings.
    /// </summary>
    public StreamSettings Stream { get; set; } = new();

    /// <summary>
    /// Gets or sets the per-camera overrides.
    /// </summary>
    public CameraOverrides Overrides { get; set; } = new();

    /// <summary>
    /// Gets or sets the camera's motion zones, drawn on the picture as it is shown.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON deserialization")]
    [SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Required for JSON deserialization")]
    public List<MotionZone> MotionZones { get; set; } = [];

    /// <summary>
    /// Builds the camera stream URI based on the configuration. Only
    /// supported for network cameras; USB cameras have no URL and must
    /// be resolved through
    /// <see cref="Helpers.CameraUriHelper.BuildSourceLocator(CameraConfiguration)"/>.
    /// </summary>
    /// <returns>The constructed URI for the camera stream.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Models.Settings.ConnectionSettings.Source"/>
    /// is not <see cref="CameraSource.Network"/>.
    /// </exception>
    public Uri BuildUri()
    {
        if (Connection.Source != CameraSource.Network)
        {
            throw new InvalidOperationException(
                $"Cannot build a URI for a {Connection.Source} camera; use CameraUriHelper.BuildSourceLocator(camera) instead.");
        }

        var scheme = Connection.Protocol.ToScheme();

        var userInfo = !string.IsNullOrEmpty(Authentication.UserName)
            ? $"{Uri.EscapeDataString(Authentication.UserName)}:{Uri.EscapeDataString(Authentication.Password ?? string.Empty)}@"
            : string.Empty;

        var normalizedPath = string.IsNullOrEmpty(Connection.Path)
            ? string.Empty
            : $"/{Connection.Path.TrimStart('/')}";

        return new Uri($"{scheme}://{userInfo}{BracketIfIPv6(Connection.IpAddress)}:{Connection.Port}{normalizedPath}");
    }

    private static string BracketIfIPv6(string host)
        => host.StartsWith('[') ? host : host.Contains(':', StringComparison.Ordinal) ? $"[{host}]" : host;

    /// <inheritdoc />
    public override string ToString()
        => $"CameraConfiguration {{ Id={Id.ToString().Substring(0, 8)}, DisplayName='{Display.DisplayName}', IpAddress='{Connection.IpAddress}' }}";

    /// <summary>
    /// Creates a deep copy of this camera configuration.
    /// </summary>
    public CameraConfiguration Clone()
        => new()
        {
            Id = Id,
            Connection = Connection.Clone(),
            Authentication = Authentication.Clone(),
            Display = Display.Clone(),
            Stream = Stream.Clone(),
            Overrides = Overrides.Clone(),
            MotionZones = [.. MotionZones.Select(z => z.Clone())],
        };

    /// <summary>
    /// Copies values from another camera configuration.
    /// </summary>
    public void CopyFrom(CameraConfiguration source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Connection.CopyFrom(source.Connection);
        Authentication.CopyFrom(source.Authentication);
        Display.CopyFrom(source.Display);
        Stream.CopyFrom(source.Stream);
        Overrides = source.Overrides.Clone();
        MotionZones = [.. source.MotionZones.Select(z => z.Clone())];
    }

    /// <summary>
    /// Determines whether the specified instance has the same values (excluding Id).
    /// </summary>
    public bool ValueEquals(CameraConfiguration? other)
    {
        if (other is null)
        {
            return false;
        }

        var overridesEqual = (Overrides is null && other.Overrides is null) ||
                             (Overrides?.ValueEquals(other.Overrides) == true);

        return Connection.ValueEquals(other.Connection) &&
               Authentication.ValueEquals(other.Authentication) &&
               Display.ValueEquals(other.Display) &&
               Stream.ValueEquals(other.Stream) &&
               overridesEqual &&
               MotionZones.Count == other.MotionZones.Count &&
               MotionZones.Zip(other.MotionZones).All(p => p.First.ValueEquals(p.Second));
    }
}