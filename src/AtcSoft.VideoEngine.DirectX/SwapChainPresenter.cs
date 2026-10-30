namespace AtcSoft.VideoEngine.DirectX;

/// <summary>
/// Manages a DXGI composition swap chain and DirectComposition visual tree
/// for presenting BGRA textures to a WPF-hosted HWND.
/// </summary>
public sealed class SwapChainPresenter : IDisposable
{
    // DXGI device-lost HRESULTs — when Present returns one of these the
    // GPU device is gone (driver crash, sleep/resume, eviction) and the
    // swap chain is unusable until the consumer recreates it.
    private const int DxgiErrorDeviceRemoved = unchecked((int)0x887A0005);
    private const int DxgiErrorDeviceHung = unchecked((int)0x887A0006);
    private const int DxgiErrorDeviceReset = unchecked((int)0x887A0007);
    private const int DxgiErrorDriverInternalError = unchecked((int)0x887A0020);

    private readonly D3D11Device d3d11Device;
    private readonly IDXGISwapChain1 swapChain;
    private readonly IDCompositionDevice dcompDevice;
    private readonly IDCompositionTarget dcompTarget;
    private readonly IDCompositionVisual dcompVisual;
    private readonly IDCompositionVisual rootVisual;
    private readonly IDCompositionVisual backgroundVisual;
    private readonly IDCompositionSurface blackSurface;

    private readonly Lock presentLock = new();
    private uint swapChainWidth;
    private uint swapChainHeight;
    private int lastControlWidth;
    private int lastControlHeight;
    private float zoomLevel = 1.0f;
    private float panX;
    private float panY;
    private bool disposed;
    private volatile bool deviceLost;

    public SwapChainPresenter(
        D3D11Device d3d11Device,
        nint hwnd)
    {
        ArgumentNullException.ThrowIfNull(d3d11Device);

        this.d3d11Device = d3d11Device;

        using var dxgiDevice = d3d11Device.Device.QueryInterface<IDXGIDevice>();

        // Create swap chain via DXGI factory.
        dxgiDevice.GetAdapter(out var adapter);
        using (adapter)
        {
            using var factory = adapter.GetParent<IDXGIFactory2>();

            var desc = new SwapChainDescription1
            {
                Width = 2,
                Height = 2,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Ignore,
                Flags = SwapChainFlags.None,
            };

            swapChain = factory.CreateSwapChainForComposition(
                d3d11Device.Device,
                desc);
        }

        swapChainWidth = 2;
        swapChainHeight = 2;

        // Create DirectComposition device, target, and visual.
        dcompDevice = DComp.DCompositionCreateDevice<IDCompositionDevice>(dxgiDevice);

        // Topmost: false — render DComp visuals behind child HWNDs so the
        // overlay window (WPF content with camera info) appears on top of video.
        dcompDevice.CreateTargetForHwnd(
            hwnd,
            false,
            out dcompTarget).CheckError();

        // Build a 3-visual tree:
        //   rootVisual            — full-extent host (no transform)
        //   ├── backgroundVisual  — opaque black, stretched to cover the control
        //   └── dcompVisual       — swap chain, fit-to-control with optional zoom/pan
        // The background visual guarantees the letterbox region is black on every
        // GPU/driver. Without it, the area outside the swap-chain visual is
        // implementation-defined (some Intel/AMD drivers render it bright green
        // because the surface HWND uses WS_EX_NOREDIRECTIONBITMAP, which prevents
        // WPF's Background brush from ever being painted).
        dcompDevice.CreateVisual(out rootVisual).CheckError();
        dcompDevice.CreateVisual(out backgroundVisual).CheckError();
        dcompDevice.CreateVisual(out dcompVisual).CheckError();

        blackSurface = CreateBlackSurface(dcompDevice, d3d11Device.DeviceContext);
        backgroundVisual.SetContent(blackSurface).CheckError();
        backgroundVisual.SetBitmapInterpolationMode(BitmapInterpolationMode.NearestNeighbor).CheckError();

        dcompVisual.SetContent(swapChain).CheckError();

        rootVisual.AddVisual(backgroundVisual, insertAbove: false, referenceVisual: null!).CheckError();
        rootVisual.AddVisual(dcompVisual, insertAbove: true, backgroundVisual).CheckError();

        dcompTarget.SetRoot(rootVisual).CheckError();
        dcompDevice.Commit().CheckError();
    }

    /// <summary>
    /// Raised once when the underlying GPU device is lost. Subsequent
    /// <see cref="Present"/> calls are no-ops; the upstream consumer
    /// (typically <c>VideoPlayer</c>) should recreate the pipeline.
    /// </summary>
    public event EventHandler<EventArgs>? DeviceLost;

    /// <summary>
    /// Indicates whether the presenter has detected a lost GPU device.
    /// Once <c>true</c>, this presenter cannot recover and must be
    /// disposed and recreated by the consumer.
    /// </summary>
    public bool IsDeviceLost => deviceLost;

    private static IDCompositionSurface CreateBlackSurface(
        IDCompositionDevice device,
        ID3D11DeviceContext context)
    {
        device.CreateSurface(
            1,
            1,
            Format.B8G8R8A8_UNorm,
            AlphaMode.Ignore,
            out var surface).CheckError();

        var texture = surface.BeginDraw<ID3D11Texture2D>(null, out var offset);
        try
        {
            // Write a single opaque-black BGRA pixel at the surface's atlas offset.
            ReadOnlySpan<byte> blackPixel = [0x00, 0x00, 0x00, 0xFF];
            var box = new Box(
                offset.X,
                offset.Y,
                0,
                offset.X + 1,
                offset.Y + 1,
                1);
            context.UpdateSubresource(blackPixel, texture, 0, 4, 4, box);
        }
        finally
        {
            texture.Dispose();
        }

        surface.EndDraw().CheckError();
        return surface;
    }

