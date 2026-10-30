namespace AtcSoft.VideoEngine.Decoding;

/// <summary>
/// Wraps FFmpeg video decoding with optional D3D11VA hardware acceleration.
/// </summary>
internal sealed unsafe class VideoDecoder : IDisposable
{
    private const int MaxConsecutiveErrors = 30;

    private AVCodecContext* codecCtx;
    private AVFrame* frame;
    private AVCodec* codec;
    private int consecutiveErrors;
    private bool disposed;
    private bool hwAccelActive;

    // Prevent GC of the delegate while it's registered with FFmpeg.
    [SuppressMessage("CodeQuality", "S1450", Justification = "Field prevents GC of delegate registered with FFmpeg")]
    private AVCodecContext_get_format? getFormatDelegate;

    public AVFrame* CurrentFrame => frame;

    public int Width
        => codecCtx is not null
            ? codecCtx->width
            : 0;

    public int Height
        => codecCtx is not null
            ? codecCtx->height
            : 0;

    public AVPixelFormat PixelFormat
        => codecCtx is not null
            ? codecCtx->pix_fmt
            : AVPixelFormat.None;

    /// <summary>
    /// Gets the layout the pictures have in memory: the decoder's own, or,
    /// when the graphics card decodes, the one its pictures come back in.
    /// </summary>
    public AVPixelFormat MemoryPixelFormat
    {
        get
        {
            if (codecCtx is null)
            {
                return AVPixelFormat.None;
            }

            return hwAccelActive && codecCtx->sw_pix_fmt != AVPixelFormat.None
                ? codecCtx->sw_pix_fmt
                : codecCtx->pix_fmt;
        }
    }

    public string? CodecName => codec is not null
        ? Marshal.PtrToStringAnsi((IntPtr)codec->name)
        : null;

    public bool IsHardwareAccelerated => hwAccelActive;

    public void Open(
        AVCodecParameters* codecpar,
        int threadCount)
        => Open(codecpar, threadCount, hwDeviceCtx: null);

    public void Open(
        AVCodecParameters* codecpar,
        int threadCount,
        AVBufferRef* hwDeviceCtx)
    {
        codec = avcodec_find_decoder(codecpar->codec_id);
        if (codec is null)
        {
            throw new InvalidOperationException(
                $"No decoder found for codec ID {codecpar->codec_id}.");
        }

        codecCtx = avcodec_alloc_context3(codec);
        if (codecCtx is null)
        {
            throw new InvalidOperationException("Failed to allocate AVCodecContext.");
        }

        int ret = avcodec_parameters_to_context(codecCtx, codecpar);
        if (ret < 0)
        {
            throw new FFmpegException(ret, "Failed to copy codec parameters");
        }

        // Only wire the hardware-accelerated path when the codec
        // actually advertises a D3D11VA config. Codecs like mjpeg list
        // CUDA / VAAPI / NVDEC hwaccels but no D3D11VA, and FFmpeg
        // would otherwise probe each registered hwaccel against the
        // wrong device context and emit
        //   "Invalid setup for format <cuda|vaapi>: does not match the
        //   type of the provided device context"
        // before silently falling back to a CPU decoder. Pre-filtering
        // skips the noise and lets us report IsHardwareAccelerated
        // honestly (it stays false when D3D11VA isn't applicable).
        var canUseHwAccel = hwDeviceCtx is not null
            && CodecSupportsD3d11va(codec);

        if (canUseHwAccel)
        {
            // HW-accelerated decoding must be single-threaded to avoid
            // D3D11 device context conflicts (matches Flyleaf behavior).
            codecCtx->thread_count = 1;
            codecCtx->hw_device_ctx = av_buffer_ref(hwDeviceCtx);
            getFormatDelegate = GetHwFormat;
            codecCtx->get_format = getFormatDelegate;
        }
        else
        {
            codecCtx->thread_count = Math.Clamp(threadCount, 0, 16);
        }

        AVDictionary* opts = null;
        ret = avcodec_open2(codecCtx, codec, ref opts);
        if (ret < 0)
        {
            throw new FFmpegException(ret, "Failed to open decoder");
        }

        frame = av_frame_alloc();
        if (frame is null)
        {
            throw new InvalidOperationException("Failed to allocate AVFrame.");
        }

        hwAccelActive = canUseHwAccel;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="codec"/> ships
    /// a hwaccel entry for D3D11VA. Codecs without a matching entry are
    /// always decoded on the CPU on Windows — passing an
    /// <c>hw_device_ctx</c> to them only triggers FFmpeg's probe of the
    /// other registered hwaccels (CUDA / VAAPI / NVDEC), all of which
    /// fail against a D3D11 device.
    /// </summary>
    private static bool CodecSupportsD3d11va(AVCodec* codec)
    {
        if (codec is null)
        {
            return false;
        }

        var i = 0;
        while (true)
        {
            var cfg = avcodec_get_hw_config(codec, i);
            if (cfg is null)
            {
                return false;
            }

            if (cfg->device_type == AVHWDeviceType.D3d11va)
            {
                return true;
            }

            i++;
        }
    }

    private static AVPixelFormat GetHwFormat(
        AVCodecContext* ctx,
        AVPixelFormat* fmts)
    {
        var fmt = fmts;
        while (*fmt != AVPixelFormat.None)
        {
            if (*fmt == AVPixelFormat.D3d11)
            {
                return AVPixelFormat.D3d11;
            }

            fmt++;
        }

        // D3D11 not offered — fall back to first SW format.
        return fmts[0];
    }

    public bool SendPacket(AVPacket* packet)
    {
        int ret = avcodec_send_packet(codecCtx, packet);
        if (ret == 0)
        {
            consecutiveErrors = 0;
            return true;
        }

        if (ret == AVERROR_EAGAIN || ret == AVERROR_EOF)
        {
            return false;
        }

        consecutiveErrors++;
        if (consecutiveErrors > MaxConsecutiveErrors)
        {
            throw new FFmpegException(ret, $"Decoder exceeded {MaxConsecutiveErrors} consecutive errors");
        }

        return false;
    }

    public bool ReceiveFrame()
    {
        int ret = avcodec_receive_frame(codecCtx, frame);
        if (ret == 0)
        {
            if (frame->pts == AV_NOPTS_VALUE)
            {
                frame->pts = frame->best_effort_timestamp;
            }

            return true;
        }

        return false;
    }

    public void Flush()
    {
        if (codecCtx is not null)
        {
            avcodec_flush_buffers(codecCtx);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (frame is not null)
        {
            var f = frame;
            av_frame_free(ref f);
            frame = null;
        }

        if (codecCtx is not null)
        {
            var ctx = codecCtx;
            avcodec_free_context(ref ctx);
            codecCtx = null;
        }

        codec = null;
    }
}