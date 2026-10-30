namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class ServerHeartbeatService
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Heartbeat: uptime={UptimeSeconds}s, recordings={ActiveCount} (stuck={StuckCount}) [{CameraNames}], ws={WorkingSetMb:F0}MB (peak={PeakWorkingSetMb:F0}MB), handles={HandleCount} (delta={HandleDelta:+#;-#;0}, peak={PeakHandleCount}), threads={ThreadCount}, gc={Gen0}/{Gen1}/{Gen2}, drive='{Drive}' freeGb={FreeGb:F1}")]
    private partial void LogHeartbeat(
        long uptimeSeconds,
        int activeCount,
        int stuckCount,
        string cameraNames,
        double workingSetMb,
        double peakWorkingSetMb,
        int handleCount,
        int handleDelta,
        int peakHandleCount,
        int threadCount,
        int gen0,
        int gen1,
        int gen2,
        string drive,
        double freeGb);
}