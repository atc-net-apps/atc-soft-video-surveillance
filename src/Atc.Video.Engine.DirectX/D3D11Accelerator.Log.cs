namespace Atc.Video.Engine.DirectX;

public sealed partial class D3D11Accelerator
{
    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "D3D11 GPU accelerator initialized")]
    private partial void LogGpuAcceleratorInitialized();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "GPU frame processing failed ({Count} pictures since the last time this was logged; logged at most once a minute)")]
    private partial void LogGpuFrameProcessingFailed(Exception ex, int count);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "The graphics device was removed: no more pictures are processed on it until the player makes a new one")]
    private partial void LogGpuDeviceRemoved(Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "GPU snapshot capture failed")]
    private partial void LogGpuSnapshotCaptureFailed(Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "D3D11 GPU accelerator disposed")]
    private partial void LogGpuAcceleratorDisposed();
}