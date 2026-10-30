namespace AtcSoft.VideoEngine.DirectX;

/// <summary>
/// Reads a BGRA GPU texture back to CPU memory and encodes it as PNG
/// using FFmpeg's PNG encoder.
/// </summary>
internal sealed unsafe class GpuSnapshotCapture : IDisposable
{
    private readonly ID3D11Device device;
    private readonly ID3D11DeviceContext deviceContext;

    private ID3D11Texture2D? stagingTexture;
    private SwsContext* swsCtx;
    private AVCodecContext* pngEncCtx;
    private AVFrame* rgbFrame;
    private int cachedWidth;
    private int cachedHeight;
    private bool disposed;

    public GpuSnapshotCapture(D3D11Device d3d11Device)
    {
        device = d3d11Device.Device;
        deviceContext = d3d11Device.DeviceContext;
    }

    /// <summary>
    /// Captures the given BGRA texture as PNG bytes.
    /// </summary>
    /// <param name="sourceTexture">The BGRA texture to capture.</param>
    /// <param name="width">Texture width.</param>
    /// <param name="height">Texture height.</param>
    /// <returns>PNG-encoded bytes, or <c>null</c> if capture failed.</returns>
    public byte[]? Capture(
        ID3D11Texture2D sourceTexture,
        int width,
        int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        EnsureStagingTexture(width, height);
        EnsureEncoder(width, height);

        deviceContext.CopyResource(stagingTexture!, sourceTexture);

        var mapped = deviceContext.Map(stagingTexture!, 0, MapMode.Read);
        try
        {
            ConvertBgraToRgb24(mapped.DataPointer, (int)mapped.RowPitch, width, height);
        }
        finally
        {
            deviceContext.Unmap(stagingTexture!, 0);
        }

        return EncodePng();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        FreeEncoder();

        stagingTexture?.Dispose();
        stagingTexture = null;
    }

    private void EnsureStagingTexture(
        int width,
        int height)
    {
        if (stagingTexture is not null && cachedWidth == width && cachedHeight == height)
        {
            return;
        }

        stagingTexture?.Dispose();

        var desc = new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
        };

        stagingTexture = device.CreateTexture2D(desc);
    }

    private void EnsureEncoder(
        int width,
        int height)
    {
        if (pngEncCtx is not null && cachedWidth == width && cachedHeight == height)
        {
            return;
        }

        FreeEncoder();

        // A throw between the first successful alloc and the final
        // avcodec_open2 used to leave a partially-initialized state around.
        // Run all allocations under try/catch so any failure resets every
        // field via FreeEncoder before propagating.
        AVDictionary* opts = null;
        try
        {
            swsCtx = sws_getContext(
                width,
                height,
                AVPixelFormat.Bgra,
                width,
                height,
                AVPixelFormat.Rgb24,
                SwsFlags.Bilinear,
                null,
                null,
                null);

            if (swsCtx is null)
            {
                throw new InvalidOperationException("Failed to create SwsContext for BGRA→RGB24.");
            }

            rgbFrame = av_frame_alloc();
            if (rgbFrame is null)
            {
                throw new InvalidOperationException("Failed to allocate RGB frame.");
            }

            rgbFrame->format = (int)AVPixelFormat.Rgb24;
            rgbFrame->width = width;
            rgbFrame->height = height;

            int ret = av_frame_get_buffer(rgbFrame, 0);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to allocate RGB frame buffer");
            }

            var encoder = avcodec_find_encoder_by_name("png");
            if (encoder is null)
            {
                throw new InvalidOperationException("PNG encoder not found.");
            }

            pngEncCtx = avcodec_alloc_context3(encoder);
            if (pngEncCtx is null)
            {
                throw new InvalidOperationException("Failed to allocate PNG encoder context.");
            }

            pngEncCtx->width = width;
            pngEncCtx->height = height;
            pngEncCtx->pix_fmt = AVPixelFormat.Rgb24;
            pngEncCtx->time_base = new AVRational { Num = 1, Den = 1 };

            ret = avcodec_open2(pngEncCtx, encoder, ref opts);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to open PNG encoder");
            }

            cachedWidth = width;
            cachedHeight = height;
        }
        catch
        {
            FreeEncoder();
            throw;
        }
        finally
        {
            av_dict_free(ref opts);
        }
    }

    private void ConvertBgraToRgb24(
        nint bgraData,
        int rowPitch,
        int width,
        int height)
    {
        byte** srcData = stackalloc byte*[] { (byte*)bgraData, null, null, null };
        int* srcStride = stackalloc int[] { rowPitch, 0, 0, 0 };

        byte** dstData = stackalloc byte*[]
        {
            (byte*)rgbFrame->data[0],
            (byte*)rgbFrame->data[1],
            (byte*)rgbFrame->data[2],
            (byte*)rgbFrame->data[3],
        };
        int* dstStride = stackalloc int[]
        {
            rgbFrame->linesize[0],
            rgbFrame->linesize[1],
            rgbFrame->linesize[2],
            rgbFrame->linesize[3],
        };

        _ = sws_scale(swsCtx, srcData, srcStride, 0, height, dstData, dstStride);
    }

    private byte[]? EncodePng()
    {
        int ret = avcodec_send_frame(pngEncCtx, rgbFrame);
        if (ret < 0)
        {
            return null;
        }

        var pkt = av_packet_alloc();
        if (pkt is null)
        {
            return null;
        }

        try
        {
            ret = avcodec_receive_packet(pngEncCtx, pkt);
            if (ret < 0)
            {
                return null;
            }

            var result = new byte[pkt->size];
            Marshal.Copy((IntPtr)pkt->data, result, 0, pkt->size);
            return result;
        }
        finally
        {
            av_packet_free(ref pkt);
        }
    }

    private void FreeEncoder()
    {
        if (pngEncCtx is not null)
        {
            var ctx = pngEncCtx;
            avcodec_free_context(ref ctx);
            pngEncCtx = null;
        }

        if (rgbFrame is not null)
        {
            var f = rgbFrame;
            av_frame_free(ref f);
            rgbFrame = null;
        }

        if (swsCtx is not null)
        {
            sws_freeContext(swsCtx);
            swsCtx = null;
        }
    }
}