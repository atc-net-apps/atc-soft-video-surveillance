namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// Source-generated high-performance log methods for <see cref="CameraGrid"/>.
/// </summary>
public partial class CameraGrid
{
    [LoggerMessage(Level = LogLevel.Information, Message = "RecreateConnectedPlayers: {TrackedCount} tracked tile(s)")]
    private static partial void LogRecreateTrackedCount(ILogger logger, int trackedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "RecreateConnectedPlayers: skipping '{CameraName}' (state={State})")]
    private static partial void LogRecreateSkipped(ILogger logger, string cameraName, ConnectionState state);

    [LoggerMessage(Level = LogLevel.Information, Message = "RecreateConnectedPlayers: recreating player for '{CameraName}'")]
    private static partial void LogRecreateRecreating(ILogger logger, string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Replacing zombie tile for '{CameraName}' — RDP/desktop session rebuild created a new tile, disposing the old one")]
    private static partial void LogZombieReplaced(ILogger logger, string cameraName);
}