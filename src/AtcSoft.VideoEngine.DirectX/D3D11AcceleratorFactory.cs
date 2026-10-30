namespace AtcSoft.VideoEngine.DirectX;

/// <summary>
/// Factory that creates <see cref="D3D11Accelerator"/> instances.
/// Returns <c>null</c> when D3D11 GPU acceleration is not available.
/// </summary>
public sealed partial class D3D11AcceleratorFactory : IGpuAcceleratorFactory
{
    public IGpuAccelerator? TryCreate(ILogger logger)
    {
        try
        {
            return new D3D11Accelerator(logger);
        }
        catch (Exception ex)
        {
            LogGpuAccelerationUnavailable(logger, ex);
            return null;
        }
    }
}