namespace AtcSoft.VideoEngine.DirectX;

/// <summary>
/// Renders software-decoded AVFrames into a BGRA D3D11 texture so the
/// existing SwapChainPresenter pipeline can display them. Used as the
/// fallback when the decoder doesn't deliver D3D11 textures — most
/// commonly MJPEG (no D3D11VA path on Windows), and any codec that
/// falls back to a CPU decoder. Caches the SWS context and the output
/// texture per (width, height, src-format, rotation) so steady-state
/// playback allocates nothing. These frames never reach the video
/// processor that rotates hardware frames, so the rotation is applied
/// here, on the CPU, before the upload.
/// </summary>
[SuppressMessage("", "CA1806:return value not used (av_*)", Justification = "FFmpeg APIs use return codes but failures still produce usable state in this path")]
internal sealed unsafe class CpuFrameUploader : IDisposable
{
    private readonly ID3D11Device device;
    private readonly ID3D11DeviceContext deviceContext;

    private SwsContext* swsCtx;
    private AVFrame* bgraFrame;
    private AVFrame* rotatedFrame;
    private ID3D11Texture2D? bgraTexture;

    private int cachedWidth;
    private int cachedHeight;
    private int outputWidth;
    private int outputHeight;
    private AVPixelFormat cachedSrcFormat = AVPixelFormat.None;
    private VideoRotation cachedRotation = VideoRotation.None;
    private VideoRotation rotation = VideoRotation.None;
    private bool disposed;

    public CpuFrameUploader(D3D11Device d3d11Device)
    {
        device = d3d11Device.Device;
        deviceContext = d3d11Device.DeviceContext;
    }

    public ID3D11Texture2D? BgraTexture => bgraTexture;

    /// <summary>
    /// Gets the texture width, which is the frame height after a quarter turn.
    /// </summary>
    public int Width => outputWidth;

    /// <summary>
    /// Gets the texture height, which is the frame width after a quarter turn.
    /// </summary>
    public int Height => outputHeight;

    /// <summary>
    /// Sets the clockwise rotation applied to subsequent frames.
    /// </summary>
    public void SetRotation(VideoRotation rotation)
    {
        this.rotation = rotation;
    }

    /// <summary>
    /// Uploads <paramref name="frame"/> (any libswscale-supported CPU
    /// pixel format) into the cached BGRA texture. Returns
    /// <see langword="true"/> when a valid texture is available, with
    /// <see cref="BgraTexture"/> / <see cref="Width"/> /
    /// <see cref="Height"/> reflecting the latest upload.
    /// </summary>
    public bool Upload(AVFrame* frame)
    {
        if (frame is null || frame->width <= 0 || frame->height <= 0)
        {
            return false;
        }

        var srcFormat = (AVPixelFormat)frame->format;
        if (srcFormat == AVPixelFormat.None || srcFormat == AVPixelFormat.D3d11)
        {
            // D3D11 hardware frames go through VideoProcessorRenderer
            // — this uploader exists only for CPU formats.
            return false;
        }

        if (!EnsurePipeline(frame->width, frame->height, srcFormat))
        {
            return false;
        }

        // Flyleaf.FFmpeg's byte_ptrArray8 doesn't expose a byte** root,
        // so unpack the plane pointers and strides into managed arrays
        // for the sws_scale call (mirrors FrameCapture's approach).
        byte** srcData = stackalloc byte*[]
        {
            (byte*)frame->data[0], (byte*)frame->data[1],
            (byte*)frame->data[2], (byte*)frame->data[3],
        };

        int* srcStride = stackalloc int[]
        {
            frame->linesize[0], frame->linesize[1],
            frame->linesize[2], frame->linesize[3],
        };

        byte** dstData = stackalloc byte*[]
        {
            (byte*)bgraFrame->data[0], (byte*)bgraFrame->data[1],
            (byte*)bgraFrame->data[2], (byte*)bgraFrame->data[3],
        };

        int* dstStride = stackalloc int[]
        {
            bgraFrame->linesize[0], bgraFrame->linesize[1],
            bgraFrame->linesize[2], bgraFrame->linesize[3],
        };

        sws_scale(swsCtx, srcData, srcStride, 0, frame->height, dstData, dstStride);

        var upload = bgraFrame;
        if (rotatedFrame is not null)
        {
            PixelRotator.Rotate(
                (byte*)bgraFrame->data[0],
                bgraFrame->linesize[0],
                cachedWidth,
                cachedHeight,
                bytesPerPixel: 4,
                cachedRotation,
                (byte*)rotatedFrame->data[0],
                rotatedFrame->linesize[0]);

            upload = rotatedFrame;
        }

        deviceContext.UpdateSubresource(
            bgraTexture!,
            0,
            null,
            upload->data[0],
            (uint)upload->linesize[0],
            0);

        return true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ReleasePipeline();
    }

