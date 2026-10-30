namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Detects how a network camera is physically connected (wired or Wi-Fi).
/// Implementations never throw for an unreachable or unsupported camera:
/// they return <see cref="CameraConnectionType.None"/>, meaning "unknown".
/// Apply the result with <c>CameraConnectionTypeHelper.ApplyDetected</c>.
/// </summary>
public interface ICameraConnectionTypeDetector
{
    /// <summary>
    /// Detects the connection type of the camera at <paramref name="ipAddress"/>.
    /// </summary>
    /// <param name="ipAddress">The camera's IP address or host name.</param>
    /// <param name="userName">The camera user name, if any.</param>
    /// <param name="password">The camera password, if any.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="CameraConnectionType.Lan"/> or <see cref="CameraConnectionType.Wifi"/>
    /// when detected; otherwise <see cref="CameraConnectionType.None"/>.
    /// </returns>
    Task<CameraConnectionType> DetectAsync(
        string ipAddress,
        string? userName,
        string? password,
        CancellationToken cancellationToken = default);
}