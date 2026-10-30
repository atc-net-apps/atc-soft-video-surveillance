// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// Encodes uncompressed pictures to H.264 so they can be recorded. A webcam
/// delivers raw pictures (YUY2, NV12), which no recording format takes and
/// which would fill a disk at tens of megabytes a second if one did.
/// </summary>
[SuppressMessage("", "CA1806:calls av_*", Justification = "OK")]
internal sealed unsafe class FrameEncoder : IDisposable
{
    // A key frame this often: where a player can start, and the most a new
    // file can lose when no key frame was asked for.
    private const int KeyFrameIntervalSeconds = 2;

    private const int DefaultFramesPerSecond = 30;

    // The encoders to try, best first, with the colour layout each takes.
    // libx264 is not in every FFmpeg build (it makes the build GPL);
    // h264_mf is Windows' own encoder and is in every Windows build.
    private static readonly (string Name, AVPixelFormat Format)[] Candidates =
    [
        ("libx264", AVPixelFormat.Yuv420p),
        ("h264_mf", AVPixelFormat.Nv12),
        ("libopenh264", AVPixelFormat.Yuv420p),
    ];

    private AVCodecContext* encoderCtx;
    private AVCodecParameters* codecParameters;
    private SwsContext* swsCtx;
    private AVFrame* converted;
    private AVPacket* packet;
    private int sourceWidth;
    private int sourceHeight;
    private AVPixelFormat sourceFormat;
    private int encodedWidth;
    private int encodedHeight;
    private AVPixelFormat encodedFormat;
    private long frameDuration;
    private long lastPts = AV_NOPTS_VALUE;
    private long lastKeyFramePts = AV_NOPTS_VALUE;
    private bool keyFrameRequested;
    private bool disposed;

    /// <summary>
    /// Gets the time base of the packets the encoder hands out.
    /// </summary>
    public AVRational TimeBase { get; } = new() { Num = 1, Den = 90_000 };

    /// <summary>
    /// Gets the parameters of the encoded stream, for the recorder to open a file with.
    /// </summary>
    public AVCodecParameters* CodecParameters => codecParameters;

    /// <summary>
    /// Gets the name of the encoder in use.
    /// </summary>
    public string? EncoderName { get; private set; }

    /// <summary>
    /// Tells whether a stream has to be encoded before it can be recorded.
    /// </summary>
    public static bool IsNeededFor(AVCodecID codec)
        => codec == AVCodecID.Rawvideo;

    /// <summary>
    /// Tells whether a stream has to be encoded for its recording.
    /// </summary>
    /// <param name="codec">What the source sends.</param>
    /// <param name="transcodeToH264">Whether recordings are wanted as H.264 whatever the source sends.</param>
    public static bool IsNeededFor(
        AVCodecID codec,
        bool transcodeToH264)
        => IsNeededFor(codec) || (transcodeToH264 && codec != AVCodecID.H264);

    public void Open(
        int width,
        int height,
        AVPixelFormat format,
        int framesPerSecond)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (encoderCtx is not null)
        {
            throw new InvalidOperationException("The encoder is already open.");
        }

        if (framesPerSecond <= 0)
        {
            framesPerSecond = DefaultFramesPerSecond;
        }

        // H.264 with 4:2:0 colour needs even sides.
        encodedWidth = width & ~1;
        encodedHeight = height & ~1;

        try
        {
            encodedFormat = OpenFirstAvailable(encodedWidth, encodedHeight, framesPerSecond);

            codecParameters = avcodec_parameters_alloc();
            var ret = avcodec_parameters_from_context(codecParameters, encoderCtx);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to read the encoder's parameters");
            }

            if (!TryOpenConverter(width, height, format))
            {
                throw new InvalidOperationException($"Pictures in {format} cannot be converted for the encoder.");
            }

