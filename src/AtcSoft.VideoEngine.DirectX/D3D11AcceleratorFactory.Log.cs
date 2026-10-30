namespace AtcSoft.VideoEngine.DirectX;

public sealed partial class D3D11AcceleratorFactory
{
    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "D3D11 GPU acceleration unavailable, using CPU fallback")]
    private static partial void LogGpuAccelerationUnavailable(ILogger logger, Exception ex);
}