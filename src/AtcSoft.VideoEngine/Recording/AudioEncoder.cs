// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// Encodes a source's sound to AAC so it can be recorded next to the
/// picture. A microphone delivers uncompressed sound (PCM), which MP4 does
/// not take and browsers do not play; AAC goes into every container.
/// </summary>
[SuppressMessage("", "CA1806:calls av_*", Justification = "OK")]
internal sealed unsafe class AudioEncoder : IDisposable
{
    private const int BitRate = 128_000;
    private const int MaxChannels = 2;

    // Sound whose time has moved this far from where the samples counted
    // so far put it (the microphone dropped out) starts counting afresh.
    private const double ResyncSeconds = 0.5;

    private AVCodecContext* decoderCtx;
    private AVCodecContext* encoderCtx;
    private AVCodecParameters* codecParameters;
    private SwrContext* swrCtx;
    private AVAudioFifo* fifo;
    private AVFrame* decoded;
    private AVFrame* toEncode;
    private AVPacket* packet;
    private long nextPts = AV_NOPTS_VALUE;
    private bool disposed;

    /// <summary>
    /// Gets the time base of the packets the encoder hands out: one sample.
    /// </summary>
    public AVRational TimeBase { get; private set; }

    /// <summary>
    /// Gets the parameters of the encoded stream, for the recorder to open a file with.
    /// </summary>
    public AVCodecParameters* CodecParameters => codecParameters;

    public void Open(AVCodecParameters* source)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (encoderCtx is not null)
        {
            throw new InvalidOperationException("The encoder is already open.");
        }

        try
        {
            OpenDecoder(source);
            OpenEncoder();

            codecParameters = avcodec_parameters_alloc();
            var ret = avcodec_parameters_from_context(codecParameters, encoderCtx);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to read the sound encoder's parameters");
            }

            ret = swr_alloc_set_opts2(
                ref swrCtx,
                &encoderCtx->ch_layout,
                encoderCtx->sample_fmt,
                encoderCtx->sample_rate,
                &decoderCtx->ch_layout,
                decoderCtx->sample_fmt,
                decoderCtx->sample_rate,
                0,
                null);
            if (ret < 0 || swrCtx is null || swr_init(swrCtx) < 0)
            {
                throw new InvalidOperationException("The source's sound cannot be converted for the encoder.");
            }