    /// <summary>
    /// Copies the BGRA texture to the swap chain back buffer and presents it.
    /// Called on the demux thread.
    /// </summary>
    public void Present(
        ID3D11Texture2D bgraTexture,
        int videoWidth,
        int videoHeight)
    {
        if (disposed || deviceLost)
        {
            return;
        }

        var raisedDeviceLost = false;

        lock (presentLock)
        {
            if (disposed || deviceLost)
            {
                return;
            }

            var w = (uint)videoWidth;
            var h = (uint)videoHeight;

            if (w != swapChainWidth || h != swapChainHeight)
            {
                var resizeResult = swapChain.ResizeBuffers(
                    0,
                    w,
                    h,
                    Format.Unknown,
                    SwapChainFlags.None);

                if (IsDeviceLostResult(resizeResult))
                {
                    deviceLost = true;
                    raisedDeviceLost = true;
                }
                else
                {
                    swapChainWidth = w;
                    swapChainHeight = h;

                    // Swap chain dimensions changed — update the DComp transform
                    // so it scales correctly to the control size.
                    if (lastControlWidth > 0 && lastControlHeight > 0)
                    {
                        ApplyTransform();
                    }
                }
            }

            if (!deviceLost)
            {
                using var backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
                d3d11Device.DeviceContext.CopyResource(backBuffer, bgraTexture);
                var presentResult = swapChain.Present(0, PresentFlags.None);

                if (IsDeviceLostResult(presentResult))
                {
                    deviceLost = true;
                    raisedDeviceLost = true;
                }
            }
        }

        // Raise outside the lock so subscribers (e.g. VideoPlayer) can
        // safely re-enter the pipeline without re-acquiring presentLock.
        if (raisedDeviceLost)
        {
            DeviceLost?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsDeviceLostResult(SharpGen.Runtime.Result result)
    {
        if (result.Success)
        {
            return false;
        }

        return result.Code is DxgiErrorDeviceRemoved
                           or DxgiErrorDeviceHung
                           or DxgiErrorDeviceReset
                           or DxgiErrorDriverInternalError;
    }

    /// <summary>
    /// Updates the DComp visual transform to scale the swap chain content
    /// to the control size. Called on the WPF UI thread.
    /// </summary>
    public void Resize(
        int controlWidth,
        int controlHeight)
    {
        if (disposed || swapChainWidth == 0 || swapChainHeight == 0)
        {
            return;
        }

        lock (presentLock)
        {
            if (disposed || swapChainWidth == 0 || swapChainHeight == 0)
            {
                return;
            }

            lastControlWidth = controlWidth;
            lastControlHeight = controlHeight;
            ApplyTransform();
        }
    }

    /// <summary>
    /// Sets the zoom level and pan offset for the video viewport.
    /// </summary>
    /// <param name="zoom">Zoom level (1.0 = fit to control, >1.0 = zoomed in).</param>
    /// <param name="offsetX">Horizontal pan offset in normalized coordinates (0.0-1.0).</param>
    /// <param name="offsetY">Vertical pan offset in normalized coordinates (0.0-1.0).</param>
    public void SetZoom(
        float zoom,
        float offsetX,
        float offsetY)
    {
        if (disposed)
        {
            return;
        }

        lock (presentLock)
        {
            if (disposed)
            {
                return;
            }

            zoomLevel = Math.Max(1.0f, zoom);
            panX = offsetX;
            panY = offsetY;

            if (lastControlWidth > 0 && lastControlHeight > 0 && swapChainWidth > 0 && swapChainHeight > 0)
            {
                ApplyTransform();
            }
        }
    }

    private void ApplyTransform()
    {
        float scaleX = lastControlWidth / (float)swapChainWidth;
        float scaleY = lastControlHeight / (float)swapChainHeight;

        // Uniform scale preserves aspect ratio; the dedicated backgroundVisual
        // (1×1 black surface stretched to the control bounds) provides the
        // letterbox/pillarbox bars deterministically across all GPU drivers.
        float baseScale = Math.Min(scaleX, scaleY);
        float totalScale = baseScale * zoomLevel;

        // Video dimensions after scaling
        float scaledW = swapChainWidth * totalScale;
        float scaledH = swapChainHeight * totalScale;

        // Center when at fit-to-view; pan offset shifts the viewport when zoomed.
        // panX/panY are in normalized coordinates (0.0 = centered, -1.0/+1.0 = edges).
        float maxPanX = Math.Max(0, (scaledW - lastControlWidth) / 2f);
        float maxPanY = Math.Max(0, (scaledH - lastControlHeight) / 2f);

        float offsetX = ((lastControlWidth - scaledW) / 2f) - (panX * maxPanX);
        float offsetY = ((lastControlHeight - scaledH) / 2f) - (panY * maxPanY);

        var matrix = Matrix3x2.CreateScale(totalScale, totalScale)
            * Matrix3x2.CreateTranslation(offsetX, offsetY);
        dcompVisual.SetTransform(ref matrix);

        // Stretch the 1×1 black surface to fully cover the control area so the
        // letterbox bars are always opaque black.
        var backgroundMatrix = Matrix3x2.CreateScale(lastControlWidth, lastControlHeight);
        backgroundVisual.SetTransform(ref backgroundMatrix);

        dcompDevice.Commit();
    }

    public void Dispose()
    {
        lock (presentLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            dcompVisual.Dispose();
            backgroundVisual.Dispose();
            rootVisual.Dispose();
            blackSurface.Dispose();
            dcompTarget.Dispose();
            dcompDevice.Dispose();
            swapChain.Dispose();
        }
    }
}