// ReSharper disable StringLiteralTypo
namespace AtcSoft.VideoEngine.Demuxing;

/// <summary>
/// Wraps FFmpeg demuxing for RTSP/HTTP video streams with interrupt-based timeout.
/// </summary>
[SuppressMessage("", "CA1806:calls av_*", Justification = "OK")]
internal sealed unsafe partial class Demuxer : IDisposable
{
    private const int OpenTimeoutSeconds = 15;
    private const int ReadTimeoutSeconds = 10;
    private const int AvErrorExit = -1414092869;

    private readonly ILogger logger;
    private readonly Stopwatch timeoutWatch = new();

    private AVFormatContext* fmtCtx;
    private AVPacket* packet;
    private int videoStreamIndex = -1;
    private AVCodecParameters* videoCodecParameters;
    private AVRational videoTimeBase;
    private AVRational videoFrameRate;
    private int audioStreamIndex = -1;
    private AVCodecParameters* audioCodecParameters;
    private AVRational audioTimeBase;
    private bool disposed;

    private int timeoutSeconds = OpenTimeoutSeconds;
    private volatile bool abortRequested;
    private CancellationToken cancellationToken;
    private GCHandle gcHandle;

    [SuppressMessage("CodeQuality", "S1450", Justification = "Field prevents GC of delegate registered with FFmpeg")]
    private AVIOInterruptCB_callback? interruptDelegate;

    internal Demuxer()
        : this(logger: null)
    {
    }

    internal Demuxer(ILogger? logger)
    {
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public int VideoStreamIndex => videoStreamIndex;

    public AVCodecParameters* VideoCodecParameters => videoCodecParameters;

    public AVRational VideoTimeBase => videoTimeBase;

    public AVRational VideoFrameRate => videoFrameRate;

    public AVPacket* CurrentPacket => packet;

    public bool IsVideoPacket => packet->stream_index == videoStreamIndex;

    /// <summary>
    /// Gets a value indicating whether the source has sound.
    /// </summary>
    public bool HasAudio => audioStreamIndex >= 0;

    public AVCodecParameters* AudioCodecParameters => audioCodecParameters;

    public AVRational AudioTimeBase => audioTimeBase;

    public bool IsAudioPacket
        => audioStreamIndex >= 0 && packet->stream_index == audioStreamIndex;

    /// <summary>
    /// Signals the interrupt callback to abort any blocking FFmpeg call immediately.
    /// </summary>
    public void RequestAbort()
    {
        abortRequested = true;
    }

    public void Open(
        Uri streamUri,
        StreamOptions options,
        CancellationToken ct = default)
    {
        cancellationToken = ct;
        fmtCtx = avformat_alloc_context();
        if (fmtCtx is null)
        {
            throw new InvalidOperationException("Failed to allocate AVFormatContext.");
        }

        AVDictionary* dict = null;

        try
        {
            gcHandle = GCHandle.Alloc(this);
            interruptDelegate = new AVIOInterruptCB_callback(InterruptCallback);
            fmtCtx->interrupt_callback.callback = interruptDelegate;
            fmtCtx->interrupt_callback.opaque = (void*)GCHandle.ToIntPtr(gcHandle);

            SetFormatOptions(ref dict, options);

            timeoutSeconds = OpenTimeoutSeconds;
            timeoutWatch.Restart();

            // For local-device sources (dshow / v4l2 / avfoundation) the
            // url is a raw device specifier such as `video=Logitech BRIO`
            // which is not a valid Uri — we ignore streamUri and pass
            // RawDeviceSpec directly. The input format must also be set
            // explicitly because FFmpeg cannot sniff a non-URL string.
            var inputFormatName = options.InputFormatName;
            AVInputFormat* inputFormat = null;
            string url;
            if (inputFormatName is not null)
            {
                inputFormat = av_find_input_format(inputFormatName);
                if (inputFormat is null)
                {
                    throw new InvalidOperationException(
                        $"FFmpeg input format '{inputFormatName}' is not available in this build.");
                }

                if (string.IsNullOrEmpty(options.RawDeviceSpec))
                {
                    throw new InvalidOperationException(
                        $"StreamOptions.RawDeviceSpec must be set when InputFormat is {options.InputFormat}.");
                }

                url = options.RawDeviceSpec;
            }
            else
            {
                url = streamUri.AbsoluteUri;
            }

            var ret = avformat_open_input(ref fmtCtx, url, inputFormat, ref dict);

            if (ret < 0)
            {
                if (ret == AvErrorExit)
                {
                    LogAvformatOpenInputAborted(
                        options.Source,
                        abortRequested,
                        cancellationToken.IsCancellationRequested,
                        timeoutWatch.Elapsed.TotalSeconds,
                        timeoutSeconds);
                }

                // avformat_open_input frees the user-supplied fmtCtx on failure
                // and sets it to null via the ref parameter
                throw new FFmpegException(ret, "Failed to open input");
            }

            timeoutWatch.Restart();

            // No options: the argument is one dictionary per stream, and
            // FFmpeg reads past a single one when the source has a second
            // stream (a camera with sound).
            ret = avformat_find_stream_info(fmtCtx, (AVDictionary**)null);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to find stream info");
            }

            if (fmtCtx->pb is not null)
            {
                fmtCtx->pb->eof_reached = 0;
            }

            FindVideoStream();
            FindAudioStream();

            timeoutSeconds = ReadTimeoutSeconds;
            timeoutWatch.Restart();

            packet = av_packet_alloc();
            if (packet is null)
            {
                throw new InvalidOperationException("Failed to allocate AVPacket.");
            }
        }
        catch
        {
            CleanupOnOpenFailure();
            throw;
        }
        finally
        {
            // Free option dictionaries on every path; FFmpeg may leave unconsumed
            // entries even on success
            av_dict_free(ref dict);
        }
    }

