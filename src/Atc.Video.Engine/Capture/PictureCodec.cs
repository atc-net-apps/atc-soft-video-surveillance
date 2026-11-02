namespace Atc.Video.Engine.Capture;

/// <summary>
/// Reads and writes still pictures with FFmpeg, on every platform: a PNG or JPEG into BGRA pixels
/// at the size asked for, and BGRA pixels into a PNG or a JPEG. For the pictures the server makes
/// where GDI+ is not there (Linux, macOS): recording thumbnails and preview sheets.
/// </summary>
[SuppressMessage("", "CA1806:calls av_*", Justification = "OK")]
public static unsafe class PictureCodec
{
    // FFmpeg's FF_QP2LAMBDA: a quantiser in the units the encoders take it.
    private const int QualityToLambda = 118;

    /// <summary>
    /// Reads a PNG or JPEG picture into BGRA pixels.
    /// </summary>
    /// <param name="encoded">The picture's file contents.</param>
    /// <param name="size">The size to make the picture, given its own width and height.</param>
    /// <returns>The picture at the size chosen, or <see langword="null"/> when it cannot be read.</returns>
    public static PictureFrame? Decode(
        byte[] encoded,
        Func<int, int, (int Width, int Height)> size)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(size);

        var codecId = Detect(encoded);
        if (codecId is null)
        {
            return null;
        }

        var decoder = avcodec_find_decoder(codecId.Value);
        if (decoder is null)
        {
            return null;
        }

        var context = avcodec_alloc_context3(decoder);
        var packet = av_packet_alloc();
        var frame = av_frame_alloc();
        try
        {
            if (context is null || packet is null || frame is null)
            {
                return null;
            }

            AVDictionary* options = null;
            var opened = avcodec_open2(context, decoder, ref options);
            av_dict_free(ref options);
            if (opened < 0 || av_new_packet(packet, encoded.Length) < 0)
            {
                return null;
            }

            Marshal.Copy(encoded, 0, (nint)packet->data, encoded.Length);
            if (avcodec_send_packet(context, packet) < 0)
            {
                return null;
            }

            avcodec_send_packet(context, null);
            if (avcodec_receive_frame(context, frame) < 0 || frame->width <= 0 || frame->height <= 0)
            {
                return null;
            }

            var (width, height) = size(frame->width, frame->height);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            using var scaler = new FrameScaler(AVPixelFormat.Bgra);
            var pixels = scaler.ToPixels(frame, width, height, VideoRotation.None);
            return pixels is null ? null : new PictureFrame(TimeSpan.Zero, width, height, pixels);
        }
        finally
        {
            if (frame is not null)
            {
                av_frame_free(ref frame);
            }

            if (packet is not null)
            {
                av_packet_free(ref packet);
            }

            if (context is not null)
            {
                avcodec_free_context(ref context);
            }
        }
    }

    /// <summary>
    /// Writes BGRA pixels as a PNG.
    /// </summary>
    /// <param name="pixels">The pixels, row by row, four bytes each.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">The picture's height.</param>
    /// <returns>The PNG file's contents, or <see langword="null"/> when it cannot be made.</returns>
    public static byte[]? EncodePng(
        byte[] pixels,
        int width,
        int height)
        => Encode(pixels, width, height, "png", AVPixelFormat.Rgb24, quality: null);

    /// <summary>
    /// Writes BGRA pixels as a JPEG.
    /// </summary>
    /// <param name="pixels">The pixels, row by row, four bytes each.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">The picture's height.</param>
    /// <param name="quality">The quality, 0 (smallest file) to 100 (best picture).</param>
    /// <returns>The JPEG file's contents, or <see langword="null"/> when it cannot be made.</returns>
    public static byte[]? EncodeJpeg(
        byte[] pixels,
        int width,
        int height,
        int quality)
        => Encode(pixels, width, height, "mjpeg", AVPixelFormat.Yuvj420p, Math.Clamp(quality, 0, 100));

    private static AVCodecID? Detect(byte[] encoded)
    {
        if (encoded.Length >= 8 && encoded[0] == 0x89 && encoded[1] == 0x50 && encoded[2] == 0x4E && encoded[3] == 0x47)
        {
            return AVCodecID.Png;
        }

        if (encoded.Length >= 3 && encoded[0] == 0xFF && encoded[1] == 0xD8 && encoded[2] == 0xFF)
        {
            return AVCodecID.Mjpeg;
        }

        return null;
    }

    private static byte[]? Encode(
        byte[] pixels,
        int width,
        int height,
        string encoderName,
        AVPixelFormat encoderFormat,
        int? quality)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length < width * height * 4)
        {
            throw new ArgumentException("Fewer pixels than the size says.", nameof(pixels));
        }

        var encoder = avcodec_find_encoder_by_name(encoderName);
        if (encoder is null)
        {
            return null;
        }

        var context = avcodec_alloc_context3(encoder);
        var frame = av_frame_alloc();
        var packet = av_packet_alloc();
        SwsContext* converter = null;
        try
        {
            if (context is null || frame is null || packet is null)
            {
                return null;
            }

            context->width = width;
            context->height = height;
            context->pix_fmt = encoderFormat;
            context->time_base = new AVRational { Num = 1, Den = 1 };
            if (quality is { } q)
            {
                // 100 is the encoder's finest quantiser (2), 0 its coarsest (31).
                var quantiser = 31 - (int)Math.Round(q * 29 / 100.0);
                context->flags |= CodecFlags.Qscale;
                context->global_quality = quantiser * QualityToLambda;
                context->color_range = AVColorRange.Jpeg;
            }

            AVDictionary* options = null;
            var opened = avcodec_open2(context, encoder, ref options);
            av_dict_free(ref options);
            if (opened < 0)
            {
                return null;
            }

            frame->format = (int)encoderFormat;
            frame->width = width;
            frame->height = height;
            if (quality is not null)
            {
                frame->quality = context->global_quality;
                frame->color_range = AVColorRange.Jpeg;
            }

            if (av_frame_get_buffer(frame, 0) < 0)
            {
                return null;
            }

            converter = sws_getContext(width, height, AVPixelFormat.Bgra, width, height, encoderFormat, SwsFlags.Bilinear, null, null, null);
            if (converter is null)
            {
                return null;
            }

            fixed (byte* source = pixels)
            {
                var sourceData = stackalloc byte*[4] { source, null, null, null };
                var sourceStride = stackalloc int[4] { width * 4, 0, 0, 0 };
                var targetData = stackalloc byte*[4] { (byte*)frame->data[0], (byte*)frame->data[1], (byte*)frame->data[2], (byte*)frame->data[3] };
                var targetStride = stackalloc int[4] { frame->linesize[0], frame->linesize[1], frame->linesize[2], frame->linesize[3] };
                sws_scale(converter, sourceData, sourceStride, 0, height, targetData, targetStride);
            }

            if (avcodec_send_frame(context, frame) < 0)
            {
                return null;
            }

            avcodec_send_frame(context, null);
            if (avcodec_receive_packet(context, packet) < 0 || packet->size <= 0)
            {
                return null;
            }

            var result = new byte[packet->size];
            Marshal.Copy((nint)packet->data, result, 0, packet->size);
            return result;
        }
        finally
        {
            if (converter is not null)
            {
                sws_freeContext(converter);
            }

            if (packet is not null)
            {
                av_packet_free(ref packet);
            }

            if (frame is not null)
            {
                av_frame_free(ref frame);
            }

            if (context is not null)
            {
                avcodec_free_context(ref context);
            }
        }
    }
}