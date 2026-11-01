namespace Atc.Video.Engine.DirectX;

/// <summary>
/// Creates an FFmpeg <see cref="AVHWDeviceContext"/> wrapping the D3D11 device
/// so that the video decoder can output frames as D3D11 textures.
/// </summary>
internal sealed unsafe class HwAccelContext : IDisposable
{
    // Kept alive for as long as FFmpeg may call them.
    private static readonly AVD3D11VADeviceContext_lock LockCallback = Lock;
    private static readonly AVD3D11VADeviceContext_unlock UnlockCallback = Unlock;

    private AVBufferRef* deviceContextBuffer;
    private GCHandle lockHandle;
    private bool disposed;

    public HwAccelContext(D3D11Device d3d11Device)
    {
        deviceContextBuffer = av_hwdevice_ctx_alloc(AVHWDeviceType.D3d11va);
        if (deviceContextBuffer is null)
        {
            throw new InvalidOperationException("Failed to allocate D3D11VA hardware device context.");
        }

        var hwDevCtx = (AVHWDeviceContext*)deviceContextBuffer->data;
        var d3d11Ctx = (AVD3D11VADeviceContext*)hwDevCtx->hwctx;

        d3d11Ctx->device = (Flyleaf.FFmpeg.ID3D11Device*)(void*)d3d11Device.Device.NativePointer;
        d3d11Ctx->device_context = (Flyleaf.FFmpeg.ID3D11DeviceContext*)(void*)d3d11Device.DeviceContext.NativePointer;

        // FFmpeg's work on the immediate context takes the device's own lock, the one our picture
        // processing, presenting and snapshots take: they never interleave (unset, FFmpeg used a
        // lock of its own that our work did not know about).
        lockHandle = GCHandle.Alloc(d3d11Device);
        d3d11Ctx->lock_ctx = (void*)GCHandle.ToIntPtr(lockHandle);
        d3d11Ctx->@lock = LockCallback;
        d3d11Ctx->unlock = UnlockCallback;

        int ret = av_hwdevice_ctx_init(deviceContextBuffer);
        if (ret < 0)
        {
            av_buffer_unref(ref deviceContextBuffer);
            throw new FFmpegException(ret, "Failed to initialize D3D11VA hardware device context");
        }

        // FFmpeg now holds references to the D3D11 objects and will Release
        // them when the hw device context is freed. AddRef so the Vortice
        // wrappers can still safely Dispose without double-releasing.
        d3d11Device.Device.AddRef();
        d3d11Device.DeviceContext.AddRef();
    }

    public AVBufferRef* DeviceContextBuffer => deviceContextBuffer;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (deviceContextBuffer is not null)
        {
            av_buffer_unref(ref deviceContextBuffer);
        }

        if (lockHandle.IsAllocated)
        {
            lockHandle.Free();
        }
    }

    private static void Lock(void* context)
    {
        if (GCHandle.FromIntPtr((nint)context).Target is D3D11Device device)
        {
            device.EnterContext();
        }
    }

    private static void Unlock(void* context)
    {
        if (GCHandle.FromIntPtr((nint)context).Target is D3D11Device device)
        {
            device.LeaveContext();
        }
    }
}