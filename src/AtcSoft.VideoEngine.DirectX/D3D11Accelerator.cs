namespace AtcSoft.VideoEngine.DirectX;

/// <summary>
/// Composition root implementing <see cref="IGpuAccelerator"/> for D3D11VA.
/// Composes <see cref="D3D11Device"/>, <see cref="HwAccelContext"/>,
/// <see cref="VideoProcessorRenderer"/>, and <see cref="GpuSnapshotCapture"/>.
/// </summary>
public sealed unsafe partial class D3D11Accelerator : IGpuAccelerator
{
    private readonly ILogger logger;
    private readonly D3D11Device d3d11Device;
    private readonly HwAccelContext hwAccelContext;
    private readonly VideoProcessorRenderer renderer;
    private readonly CpuFrameUploader cpuUploader;
    private readonly GpuSnapshotCapture snapshotCapture;
    private readonly Lock frameLock = new();

    private ID3D11Texture2D? latestBgraTexture;
    private int latestWidth;
    private int latestHeight;
    private bool disposed;

    public D3D11Accelerator(ILogger logger)
    {
        this.logger = logger;

        d3d11Device = new D3D11Device();
        hwAccelContext = new HwAccelContext(d3d11Device);
        renderer = new VideoProcessorRenderer(d3d11Device);
        cpuUploader = new CpuFrameUploader(d3d11Device);
        snapshotCapture = new GpuSnapshotCapture(d3d11Device);

        LogGpuAcceleratorInitialized();
    }

    public AVHWDeviceType HwDeviceType => AVHWDeviceType.D3d11va;

    public AVBufferRef* HwDeviceContext => hwAccelContext.DeviceContextBuffer;

    public bool IsInitialized => !disposed && HwDeviceContext is not null;

    /// <summary>
    /// Gets the underlying D3D11 device for swap chain creation.
    /// </summary>
    public D3D11Device D3D11DeviceRef => d3d11Device;

    public event Action? FrameReady;

    public void OnFrameDecoded(AVFrame* frame)
    {
        if (frame is null)
        {
            return;
        }

        var srcFormat = (AVPixelFormat)frame->format;

        try
        {
            // Hold frameLock during GPU processing + present to prevent
            // CaptureSnapshot from reading the output texture mid-write.
            // D3D11 immediate context is single-threaded.
            lock (frameLock)
            {
                if (srcFormat == AVPixelFormat.D3d11)
                {
                    ProcessHardwareFrame(frame);
                }
                else
                {
                    ProcessSoftwareFrame(frame);
                }

                FrameReady?.Invoke();
            }
        }
        catch (Exception ex)
        {
            LogGpuFrameProcessingFailed(ex);
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "nv12Texture is a non-owning wrapper around an FFmpeg-owned texture pointer; disposing it would corrupt FFmpeg's HW frames context.")]
    private void ProcessHardwareFrame(AVFrame* frame)
    {
        var texturePtr = frame->data[0];
        var arrayIndex = (int)frame->data[1];

        if (texturePtr == nint.Zero)
        {
            return;
        }

        // Wrap the FFmpeg-owned texture pointer as a Vortice COM object.
        // Do NOT dispose — the texture is owned by FFmpeg's HW frames context.
        var nv12Texture = new ID3D11Texture2D(texturePtr);

        // Use the frame's display dimensions, not the texture's allocated size.
        // HEVC decoders pad the texture height up to the next CTU multiple
        // (e.g., 1280×720 → 1280×768); those padding rows hold uninitialized
        // data that renders as a green strip on some GPUs (notably Intel UHD).
        int width = frame->width;
        int height = frame->height;

        renderer.ProcessFrame(nv12Texture, arrayIndex, width, height);

        latestBgraTexture = renderer.OutputTexture;
        latestWidth = renderer.OutputWidth;
        latestHeight = renderer.OutputHeight;
    }

    private void ProcessSoftwareFrame(AVFrame* frame)
    {
        // CPU-decoded path — typically MJPEG (no D3D11VA hwaccel on
        // Windows) or any codec that falls back to a software decoder.
        // The uploader converts the YUV (or RGB) plane to BGRA via
        // libswscale and refreshes a cached D3D11 texture that the
        // SwapChainPresenter can blit directly.
        if (!cpuUploader.Upload(frame))
        {
            return;
        }

        latestBgraTexture = cpuUploader.BgraTexture;
        latestWidth = cpuUploader.Width;
        latestHeight = cpuUploader.Height;
    }

    /// <summary>
    /// Retrieves the latest BGRA texture and its dimensions (thread-safe).
    /// </summary>
    public bool TryGetBgraTexture(
        out ID3D11Texture2D? texture,
        out int width,
        out int height)
    {
        lock (frameLock)
        {
            texture = latestBgraTexture;
            width = latestWidth;
            height = latestHeight;
        }

        return texture is not null && width > 0 && height > 0;
    }

    public byte[]? CaptureSnapshot()
    {
        // Hold frameLock during the entire capture to prevent OnFrameDecoded
        // from writing to the BGRA texture (via ProcessFrame) while
        // CopyResource reads from it. D3D11 immediate context is single-threaded.
        lock (frameLock)
        {
            if (latestBgraTexture is null || latestWidth <= 0 || latestHeight <= 0)
            {
                return null;
            }

            try
            {
                return snapshotCapture.Capture(latestBgraTexture, latestWidth, latestHeight);
            }
            catch (Exception ex)
            {
                LogGpuSnapshotCaptureFailed(ex);
                return null;
            }
        }
    }

    public void SetRotation(VideoRotation rotation)
    {
        // Take frameLock so a concurrent OnFrameDecoded sees a coherent view of
        // (rotation + cached pipeline). The renderer's EnsurePipeline reallocates
        // the output texture if the rotation flips dimensions. Software frames
        // bypass the renderer, so the uploader rotates those.
        lock (frameLock)
        {
            renderer.SetRotation(rotation);
            cpuUploader.SetRotation(rotation);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        lock (frameLock)
        {
            latestBgraTexture = null;
        }

        snapshotCapture.Dispose();
        cpuUploader.Dispose();
        renderer.Dispose();
        hwAccelContext.Dispose();
        d3d11Device.Dispose();

        LogGpuAcceleratorDisposed();
    }
}