namespace AtcSoft.VideoEngine.DirectX;

/// <summary>
/// Creates an FFmpeg <see cref="AVHWDeviceContext"/> wrapping the D3D11 device
/// so that the video decoder can output frames as D3D11 textures.
/// </summary>
internal sealed unsafe class HwAccelContext : IDisposable
{
    private AVBufferRef* deviceContextBuffer;
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
    }
}