            converted = av_frame_alloc();
            converted->format = (int)encodedFormat;
            converted->width = encodedWidth;
            converted->height = encodedHeight;
            ret = av_frame_get_buffer(converted, 0);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to allocate the picture for the encoder");
            }

            packet = av_packet_alloc();
            frameDuration = TimeBase.Den / framesPerSecond;
        }
        catch
        {
            Free();
            throw;
        }
    }

    /// <summary>
    /// Makes the next picture a key frame, so a file that starts with it
    /// loses nothing.
    /// </summary>
    public void RequestKeyFrame()
        => keyFrameRequested = true;

    /// <summary>
    /// Encodes one picture and hands every packet that is ready to
    /// <paramref name="onPacket"/>.
    /// </summary>
    /// <param name="frame">The picture, in the size and format the encoder was opened with.</param>
    /// <param name="sourceTimeBase">The time base of the picture's time.</param>
    /// <param name="onPacket">Receives the encoded packets.</param>
    public void Encode(
        AVFrame* frame,
        AVRational sourceTimeBase,
        EncodedPacketHandler onPacket)
    {
        ArgumentNullException.ThrowIfNull(onPacket);

        if (encoderCtx is null || frame is null || frame->width <= 0 || frame->height <= 0)
        {
            return;
        }

        // The pictures can arrive in another layout than the encoder was
        // opened for: one decoded on the graphics card is handed over in
        // the card's layout, which is only known when the first arrives.
        // The encoder stays as it is; only the conversion in front of it
        // is set up again.
        if ((frame->width != sourceWidth ||
             frame->height != sourceHeight ||
             frame->format != (int)sourceFormat) &&
            !TryOpenConverter(frame->width, frame->height, (AVPixelFormat)frame->format))
        {
            return;
        }

        // A webcam now and then delivers two pictures with the same time,
        // and an encoder refuses a time that does not move on.
        var pts = GetPts(frame, sourceTimeBase);
        if (lastPts != AV_NOPTS_VALUE && pts <= lastPts)
        {
            return;
        }

        if (av_frame_make_writable(converted) < 0)
        {
            return;
        }

        Convert(frame);

        // Key frames go by the clock, not by counting pictures: a webcam in
        // poor light delivers fewer pictures than it promised, and a live
        // stream is cut into segments at key frames.
        var keyFrame = keyFrameRequested ||
                       lastKeyFramePts == AV_NOPTS_VALUE ||
                       pts - lastKeyFramePts >= (long)KeyFrameIntervalSeconds * TimeBase.Den;
        if (keyFrame)
        {
            lastKeyFramePts = pts;
        }

        converted->pts = pts;
        converted->pict_type = keyFrame ? AVPictureType.I : AVPictureType.None;
        keyFrameRequested = false;
        lastPts = pts;

        if (avcodec_send_frame(encoderCtx, converted) < 0)
        {
            return;
        }

        ReceivePackets(onPacket);
    }

    /// <summary>
    /// Hands out the packets the encoder still holds. The encoder takes no
    /// more pictures afterwards.
    /// </summary>
    public void Flush(EncodedPacketHandler onPacket)
    {
        ArgumentNullException.ThrowIfNull(onPacket);

        if (encoderCtx is null)
        {
            return;
        }

        if (avcodec_send_frame(encoderCtx, null) < 0)
        {
            return;
        }

        ReceivePackets(onPacket);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Free();
    }

    private bool TryOpenConverter(
        int width,
        int height,
        AVPixelFormat format)
    {
        if (swsCtx is not null)
        {
            sws_freeContext(swsCtx);
            swsCtx = null;
        }

        swsCtx = sws_getContext(
            width,
            height,
            format,
            encodedWidth,
            encodedHeight,
            encodedFormat,
            SwsFlags.Bilinear,
            null,
            null,
            null);

        if (swsCtx is null)
        {
            sourceWidth = 0;
            sourceHeight = 0;
            return false;
        }

        sourceWidth = width;
        sourceHeight = height;
        sourceFormat = format;
        return true;
    }

    private AVPixelFormat OpenFirstAvailable(
        int width,
        int height,
        int framesPerSecond)
    {
        foreach (var (name, format) in Candidates)
        {
            var codec = avcodec_find_encoder_by_name(name);
            if (codec is not null && TryOpen(codec, name, format, width, height, framesPerSecond))
            {
                return format;
            }
        }

        // Whatever else the libraries offer for H.264.
        var fallback = avcodec_find_encoder(AVCodecID.H264);
        if (fallback is not null)
        {
            var name = Marshal.PtrToStringAnsi((IntPtr)fallback->name) ?? "h264";
            if (TryOpen(fallback, name, AVPixelFormat.Yuv420p, width, height, framesPerSecond))
            {
                return AVPixelFormat.Yuv420p;
            }
        }

        throw new InvalidOperationException(
            "The FFmpeg libraries have no usable H.264 encoder (libx264, h264_mf or libopenh264). " +
            "A camera that delivers uncompressed video cannot be recorded without one.");
    }

    private bool TryOpen(
        AVCodec* codec,
        string name,
        AVPixelFormat format,
        int width,
        int height,
        int framesPerSecond)
    {
        var ctx = avcodec_alloc_context3(codec);
        if (ctx is null)
        {
            return false;
        }

        ctx->width = width;
        ctx->height = height;
        ctx->pix_fmt = format;
        ctx->time_base = TimeBase;
        ctx->framerate = new AVRational { Num = framesPerSecond, Den = 1 };

        // The key frames are asked for by the clock (see Encode). This is
        // only the most pictures the encoder may let pass without one.
        ctx->gop_size = framesPerSecond * KeyFrameIntervalSeconds * 2;

        // No pictures that refer to later ones: packets then come out in
        // the order the pictures went in, which the recorder relies on.
        ctx->max_b_frames = 0;

        // MP4 and Matroska keep the stream's parameters in the file header,
        // not in front of every key frame.
        ctx->flags |= CodecFlags.GlobalHeader;

        AVDictionary* opts = null;
        try
        {
            if (string.Equals(name, "libx264", StringComparison.Ordinal))
            {
                // Recording runs on the thread that reads the camera, so the
                // encoder has to keep up with it: a fast preset, no look-ahead.
                av_dict_set(ref opts, "preset", "veryfast", DictWriteFlags.None);
                av_dict_set(ref opts, "tune", "zerolatency", DictWriteFlags.None);
                av_dict_set(ref opts, "crf", "23", DictWriteFlags.None);

                // No key frames of the encoder's own at a change of scene:
                // they would move the regular ones away from the clock.
                av_dict_set(ref opts, "sc_threshold", "0", DictWriteFlags.None);
            }
            else
            {
                // About 0.1 bit per pixel: 2.8 Mbit/s for 720p at 30 pictures a second.
                ctx->bit_rate = (long)width * height * framesPerSecond / 10;
            }

            if (avcodec_open2(ctx, codec, ref opts) < 0)
            {
                avcodec_free_context(ref ctx);
                return false;
            }
        }
        finally
        {
            av_dict_free(ref opts);
        }

        encoderCtx = ctx;
        EncoderName = name;
        return true;
    }

    private long GetPts(
        AVFrame* frame,
        AVRational sourceTimeBase)
    {
        var sourcePts = frame->pts != AV_NOPTS_VALUE ? frame->pts : frame->best_effort_timestamp;
        if (sourcePts == AV_NOPTS_VALUE)
        {
            // No time on the picture: count on from the last one.
            return lastPts == AV_NOPTS_VALUE ? 0 : lastPts + frameDuration;
        }

        // The camera's own clock is kept: the recorder sets each file's
        // start, and sound recorded next to the picture is on the same clock.
        return av_rescale_q(sourcePts, sourceTimeBase, TimeBase);
    }

    private void Convert(AVFrame* frame)
    {
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
            (byte*)converted->data[0], (byte*)converted->data[1],
            (byte*)converted->data[2], (byte*)converted->data[3],
        };

        int* dstStride = stackalloc int[]
        {
            converted->linesize[0], converted->linesize[1],
            converted->linesize[2], converted->linesize[3],
        };

        sws_scale(swsCtx, srcData, srcStride, 0, sourceHeight, dstData, dstStride);
    }

    private void ReceivePackets(EncodedPacketHandler onPacket)
    {
        while (avcodec_receive_packet(encoderCtx, packet) >= 0)
        {
            try
            {
                onPacket(packet);
            }
            finally
            {
                av_packet_unref(packet);
            }
        }
    }

    private void Free()
    {
        if (packet is not null)
        {
            av_packet_free(ref packet);
        }

        if (converted is not null)
        {
            av_frame_free(ref converted);
        }

        if (swsCtx is not null)
        {
            sws_freeContext(swsCtx);
            swsCtx = null;
        }

        if (codecParameters is not null)
        {
            avcodec_parameters_free(ref codecParameters);
        }

        if (encoderCtx is not null)
        {
            avcodec_free_context(ref encoderCtx);
        }
    }
}