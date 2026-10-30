namespace AtcSoft.VideoEngine.DirectX;

/// <summary>
/// Manages the D3D11 device and device context with video processing and
/// multithreaded access support.
/// </summary>
public sealed class D3D11Device : IDisposable
{
    private bool disposed;

    public D3D11Device()
    {
        Device = D3D11.D3D11CreateDevice(
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport,
            FeatureLevel.Level_11_0);

        DeviceContext = Device.ImmediateContext;

        using var multithread = DeviceContext.QueryInterface<ID3D11Multithread>();
        multithread.SetMultithreadProtected(true);
    }

    public ID3D11Device Device { get; }

    public ID3D11DeviceContext DeviceContext { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DeviceContext.Dispose();
        Device.Dispose();
    }
}