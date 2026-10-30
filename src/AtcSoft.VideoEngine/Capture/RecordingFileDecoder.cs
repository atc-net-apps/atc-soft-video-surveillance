namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// Decodes the video of a recording file from its start, frame by frame, as fast as it decodes:
/// every frame is decoded (a compressed picture needs the ones before it), and the caller takes the
/// ones it wants. Shared by the readers that turn the frames into pictures
/// (<see cref="GreyFrameFileReader"/>, <see cref="PictureFileReader"/>). Not thread-safe.
/// </summary>
[SuppressMessage("", "CA1806:calls av_*", Justification = "OK")]
internal sealed unsafe class RecordingFileDecoder : IDisposable
{
    private readonly VideoDecoder decoder = new();
    private AVFormatContext* formatContext;
    private AVPacket* packet;
    private int streamIndex = -1;
    private AVRational timeBase;
    private long startPts;
    private bool draining;
    private bool finished;
    private bool disposed;

    private RecordingFileDecoder()
    {
    }

    /// <summary>
    /// Gets how long the recording is, or <see cref="TimeSpan.Zero"/> when the file does not say.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Opens a recording file.
    /// </summary>
    /// <param name="filePath">The file.</param>
    /// <param name="decoderThreads">How many threads decode; 0 for one per processor.</param>
    /// <returns>The decoder.</returns>
    /// <exception cref="FFmpegException">The file cannot be opened or decoded.</exception>
    public static RecordingFileDecoder Open(
        string filePath,
        int decoderThreads)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var opened = new RecordingFileDecoder();
        try
        {
            opened.OpenFile(filePath, decoderThreads > 0 ? decoderThreads : Environment.ProcessorCount);
            return opened;
        }
        catch
        {
            opened.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes on to the next frame at or after a place in the recording that the caller takes.
    /// </summary>
    /// <param name="notBefore">The earliest place wanted; frames before it are decoded and passed over.</param>
    /// <param name="take">
    /// Turns a frame into what the caller wants; <see langword="null"/> passes the frame over. The
    /// frame is the decoder's own and is only valid during the call.
    /// </param>
    /// <typeparam name="T">What the caller makes of a frame.</typeparam>
    /// <returns>What <paramref name="take"/> made, or <see langword="null"/> at the end of the recording.</returns>
    public T? ReadNext<T>(
        TimeSpan notBefore,
        Func<IntPtr, TimeSpan, T?> take)
        where T : class
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(take);

        while (!finished)
        {
            if (decoder.ReceiveFrame())
            {
                var frame = decoder.CurrentFrame;
                var position = PositionOf(frame->pts);
                if (position >= notBefore && take((IntPtr)frame, position) is { } taken)
                {
                    return taken;
                }

                continue;
            }

            if (draining)
            {
                finished = true;
                break;
            }

            if (av_read_frame(formatContext, packet) < 0)
            {
                // The end: what the decoder still holds comes out after an empty packet.
                decoder.SendPacket(null);
                draining = true;
                continue;
            }

            try
            {
                if (packet->stream_index == streamIndex)
                {
                    decoder.SendPacket(packet);
                }
            }
            finally
            {
                av_packet_unref(packet);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        decoder.Dispose();

        if (packet is not null)
        {
            var p = packet;
            av_packet_free(ref p);
            packet = null;
        }

        if (formatContext is not null)
        {
            avformat_close_input(ref formatContext);
        }
    }

    private void OpenFile(
        string filePath,
        int decoderThreads)
    {
        AVDictionary* options = null;
        try
        {
            var ret = avformat_open_input(ref formatContext, filePath, null, ref options);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to open the recording");
            }

            // Not "ref options": this argument is one dictionary per stream.
            ret = avformat_find_stream_info(formatContext, (AVDictionary**)null);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to read the recording's streams");
            }
        }
        finally
        {
            av_dict_free(ref options);
        }

        for (uint i = 0; i < formatContext->nb_streams; i++)
        {
            var stream = formatContext->streams[i];
            if (stream->codecpar->codec_type != AVMediaType.Video)
            {
                continue;
            }

            streamIndex = (int)i;
            timeBase = stream->time_base;
            startPts = stream->start_time == AV_NOPTS_VALUE ? 0 : stream->start_time;
            decoder.Open(stream->codecpar, decoderThreads);
            break;
        }

        if (streamIndex < 0)
        {
            throw new InvalidOperationException("The recording has no video.");
        }

        Duration = formatContext->duration > 0 ? TimeSpan.FromTicks(formatContext->duration * 10) : TimeSpan.Zero;
        packet = av_packet_alloc();
        if (packet is null)
        {
            throw new InvalidOperationException("Failed to allocate AVPacket.");
        }
    }

    private TimeSpan PositionOf(long pts)
    {
        if (pts == AV_NOPTS_VALUE || timeBase.Den == 0)
        {
            return TimeSpan.Zero;
        }

        var seconds = (pts - startPts) * (double)timeBase.Num / timeBase.Den;
        return seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
    }
}