    private bool EnsurePipeline(
        int width,
        int height,
        AVPixelFormat srcFormat)
    {
        if (swsCtx is not null
            && bgraTexture is not null
            && cachedWidth == width
            && cachedHeight == height
            && cachedSrcFormat == srcFormat
            && cachedRotation == rotation)
        {
            return true;
        }

        ReleasePipeline();

        var targetRotation = rotation;
        var (rotatedWidth, rotatedHeight) = PixelRotator.GetRotatedSize(width, height, targetRotation);

        try
        {
            swsCtx = sws_getContext(
                width,
                height,
                srcFormat,
                width,
                height,
                AVPixelFormat.Bgra,
                SwsFlags.Bilinear,
                null,
                null,
                null);

            if (swsCtx is null)
            {
                return false;
            }

            bgraFrame = AllocateBgraFrame(width, height);
            if (bgraFrame is null)
            {
                return false;
            }

            if (targetRotation != VideoRotation.None)
            {
                rotatedFrame = AllocateBgraFrame(rotatedWidth, rotatedHeight);
                if (rotatedFrame is null)
                {
                    return false;
                }
            }

            var desc = new Texture2DDescription
            {
                Width = (uint)rotatedWidth,
                Height = (uint)rotatedHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
            };

            bgraTexture = device.CreateTexture2D(desc);

            cachedWidth = width;
            cachedHeight = height;
            outputWidth = rotatedWidth;
            outputHeight = rotatedHeight;
            cachedSrcFormat = srcFormat;
            cachedRotation = targetRotation;
            return true;
        }
        catch
        {
            ReleasePipeline();
            throw;
        }
    }

    private static AVFrame* AllocateBgraFrame(
        int width,
        int height)
    {
        var frame = av_frame_alloc();
        if (frame is null)
        {
            return null;
        }

        frame->format = (int)AVPixelFormat.Bgra;
        frame->width = width;
        frame->height = height;

        // Align to 32 bytes — comfortably above the 16-byte SIMD
        // boundary sws_scale prefers and a multiple of 4 bytes/pixel
        // so each scanline has the same stride.
        if (av_frame_get_buffer(frame, 32) < 0)
        {
            av_frame_free(ref frame);
            return null;
        }

        return frame;
    }

    private void ReleasePipeline()
    {
        if (bgraFrame is not null)
        {
            var f = bgraFrame;
            av_frame_free(ref f);
            bgraFrame = null;
        }

        if (rotatedFrame is not null)
        {
            var f = rotatedFrame;
            av_frame_free(ref f);
            rotatedFrame = null;
        }

        if (swsCtx is not null)
        {
            sws_freeContext(swsCtx);
            swsCtx = null;
        }

        bgraTexture?.Dispose();
        bgraTexture = null;

        cachedWidth = 0;
        cachedHeight = 0;
        outputWidth = 0;
        outputHeight = 0;
        cachedSrcFormat = AVPixelFormat.None;
        cachedRotation = VideoRotation.None;
    }
}