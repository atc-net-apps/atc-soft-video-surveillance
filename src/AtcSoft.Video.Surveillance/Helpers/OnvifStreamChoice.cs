namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// Which of a camera's ONVIF profiles become its main stream and its sub-stream: the largest
/// picture is the main stream; the smallest that is at most half as wide, and at least
/// <see cref="SmallestSubStreamWidth"/> wide, is the sub-stream (one of the main stream's shape
/// when two are as small). A squeezed sub-stream is fine: the analysis takes the main stream's
/// shape. A camera with one usable profile has no sub-stream.
/// </summary>
/// <param name="Main">The main stream.</param>
/// <param name="Sub">The sub-stream, or <see langword="null"/>.</param>
public sealed record OnvifStreamChoice(
    OnvifStreamProfile Main,
    OnvifStreamProfile? Sub)
{
    /// <summary>
    /// The narrowest stream taken as a sub-stream: narrower ones are too small for motion detection.
    /// </summary>
    public const int SmallestSubStreamWidth = 320;

    // How far apart two shapes may be and still be the same (2 %).
    private const double SameShape = 0.02;

    /// <summary>
    /// Chooses the main stream and the sub-stream.
    /// </summary>
    /// <param name="profiles">The camera's profiles, with their stream addresses.</param>
    /// <returns>The choice, or <see langword="null"/> when no profile has an RTSP address.</returns>
    public static OnvifStreamChoice? Choose(
        IEnumerable<OnvifStreamProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var usable = profiles
            .Where(p => p.StreamUri is { } uri && string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase) && p.Area > 0)
            .ToList();
        if (usable.Count == 0)
        {
            return null;
        }

        // A compressed stream before MJPEG: MJPEG is several times the size to record.
        var main = usable
            .OrderBy(p => IsMjpeg(p) ? 1 : 0)
            .ThenByDescending(p => p.Area)
            .First();

        var sub = usable
            .Where(p => !ReferenceEquals(p, main) && p.Width * 2 <= main.Width && p.Width >= SmallestSubStreamWidth)
            .OrderBy(p => p.Area)
            .ThenBy(p => IsSameShape(p, main) ? 0 : 1)
            .FirstOrDefault();

        return new OnvifStreamChoice(main, sub);
    }

    /// <summary>
    /// Fills a camera's connection from the choice: RTSP, the main stream's port and path, and
    /// the sub-stream's path (and its port when it differs). The address the person typed is
    /// kept: a camera often names another one of its addresses in its stream addresses. Nothing
    /// of the user info in an address is taken.
    /// </summary>
    /// <param name="connection">The camera's connection.</param>
    public void ApplyTo(ConnectionSettings connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var main = Main.StreamUri!;
        var mainPort = PortOf(main);
        connection.Source = CameraSource.Network;
        connection.Protocol = CameraProtocol.Rtsp;
        connection.Port = mainPort;
        connection.Path = main.PathAndQuery;

        if (Sub?.StreamUri is { } sub)
        {
            connection.SubStreamPath = sub.PathAndQuery;
            connection.SubStreamPort = PortOf(sub) == mainPort ? null : PortOf(sub);
        }
        else
        {
            connection.SubStreamPath = null;
            connection.SubStreamPort = null;
        }
    }

    private static int PortOf(Uri uri)
        => uri.IsDefaultPort || uri.Port <= 0 ? 554 : uri.Port;

    private static bool IsMjpeg(OnvifStreamProfile profile)
        => profile.Encoding.Contains("JPEG", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameShape(
        OnvifStreamProfile profile,
        OnvifStreamProfile main)
    {
        var shape = (double)profile.Width / profile.Height;
        var mainShape = (double)main.Width / main.Height;
        return Math.Abs(shape - mainShape) / mainShape <= SameShape;
    }
}