            fifo = av_audio_fifo_alloc(encoderCtx->sample_fmt, encoderCtx->ch_layout.nb_channels, encoderCtx->frame_size * 4);
            decoded = av_frame_alloc();
            packet = av_packet_alloc();
            toEncode = AllocateFrame(encoderCtx->frame_size);
            TimeBase = new AVRational { Num = 1, Den = encoderCtx->sample_rate };
        }
        catch
        {
            Free();
            throw;
        }
    }

    /// <summary>
    /// Takes one packet of the source's sound and hands every encoded
    /// packet that is ready to <paramref name="onPacket"/>.
    /// </summary>
    /// <param name="sourcePacket">The packet as read from the source.</param>
    /// <param name="sourceTimeBase">The time base of the packet's time.</param>
    /// <param name="onPacket">Receives the encoded packets.</param>
    public void Encode(
        AVPacket* sourcePacket,
        AVRational sourceTimeBase,
        EncodedPacketHandler onPacket)
    {
        ArgumentNullException.ThrowIfNull(onPacket);

        if (encoderCtx is null || sourcePacket is null)
        {
            return;
        }

        if (avcodec_send_packet(decoderCtx, sourcePacket) < 0)
        {
            return;
        }

        while (avcodec_receive_frame(decoderCtx, decoded) >= 0)
        {
            var sourcePts = decoded->pts != AV_NOPTS_VALUE ? decoded->pts : sourcePacket->pts;
            if (sourcePts != AV_NOPTS_VALUE)
            {
                AlignWithSource(av_rescale_q(sourcePts, sourceTimeBase, TimeBase));
            }

            Buffer(decoded);
            av_frame_unref(decoded);
            EncodeBuffered(onPacket, flush: false);
        }
    }

    /// <summary>
    /// Hands out what the encoder still holds. The encoder takes no more
    /// sound afterwards.
    /// </summary>
    public void Flush(EncodedPacketHandler onPacket)
    {
        ArgumentNullException.ThrowIfNull(onPacket);

        if (encoderCtx is null)
        {
            return;
        }

        EncodeBuffered(onPacket, flush: true);

        if (avcodec_send_frame(encoderCtx, null) >= 0)
        {
            ReceivePackets(onPacket);
        }
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

    private void OpenDecoder(AVCodecParameters* source)
    {
        var decoder = avcodec_find_decoder(source->codec_id);
        if (decoder is null)
        {
            throw new InvalidOperationException($"No decoder found for the source's sound ({source->codec_id}).");
        }

        decoderCtx = avcodec_alloc_context3(decoder);
        var ret = avcodec_parameters_to_context(decoderCtx, source);
        if (ret < 0)
        {
            throw new FFmpegException(ret, "Failed to copy the sound's codec parameters");
        }

        AVDictionary* opts = null;
        ret = avcodec_open2(decoderCtx, decoder, ref opts);
        av_dict_free(ref opts);
        if (ret < 0)
        {
            throw new FFmpegException(ret, "Failed to open the decoder for the source's sound");
        }
    }

    private void OpenEncoder()
    {
        var encoder = avcodec_find_encoder(AVCodecID.Aac);
        if (encoder is null)
        {
            throw new InvalidOperationException("The FFmpeg libraries have no AAC encoder; sound cannot be recorded.");
        }

        encoderCtx = avcodec_alloc_context3(encoder);
        encoderCtx->sample_fmt = AVSampleFormat.Fltp;
        encoderCtx->sample_rate = decoderCtx->sample_rate;
        encoderCtx->bit_rate = BitRate;
        encoderCtx->time_base = new AVRational { Num = 1, Den = decoderCtx->sample_rate };
        av_channel_layout_default(&encoderCtx->ch_layout, Math.Clamp(decoderCtx->ch_layout.nb_channels, 1, MaxChannels));

        // MP4 and Matroska keep the stream's parameters in the file header.
        encoderCtx->flags |= CodecFlags.GlobalHeader;

        AVDictionary* opts = null;
        var ret = avcodec_open2(encoderCtx, encoder, ref opts);
        av_dict_free(ref opts);
        if (ret < 0)
        {
            throw new FFmpegException(ret, "Failed to open the AAC encoder");
        }
    }

    // The time of the next encoded packet follows from the samples counted.
    // It is set from the source at the start, and again when the source's
    // clock has moved away from the count.
    private void AlignWithSource(long sourcePts)
    {
        var expected = nextPts == AV_NOPTS_VALUE
            ? AV_NOPTS_VALUE
            : nextPts + av_audio_fifo_size(fifo);

        if (expected != AV_NOPTS_VALUE &&
            Math.Abs(sourcePts - expected) <= ResyncSeconds * encoderCtx->sample_rate)
        {
            return;
        }

        av_audio_fifo_reset(fifo);
        nextPts = sourcePts;
    }

    private void Buffer(AVFrame* frame)
    {
        var capacity = swr_get_out_samples(swrCtx, frame->nb_samples);
        if (capacity <= 0)
        {
            return;
        }

        var converted = AllocateFrame(capacity);
        try
        {
            var samples = swr_convert(
                swrCtx,
                converted->extended_data,
                capacity,
                frame->extended_data,
                frame->nb_samples);

            if (samples > 0)
            {
                av_audio_fifo_write(fifo, (void**)converted->extended_data, samples);
            }
        }
        finally
        {
            av_frame_free(ref converted);
        }
    }

    private void EncodeBuffered(
        EncodedPacketHandler onPacket,
        bool flush)
    {
        var frameSize = encoderCtx->frame_size;

        while (true)
        {
            var available = av_audio_fifo_size(fifo);
            if (available < frameSize && !(flush && available > 0))
            {
                return;
            }

            var samples = Math.Min(available, frameSize);
            if (av_frame_make_writable(toEncode) < 0)
            {
                return;
            }

            av_audio_fifo_read(fifo, (void**)toEncode->extended_data, samples);
            toEncode->nb_samples = samples;
            toEncode->pts = nextPts;
            nextPts += samples;

            if (avcodec_send_frame(encoderCtx, toEncode) < 0)
            {
                return;
            }

            ReceivePackets(onPacket);
        }
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

    private AVFrame* AllocateFrame(int samples)
    {
        var frame = av_frame_alloc();
        if (frame is null)
        {
            throw new InvalidOperationException("Failed to allocate a frame for sound.");
        }

        frame->format = (int)encoderCtx->sample_fmt;
        frame->sample_rate = encoderCtx->sample_rate;
        frame->nb_samples = samples;
        av_channel_layout_copy(&frame->ch_layout, &encoderCtx->ch_layout);

        var ret = av_frame_get_buffer(frame, 0);
        if (ret < 0)
        {
            av_frame_free(ref frame);
            throw new FFmpegException(ret, "Failed to allocate the buffer for sound");
        }

        return frame;
    }

    private void Free()
    {
        if (packet is not null)
        {
            av_packet_free(ref packet);
        }

        if (toEncode is not null)
        {
            av_frame_free(ref toEncode);
        }

        if (decoded is not null)
        {
            av_frame_free(ref decoded);
        }

        if (fifo is not null)
        {
            av_audio_fifo_free(fifo);
            fifo = null;
        }

        if (swrCtx is not null)
        {
            swr_free(ref swrCtx);
        }

        if (codecParameters is not null)
        {
            avcodec_parameters_free(ref codecParameters);
        }

        if (encoderCtx is not null)
        {
            avcodec_free_context(ref encoderCtx);
        }

        if (decoderCtx is not null)
        {
            avcodec_free_context(ref decoderCtx);
        }
    }
}