    private void CleanupOnOpenFailure()
    {
        if (packet is not null)
        {
            var p = packet;
            av_packet_free(ref p);
            packet = null;
        }

        if (fmtCtx is not null)
        {
            avformat_close_input(ref fmtCtx);
        }

        if (gcHandle.IsAllocated)
        {
            gcHandle.Free();
        }

        interruptDelegate = null;
    }

    public int ReadPacket()
    {
        timeoutWatch.Restart();
        return av_read_frame(fmtCtx, packet);
    }

    public void UnrefPacket()
    {
        av_packet_unref(packet);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (packet is not null)
        {
            var p = packet;
            av_packet_free(ref p);
            packet = null;
        }

        if (fmtCtx is not null)
        {
            avformat_close_input(ref fmtCtx);
        }

        if (gcHandle.IsAllocated)
        {
            gcHandle.Free();
        }

        interruptDelegate = null;
    }

    private void FindVideoStream()
    {
        for (uint i = 0; i < fmtCtx->nb_streams; i++)
        {
            var stream = fmtCtx->streams[i];
            if (stream->codecpar->codec_type != AVMediaType.Video)
            {
                continue;
            }

            videoStreamIndex = (int)i;
            videoCodecParameters = stream->codecpar;
            videoTimeBase = stream->time_base;
            videoFrameRate = stream->avg_frame_rate.Num > 0
                ? stream->avg_frame_rate
                : stream->r_frame_rate;
            return;
        }

        throw new InvalidOperationException("No video stream found in input.");
    }

    // The first stream with sound, if any. A source without sound is the
    // usual case and no error.
    private void FindAudioStream()
    {
        for (uint i = 0; i < fmtCtx->nb_streams; i++)
        {
            var stream = fmtCtx->streams[i];
            if (stream->codecpar->codec_type != AVMediaType.Audio)
            {
                continue;
            }

            audioStreamIndex = (int)i;
            audioCodecParameters = stream->codecpar;
            audioTimeBase = stream->time_base;
            return;
        }
    }

    private static void SetFormatOptions(
        ref AVDictionary* dict,
        StreamOptions options)
    {
        foreach (var (key, value) in BuildAvOptionPairs(options, FFmpegLoader.IsVersion8OrGreater))
        {
            av_dict_set(ref dict, key, value, DictWriteFlags.None);
        }
    }

    /// <summary>
    /// Pure helper that produces the (key, value) pairs to feed into
    /// the FFmpeg <c>AVDictionary</c>. Split out from
    /// <see cref="SetFormatOptions"/> so the option-selection logic
    /// can be unit-tested without an AVDictionary.
    /// </summary>
    /// <param name="options">User-supplied options.</param>
    /// <param name="isFFmpegV8">
    /// <see langword="true"/> when running against FFmpeg 8+, which
    /// renamed <c>stimeout</c> to <c>timeout</c>.
    /// </param>
    [SuppressMessage("Performance", "CA1822", Justification = "Static helper kept internal for testing")]
    internal static IReadOnlyList<KeyValuePair<string, string>> BuildAvOptionPairs(
        StreamOptions options,
        bool isFFmpegV8)
    {
        ArgumentNullException.ThrowIfNull(options);

        var pairs = new List<KeyValuePair<string, string>>();
        var timeoutUs = (OpenTimeoutSeconds * 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);

        switch (options.InputFormat)
        {
            case InputFormatKind.Auto:
                pairs.Add(new("rtsp_transport", options.RtspTransport));
                pairs.Add(new("probesize", "50000000"));
                pairs.Add(new("analyzeduration", "10000000"));
                pairs.Add(new(isFFmpegV8 ? "timeout" : "stimeout", timeoutUs));
                pairs.Add(new("tls_verify", options.VerifyTlsCertificate ? "1" : "0"));

                if (options.UseLowLatencyMode)
                {
                    pairs.Add(new("fflags", "nobuffer"));
                    pairs.Add(new("flags", "low_delay"));
                }

                break;

            case InputFormatKind.Dshow:
                // dshow's recommended big-buffer setting; without this
                // FFmpeg drops frames on slow (or temporarily blocked)
                // disk-bound recording paths.
                pairs.Add(new("rtbufsize", "100000000"));
                AddIfPresent(pairs, "video_size", options.VideoSize);
                AddIfPresent(pairs, "framerate", options.FrameRate);
                AddIfPresent(pairs, "pixel_format", options.PixelFormat);
                break;

            case InputFormatKind.V4l2:
                AddIfPresent(pairs, "video_size", options.VideoSize);
                AddIfPresent(pairs, "framerate", options.FrameRate);
                AddIfPresent(pairs, "input_format", options.PixelFormat);
                break;

            case InputFormatKind.AVFoundation:
                AddIfPresent(pairs, "video_size", options.VideoSize);
                AddIfPresent(pairs, "framerate", options.FrameRate);
                AddIfPresent(pairs, "pixel_format", options.PixelFormat);
                break;
        }

        return pairs;
    }

    private static void AddIfPresent(
        List<KeyValuePair<string, string>> pairs,
        string key,
        string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            pairs.Add(new(key, value));
        }
    }

    private static int InterruptCallback(void* opaque)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)opaque);
        if (handle.Target is not Demuxer demuxer)
        {
            return 1;
        }

        if (demuxer.abortRequested || demuxer.cancellationToken.IsCancellationRequested)
        {
            return 1;
        }

        return demuxer.timeoutWatch.Elapsed.TotalSeconds > demuxer.timeoutSeconds ? 1 : 0;
    }
}