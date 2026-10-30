namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class UsbDeviceCacheEvictor
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to drop the cached USB device list")]
    private partial void LogEvictFailed(Exception ex);
}