namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// CPU-based frame capture: converts a decoded frame to RGB24, rotates it, and encodes as PNG.
/// Caches the SWS context and PNG encoder for repeated captures at the same resolution and rotation.
/// </summary>
[SuppressMessage("", "CA1806:calls av_*", Justification = "OK")]
internal sealed unsafe class FrameCapture : IDisposable
{
    private SwsContext* swsCtx;
    private AVCodecContext* pngEncCtx;
    private AVFrame* rgbFrame;
    private AVFrame* rotatedFrame;
    private int cachedWidth;
    private int cachedHeight;
    private AVPixelFormat cachedSrcFormat;
    private VideoRotation cachedRotation;
    private bool disposed;

    /// <summary>
    /// Captures a frame as PNG bytes, turned the way the picture is displayed.
    /// </summary>
    /// <param name="srcFrame">The decoded frame, in the stream's own orientation.</param>
    /// <param name="rotation">The clockwise rotation the picture is displayed with.</param>
    public byte[]? CaptureFrame(
        AVFrame* srcFrame,
        VideoRotation rotation = VideoRotation.None)
    {
        if (srcFrame is null || srcFrame->width <= 0 || srcFrame->height <= 0)
        {
            return null;
        }

        // Hardware pixel formats (e.g., D3D11) are not supported by libswscale.
        // Transfer the frame to CPU memory first.
        AVFrame* cpuFrame = null;
        var srcFormat = (AVPixelFormat)srcFrame->format;
        if (srcFormat == AVPixelFormat.D3d11)
        {
            cpuFrame = av_frame_alloc();
            if (cpuFrame is null)
            {
                return null;
            }

            if (av_hwframe_transfer_data(cpuFrame, srcFrame, 0) < 0)
            {
                av_frame_free(ref cpuFrame);
                return null;
            }

            srcFrame = cpuFrame;
            srcFormat = (AVPixelFormat)cpuFrame->format;
        }

        try
        {
            var width = srcFrame->width;
            var height = srcFrame->height;

            EnsureContexts(width, height, srcFormat, rotation);

            ConvertToRgb24(srcFrame, height);

            if (rotatedFrame is null)
            {
                return EncodePng(rgbFrame);
            }

            PixelRotator.Rotate(
                (byte*)rgbFrame->data[0],
                rgbFrame->linesize[0],
                width,
                height,
                bytesPerPixel: 3,
                rotation,
                (byte*)rotatedFrame->data[0],
                rotatedFrame->linesize[0]);

            return EncodePng(rotatedFrame);
        }
        finally
        {
            if (cpuFrame is not null)
            {
                av_frame_free(ref cpuFrame);
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
        FreeContexts();
    }

    private void EnsureContexts(
        int width,
        int height,
        AVPixelFormat srcFormat,
        VideoRotation rotation)
    {
        if (swsCtx is not null
            && cachedWidth == width
            && cachedHeight == height
            && cachedSrcFormat == srcFormat
            && cachedRotation == rotation)
        {
            return;
        }

        FreeContexts();

        // A throw between the first successful alloc and the final
        // avcodec_open2 used to leave a partially-initialized state
        // around (e.g. swsCtx allocated but rgbFrame null). The next
        // matching-dimension call would skip re-init and crash on use.
        // Run all allocations under try/catch so any failure resets
        // every field via FreeContexts before propagating.
        AVDictionary* opts = null;
        try
        {
            swsCtx = sws_getContext(
                width,
                height,
                srcFormat,
                width,
                height,
                AVPixelFormat.Rgb24,
                SwsFlags.Bilinear,
                null,
                null,
                null);

            if (swsCtx is null)
            {
                throw new InvalidOperationException("Failed to create SwsContext.");
            }

            rgbFrame = AllocateRgbFrame(width, height);

            // The encoder works on the picture as it is displayed, which a
            // quarter turn makes portrait instead of landscape (or back).
            var (outputWidth, outputHeight) = PixelRotator.GetRotatedSize(width, height, rotation);
            if (rotation != VideoRotation.None)
            {
                rotatedFrame = AllocateRgbFrame(outputWidth, outputHeight);
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

            pngEncCtx->width = outputWidth;
            pngEncCtx->height = outputHeight;
            pngEncCtx->pix_fmt = AVPixelFormat.Rgb24;
            pngEncCtx->time_base = new AVRational { Num = 1, Den = 1 };

            var ret = avcodec_open2(pngEncCtx, encoder, ref opts);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to open PNG encoder");
            }

            cachedWidth = width;
            cachedHeight = height;
            cachedSrcFormat = srcFormat;
            cachedRotation = rotation;
        }
        catch
        {
            FreeContexts();
            throw;
        }
        finally
        {
            av_dict_free(ref opts);
        }
    }

    [SuppressMessage("", "SA1019:Member access symbol '->' should not be followed by a space", Justification = "OK")]
    private void ConvertToRgb24(
        AVFrame* srcFrame,
        int height)
    {
        byte** srcData = stackalloc byte*[]
        {
            (byte*)srcFrame->data[0], (byte*)srcFrame->data[1],
            (byte*)srcFrame->data[2], (byte*)srcFrame->data[3],
        };

        int* srcStride = stackalloc int[]
        {
            srcFrame->linesize[0], srcFrame->linesize[1],
            srcFrame->linesize[2], srcFrame->linesize[3],
        };

        byte** dstData = stackalloc byte*[]
        {
            (byte*)rgbFrame->data[0], (byte*)rgbFrame->data[1],
            (byte*)rgbFrame->data[2], (byte*)rgbFrame->data[3],
        };

        int* dstStride = stackalloc int[]
        {
            rgbFrame->linesize[0], rgbFrame -> linesize[1],
            rgbFrame->linesize[2], rgbFrame -> linesize[3],
        };

        sws_scale(swsCtx, srcData, srcStride, 0, height, dstData, dstStride);
    }

    private static AVFrame* AllocateRgbFrame(
        int width,
        int height)
    {
        var frame = av_frame_alloc();
        if (frame is null)
        {
            throw new InvalidOperationException("Failed to allocate RGB frame.");
        }

        frame->format = (int)AVPixelFormat.Rgb24;
        frame->width = width;
        frame->height = height;

        var ret = av_frame_get_buffer(frame, 0);
        if (ret < 0)
        {
            av_frame_free(ref frame);
            throw new FFmpegException(ret, "Failed to allocate RGB frame buffer");
        }

        return frame;
    }

    private byte[]? EncodePng(AVFrame* frame)
    {
        var ret = avcodec_send_frame(pngEncCtx, frame);
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

    private void FreeContexts()
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

        cachedWidth = 0;
        cachedHeight = 0;
        cachedRotation = VideoRotation.None;
    }
}