namespace Atc.Video.Engine.DirectX;

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

    // A wrapper of the decoder's texture (owned by FFmpeg: never disposed), kept while it is the same.
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "A non-owning wrapper of a texture FFmpeg owns; disposing it would release FFmpeg's texture.")]
    private ID3D11Texture2D? nv12Wrapper;

    // How long a snapshot waits for the picture (see CaptureSnapshot).
    private static readonly TimeSpan SnapshotLockWait = TimeSpan.FromSeconds(2);

    private ID3D11Texture2D? latestBgraTexture;
    private int latestWidth;
    private int latestHeight;
    private bool disposed;

    // A device that was removed (a driver reset, a GPU taken away) takes no more pictures: every one
    // would fail and log. The player above makes a new device (DeviceLost).
    private volatile bool deviceGone;

    // Failures are logged at most once a minute, with how many there were since.
    private long lastFailureLoggedAt = long.MinValue;
    private int failuresSinceLogged;

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

    /// <inheritdoc />
    public bool IsDeviceLost => !disposed && d3d11Device.IsRemoved;

    /// <summary>
    /// Gets the underlying D3D11 device for swap chain creation.
    /// </summary>
    public D3D11Device D3D11DeviceRef => d3d11Device;

    public event Action? FrameReady;

    public void OnFrameDecoded(AVFrame* frame)
    {
        if (frame is null || deviceGone)
        {
            return;
        }

        var srcFormat = (AVPixelFormat)frame->format;

        // Runs on the thread that reads the camera and feeds the recorder: it never waits here. While
        // the picture is being shown or captured (frameLock held by the present thread or a
        // snapshot), or showing it hangs (a locked session, a monitor that is off), this picture is
        // not shown, and the camera is read on.
        if (!frameLock.TryEnter())
        {
            return;
        }

        try
        {
            d3d11Device.EnterContext();
            try
            {
                if (srcFormat == AVPixelFormat.D3d11)
                {
                    ProcessHardwareFrame(frame);
                }
                else
                {
                    ProcessSoftwareFrame(frame);
                }
            }
            finally
            {
                d3d11Device.LeaveContext();
                frameLock.Exit();
            }

            // The subscriber (a VideoHost) only wakes its own present thread.
            FrameReady?.Invoke();
        }
        catch (Exception ex)
        {
            OnFrameFailed(ex);
        }
    }

    private void OnFrameFailed(Exception ex)
    {
        if (d3d11Device.IsRemoved)
        {
            deviceGone = true;
            LogGpuDeviceRemoved(ex);
            return;
        }

        failuresSinceLogged++;
        var now = Environment.TickCount64;
        if (lastFailureLoggedAt != long.MinValue && now - lastFailureLoggedAt < 60_000)
        {
            return;
        }

        LogGpuFrameProcessingFailed(ex, failuresSinceLogged);
        lastFailureLoggedAt = now;
        failuresSinceLogged = 0;
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
        // The decoder writes into the same texture array all along: one wrapper while it does.
        if (nv12Wrapper is null || nv12Wrapper.NativePointer != texturePtr)
        {
            nv12Wrapper = new ID3D11Texture2D(texturePtr);
        }

        var nv12Texture = nv12Wrapper;

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
    /// Uses the latest picture (a BGRA texture and its size) while no new one is written over it:
    /// the picture is shown this way, on a thread of the viewer's own.
    /// </summary>
    /// <param name="use">What to do with the texture, its width and its height.</param>
    /// <returns><see langword="false"/> when there is no picture yet.</returns>
    public bool UseLatestFrame(Action<ID3D11Texture2D, int, int> use)
    {
        ArgumentNullException.ThrowIfNull(use);

        lock (frameLock)
        {
            if (latestBgraTexture is null || latestWidth <= 0 || latestHeight <= 0)
            {
                return false;
            }

            use(latestBgraTexture, latestWidth, latestHeight);
            return true;
        }
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
        // The lock is waited for a moment only: a picture that is being shown and hangs (a locked
        // session) holds it, and the caller then takes its picture from memory instead.
        if (!frameLock.TryEnter(SnapshotLockWait))
        {
            return null;
        }

        try
        {
            if (latestBgraTexture is null || latestWidth <= 0 || latestHeight <= 0)
            {
                return null;
            }

            d3d11Device.EnterContext();
            try
            {
                return snapshotCapture.Capture(latestBgraTexture, latestWidth, latestHeight);
            }
            finally
            {
                d3d11Device.LeaveContext();
            }
        }
        catch (Exception ex)
        {
            LogGpuSnapshotCaptureFailed(ex);
            return null;
        }
        finally
        {
            frameLock.Exit();
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
            d3d11Device.EnterContext();
            try
            {
                renderer.SetRotation(rotation);
                cpuUploader.SetRotation(rotation);
            }
            finally
            {
                d3d11Device.LeaveContext();
            }
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