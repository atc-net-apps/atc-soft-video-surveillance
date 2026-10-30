namespace AtcSoft.VideoEngine.DirectX;

public sealed partial class D3D11Accelerator
{
    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "D3D11 GPU accelerator initialized")]
    private partial void LogGpuAcceleratorInitialized();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "GPU frame processing failed")]
    private partial void LogGpuFrameProcessingFailed(Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "GPU snapshot capture failed")]
    private partial void LogGpuSnapshotCaptureFailed(Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "D3D11 GPU accelerator disposed")]
    private partial void LogGpuAcceleratorDisposed();
}