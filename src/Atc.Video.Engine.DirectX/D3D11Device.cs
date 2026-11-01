namespace Atc.Video.Engine.DirectX;

/// <summary>
/// Manages the D3D11 device and device context with video processing and
/// multithreaded access support.
/// </summary>
public sealed class D3D11Device : IDisposable
{
    // The device's own lock: FFmpeg's decoder takes it for its work on the immediate context (see
    // HwAccelContext), and so does every sequence of ours (processing a picture, presenting it,
    // a snapshot), so they never interleave on it. Always taken last, after any lock of ours.
    private readonly ID3D11Multithread multithread;

    private bool disposed;

    public D3D11Device()
    {
        Device = D3D11.D3D11CreateDevice(
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport,
            FeatureLevel.Level_11_0);

        DeviceContext = Device.ImmediateContext;

        multithread = DeviceContext.QueryInterface<ID3D11Multithread>();
        multithread.SetMultithreadProtected(true);
    }

    public ID3D11Device Device { get; }

    public ID3D11DeviceContext DeviceContext { get; }

    /// <summary>
    /// Gets a value indicating whether the device was removed (a driver update or reset, a remote
    /// desktop session on another adapter): nothing drawn on it shows any more.
    /// </summary>
    public bool IsRemoved
    {
        get
        {
            try
            {
                return !disposed && Device.DeviceRemovedReason.Failure;
            }
            catch (SharpGen.Runtime.SharpGenException)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Takes the device's lock for a sequence of work on the immediate context (reentrant). Pair
    /// with <see cref="LeaveContext"/>.
    /// </summary>
    public void EnterContext()
        => multithread.Enter();

    /// <summary>
    /// Lets go of the device's lock taken with <see cref="EnterContext"/>.
    /// </summary>
    public void LeaveContext()
        => multithread.Leave();

    /// <summary>
    /// Gets the native ID3D11Multithread, for FFmpeg's lock callbacks.
    /// </summary>
    internal ID3D11Multithread Multithread => multithread;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        multithread.Dispose();
        DeviceContext.Dispose();
        Device.Dispose();
    }
}