namespace AtcSoft.VideoEngine.DirectX;

/// <summary>
/// Converts NV12 decoded textures to BGRA using the D3D11 Video Processor,
/// which runs on the GPU's dedicated video processing hardware.
/// Optionally applies a clockwise rotation (0/90/180/270°) at this stage so
/// display, snapshots, and downstream consumers all receive rotated frames.
/// </summary>
internal sealed class VideoProcessorRenderer : IDisposable
{
    private readonly ID3D11VideoDevice videoDevice;
    private readonly ID3D11VideoContext videoContext;
    private readonly ID3D11Device device;

    private ID3D11VideoProcessorEnumerator? enumerator;
    private ID3D11VideoProcessor? processor;
    private ID3D11Texture2D? outputTexture;
    private ID3D11VideoProcessorOutputView? outputView;

    private int cachedInputWidth;
    private int cachedInputHeight;
    private VideoRotation cachedRotation = VideoRotation.None;
    private VideoRotation rotation = VideoRotation.None;
    private bool disposed;

    public VideoProcessorRenderer(D3D11Device d3d11Device)
    {
        device = d3d11Device.Device;
        videoDevice = device.QueryInterface<ID3D11VideoDevice>();
        videoContext = d3d11Device.DeviceContext.QueryInterface<ID3D11VideoContext>();
    }

    /// <summary>
    /// Gets the latest BGRA output texture after processing.
    /// </summary>
    public ID3D11Texture2D? OutputTexture => outputTexture;

    /// <summary>
    /// Gets the current output width in BGRA texture pixels.
    /// For 90°/270° rotations this is the input height, not the input width.
    /// </summary>
    public int OutputWidth
        => IsQuarterTurn(cachedRotation) ? cachedInputHeight : cachedInputWidth;

    /// <summary>
    /// Gets the current output height in BGRA texture pixels.
    /// For 90°/270° rotations this is the input width, not the input height.
    /// </summary>
    public int OutputHeight
        => IsQuarterTurn(cachedRotation) ? cachedInputWidth : cachedInputHeight;

    /// <summary>
    /// Sets the clockwise rotation applied to subsequent frames. Takes effect
    /// on the next <see cref="ProcessFrame"/> call; the pipeline (output texture
    /// + view) is rebuilt automatically when the rotation flips dimensions.
    /// </summary>
    public void SetRotation(VideoRotation rotation)
    {
        this.rotation = rotation;
    }

    /// <summary>
    /// Processes a decoded NV12 texture through the Video Processor, producing a BGRA output.
    /// </summary>
    /// <param name="nv12Texture">The NV12 texture from the decoder.</param>
    /// <param name="arraySlice">The texture array slice index.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    public void ProcessFrame(
        ID3D11Texture2D nv12Texture,
        int arraySlice,
        int width,
        int height)
    {
        EnsurePipeline(width, height);

        var inputViewDesc = new VideoProcessorInputViewDescription
        {
            ViewDimension = VideoProcessorInputViewDimension.Texture2D,
            Texture2D = { ArraySlice = (uint)arraySlice, MipSlice = 0 },
        };

        videoDevice.CreateVideoProcessorInputView(
            nv12Texture,
            enumerator!,
            inputViewDesc,
            out var inputView).CheckError();

        using (inputView)
        {
            // Source rect = visible input area (excludes decoder padding rows for HEVC).
            // Dest rect = output texture extents (swapped for 90/270 rotation).
            var sourceRect = new RawRect(0, 0, width, height);
            var destRect = new RawRect(0, 0, OutputWidth, OutputHeight);
            videoContext.VideoProcessorSetStreamSourceRect(processor!, 0, true, sourceRect);
            videoContext.VideoProcessorSetStreamDestRect(processor!, 0, true, destRect);
            videoContext.VideoProcessorSetStreamRotation(processor!, 0, true, MapRotation(cachedRotation));

            VideoProcessorStream[] streams =
            [
                new VideoProcessorStream
                {
                    Enable = true,
                    InputSurface = inputView,
                },
            ];

            videoContext.VideoProcessorBlt(
                processor!,
                outputView!,
                0,
                (uint)streams.Length,
                streams);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ReleasePipeline();
        videoContext.Dispose();
        videoDevice.Dispose();
    }

    private static bool IsQuarterTurn(VideoRotation r)
        => r is VideoRotation.Rotate90 or VideoRotation.Rotate270;

    private static VideoProcessorRotation MapRotation(VideoRotation r)
        => r switch
        {
            VideoRotation.Rotate90 => VideoProcessorRotation.Rotation90,
            VideoRotation.Rotate180 => VideoProcessorRotation.Rotation180,
            VideoRotation.Rotate270 => VideoProcessorRotation.Rotation270,
            _ => VideoProcessorRotation.Identity,
        };

    private void EnsurePipeline(
        int width,
        int height)
    {
        // Pipeline cache key includes rotation so a 0°→90° change reallocates the
        // output texture at the swapped dimensions.
        if (enumerator is not null
            && cachedInputWidth == width
            && cachedInputHeight == height
            && cachedRotation == rotation)
        {
            return;
        }

        ReleasePipeline();

        cachedInputWidth = width;
        cachedInputHeight = height;
        cachedRotation = rotation;

        var outputWidth = OutputWidth;
        var outputHeight = OutputHeight;

        var contentDesc = new VideoProcessorContentDescription
        {
            InputFrameFormat = VideoFrameFormat.Progressive,
            InputWidth = (uint)width,
            InputHeight = (uint)height,
            OutputWidth = (uint)outputWidth,
            OutputHeight = (uint)outputHeight,
            InputFrameRate = new Rational(
                30,
                1),
            OutputFrameRate = new Rational(
                30,
                1),
            Usage = VideoUsage.PlaybackNormal,
        };

        // Wrap allocation in try/catch so a failure at the last step (e.g.
        // CreateVideoProcessorOutputView under GPU memory pressure) doesn't
        // strand the earlier-allocated enumerator/processor/outputTexture.
        try
        {
            videoDevice.CreateVideoProcessorEnumerator(
                ref contentDesc,
                out enumerator).CheckError();

            videoDevice.CreateVideoProcessor(
                enumerator,
                0,
                out processor).CheckError();

            var outputDesc = new Texture2DDescription
            {
                Width = (uint)outputWidth,
                Height = (uint)outputHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
            };

            outputTexture = device.CreateTexture2D(outputDesc);

            var outputViewDesc = new VideoProcessorOutputViewDescription
            {
                ViewDimension = VideoProcessorOutputViewDimension.Texture2D,
            };

            videoDevice.CreateVideoProcessorOutputView(
                outputTexture,
                enumerator,
                outputViewDesc,
                out outputView).CheckError();
        }
        catch
        {
            ReleasePipeline();
            throw;
        }
    }

    private void ReleasePipeline()
    {
        outputView?.Dispose();
        outputView = null;

        outputTexture?.Dispose();
        outputTexture = null;

        processor?.Dispose();
        processor = null;

        enumerator?.Dispose();
        enumerator = null;

        cachedInputWidth = 0;
        cachedInputHeight = 0;
        cachedRotation = VideoRotation.None;
    }
}