// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
namespace Atc.Video.Engine.Recording;

/// <summary>
/// Records a video stream by remuxing (packet copy, no transcoding) to a container file.
/// </summary>
[SuppressMessage("", "CA1806:calls av_*", Justification = "OK")]
internal sealed unsafe class Remuxer : IDisposable
{
    // Bounded queue capacity sized so a slow disk hiccup of a few seconds
    // (e.g. a cleanup pass thrashing the same drive) doesn't drop frames at
    // 30 fps. When the queue fills, the producer side drops the oldest
    // packet — keeping the freshest history when the disk is permanently
    // behind.
    private const int WriteQueueCapacity = 1000;

    // How far the source's clock may go back or forward between two pictures before it counts as a
    // jump that is re-based (see Rebase).
    private const long JumpBackMicroseconds = 5_000_000;
    private const long JumpForwardMicroseconds = 60_000_000;

    // Two locks, so the thread that reads the camera never waits for the disk: stateLock guards
    // what WritePacket needs (the file being open, its clock, the queue), ioLock guards the
    // muxer's writes. The writer takes only ioLock for each packet; opening, switching and
    // closing take both, stateLock first, so no packet lands in the wrong file.
    private readonly Lock stateLock = new();
    private readonly Lock ioLock = new();

    // BlockingCollection (not Channel) because the surrounding class is
    // `unsafe` and cannot host `await`. The writer thread waits for
    // packetQueued instead of an async wait.
    private readonly BlockingCollection<nint> writeQueue =
        new(new ConcurrentQueue<nint>(), boundedCapacity: WriteQueueCapacity);

    // Wakes the writer when a packet was queued, and when it has to stop.
    private readonly AutoResetEvent packetQueued = new(initialState: false);

    // Where a file's clock starts is kept in a unit both streams share, so
    // sound keeps its place against the picture.
    private static readonly AVRational Microseconds = new() { Num = 1, Den = 1_000_000 };

    private AVFormatContext* outputCtx;
    private AVRational outputTimeBase;
    private AVRational audioOutputTimeBase;
    private bool hasAudio;
    private long originMicroseconds = AV_NOPTS_VALUE;
    private long lastDts = AV_NOPTS_VALUE;
    private long lastAudioDts = AV_NOPTS_VALUE;

    // A camera's clock that jumps (a resync, a wrap of the MPEG-TS clock every 26.5 h or of RTP's every
    // 13.25 h) is re-based: the file goes on one picture after the last, instead of every packet
    // after the jump being squeezed one tick apart (back) or leaving a hole (forward).
    private long shiftMicroseconds;
    private long lastSourceDtsMicroseconds = AV_NOPTS_VALUE;
    private long lastStepMicroseconds = 40_000;
    private bool receivedKeyframe;
    private string? outputPath;

    // The stream's parameters (SPS/PPS), to put in front of every key frame
    // of a live stream. Null when the packets carry them already.
    private byte[]? liveParameterSets;
    private bool disposed;

    private volatile bool stopWriter;

    // Why the file can take no more packets (a full disk, a broken output); null while it can.
    private volatile string? writeError;

    private Thread? writerThread;

    // A switch to the next file that waits for a key frame (see SwitchTo). Guarded by stateLock.
    private PendingSwitch? pendingSwitch;

    public bool IsOpen => outputCtx is not null;

    /// <summary>
    /// Gets why the current file can take no more packets (a full disk, a broken output), or
    /// <see langword="null"/> while it can. Packets after such an error are dropped.
    /// </summary>
    public string? WriteError => writeError;

    /// <summary>
    /// Gets how many jumps in the source's clock this recorder has re-based.
    /// </summary>
    public int ClockJumps { get; private set; }

    public void Open(
        string outputPath,
        AVCodecParameters* codecpar,
        AVRational inputTimeBase,
        int rotationDegrees = 0,
        AVCodecParameters* audioCodecpar = null)
    {
        lock (stateLock)
        {
            pendingSwitch?.Dispose();
            pendingSwitch = null;
            lock (ioLock)
            {
                OpenLocked(outputPath, codecpar, inputTimeBase, rotationDegrees, audioCodecpar);
                EnsureWriterRunning();
            }
        }
    }

    /// <summary>
    /// Opens a live stream for browsers: an HLS playlist at
    /// <paramref name="playlistPath"/> with segments of two seconds next to
    /// it. Old segments are removed as new ones are written. With
    /// <paramref name="audioCodecpar"/> the segments carry a sound track.
    /// </summary>
    public void OpenLive(
        string playlistPath,
        AVCodecParameters* codecpar,
        AVRational inputTimeBase,
        AVCodecParameters* audioCodecpar = null)
    {
        lock (stateLock)
        {
            lock (ioLock)
            {
                OpenLocked(playlistPath, codecpar, inputTimeBase, rotationDegrees: 0, audioCodecpar, live: true);
                EnsureWriterRunning();
            }
        }
    }

    /// <summary>
    /// Writes out every packet that is still queued.
    /// </summary>
    public void Flush()
    {
        lock (stateLock)
        {
            lock (ioLock)
            {
                DrainQueueLocked();
            }
        }
    }

    /// <summary>
    /// Moves the recording on to <paramref name="newOutputPath"/>. A file has to start with a key
    /// frame, so the switch is made at the next one: until then packets go on into the current
    /// file, and none are lost between the two (before, everything up to the next key frame was
    /// dropped from the new file: seconds every hour). A file without pictures yet, or one that
    /// can take no more, is switched at once.
    /// </summary>
    public void SwitchTo(
        string newOutputPath,
        AVCodecParameters* codecpar,
        AVRational inputTimeBase,
        int rotationDegrees = 0,
        AVCodecParameters* audioCodecpar = null)
    {
        lock (stateLock)
        {
            pendingSwitch?.Dispose();
            pendingSwitch = null;
            if (outputCtx is not null && receivedKeyframe && writeError is null)
            {
                pendingSwitch = new PendingSwitch(newOutputPath, codecpar, inputTimeBase, rotationDegrees, audioCodecpar);
                return;
            }

            lock (ioLock)
            {
                CloseLocked();
                OpenLocked(newOutputPath, codecpar, inputTimeBase, rotationDegrees, audioCodecpar);
            }
        }
    }

    private void OpenLocked(
        string outputPath,
        AVCodecParameters* codecpar,
        AVRational inputTimeBase,
        int rotationDegrees,
        AVCodecParameters* audioCodecpar,
        bool live = false)
    {
        try
        {
            writeError = null;
            OpenOutputLocked(outputPath, codecpar, rotationDegrees, audioCodecpar, live);
            this.outputPath = outputPath;
        }
        catch
        {
            // A recording that could not be started must not stay "open":
            // the next start would be taken for one that is already running,
            // and packets would go into a file without a header.
            if (outputCtx is not null)
            {
                if (outputCtx->pb is not null)
                {
                    avio_closep(ref outputCtx->pb);
                }

                avformat_free_context(outputCtx);
                outputCtx = null;
            }

            DeleteFile(outputPath);
            throw;
        }
    }

    private static void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // An empty file left behind is the lesser problem.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private void OpenOutputLocked(
        string outputPath,
        AVCodecParameters* codecpar,
        int rotationDegrees,
        AVCodecParameters* audioCodecpar,
        bool live)
    {
        AVFormatContext* ctx = null;

        var ret = live
            ? avformat_alloc_output_context2(ref ctx, null, "hls", outputPath)
            : avformat_alloc_output_context2(ref ctx, null, null, outputPath);
        if (ret < 0 || ctx is null)
        {
            throw new FFmpegException(ret, "Failed to allocate output context");
        }

        outputCtx = ctx;

        var outStream = avformat_new_stream(outputCtx, null);
        if (outStream is null)
        {
            throw new InvalidOperationException("Failed to create output stream.");
        }

        ret = avcodec_parameters_copy(outStream->codecpar, codecpar);
        if (ret < 0)
        {
            throw new FFmpegException(ret, "Failed to copy codec parameters to output");
        }

        outStream->codecpar->codec_tag = 0;
        outputTimeBase = outStream->time_base;

        // Flag the rotation so players turn the picture at playback. The
        // packets are copied untouched, so the pixels stay unrotated.
        DisplayMatrix.Write(outStream->codecpar, rotationDegrees);

        AVStream* audioStream = null;
        if (audioCodecpar is not null)
        {
            audioStream = avformat_new_stream(outputCtx, null);
            if (audioStream is null)
            {
                throw new InvalidOperationException("Failed to create the output stream for sound.");
            }

            ret = avcodec_parameters_copy(audioStream->codecpar, audioCodecpar);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to copy the sound's codec parameters to output");
            }

            audioStream->codecpar->codec_tag = 0;
        }

        // A live stream is many files, which its muxer opens itself.
        if (!live)
        {
            ret = avio_open(ref outputCtx->pb, outputPath, IOFlags.Write);
            if (ret < 0)
            {
                throw new FFmpegException(ret, "Failed to open output file");
            }
        }

        AVDictionary* opts = null;
        try
        {
            if (live)
            {
                // Short segments keep the delay low; only the last few are
                // listed, and the ones no longer listed are deleted.
                av_dict_set(ref opts, "hls_time", "2", DictWriteFlags.None);
                av_dict_set(ref opts, "hls_list_size", "5", DictWriteFlags.None);
                av_dict_set(ref opts, "hls_flags", "delete_segments", DictWriteFlags.None);
            }
            else if (IsMp4Family(outputPath))
            {
                // A plain MP4 keeps its index (the moov box) for the end of
                // the file, so a recording cut short by a crash or a power
                // cut has no index and cannot be opened at all. Writing the
                // header up front and the video in self-contained fragments,
                // one per keyframe, keeps everything up to the last fragment
                // playable. Matroska is already written this way.
                av_dict_set(ref opts, "movflags", "frag_keyframe+empty_moov+default_base_moof", DictWriteFlags.None);
            }

            ret = avformat_write_header(outputCtx, ref opts);
        }
        finally
        {
            av_dict_free(ref opts);
        }

        if (ret < 0)
        {
            throw new FFmpegException(ret, "Failed to write output header");
        }

        outputTimeBase = outStream->time_base;
        liveParameterSets = live ? ReadAnnexBParameterSets(codecpar) : null;
        hasAudio = audioStream is not null;
        audioOutputTimeBase = hasAudio ? audioStream->time_base : default;
        originMicroseconds = AV_NOPTS_VALUE;
        lastDts = AV_NOPTS_VALUE;
        lastAudioDts = AV_NOPTS_VALUE;
        receivedKeyframe = false;
    }

    // An encoder that keeps the stream's parameters (SPS/PPS) for the file
    // header leaves them out of the packets. A recording has that header; a
    // live stream's segments are played one by one from wherever the viewer
    // joins, so each needs the parameters in front of its key frame.
    // Only parameters in start-code form can be put in front as they are;
    // in the other form (as in an MP4) the muxer converts the packets and
    // adds them itself.
    private static byte[]? ReadAnnexBParameterSets(AVCodecParameters* codecpar)
    {
        if (codecpar->extradata is null || codecpar->extradata_size < 4)
        {
            return null;
        }

        var extradata = new ReadOnlySpan<byte>(codecpar->extradata, codecpar->extradata_size);
        var startsWithStartCode =
            extradata[0] == 0 && extradata[1] == 0 &&
            (extradata[2] == 1 || (extradata[2] == 0 && extradata[3] == 1));

        return startsWithStartCode ? extradata.ToArray() : null;
    }

    private static void PrependParameterSets(
        ref AVPacket* packet,
        byte[] parameterSets)
    {
        var withParameters = av_packet_alloc();
        if (withParameters is null)
        {
            return;
        }

        if (av_new_packet(withParameters, parameterSets.Length + packet->size) < 0 ||
            av_packet_copy_props(withParameters, packet) < 0)
        {
            av_packet_free(ref withParameters);
            return;
        }

        var target = new Span<byte>(withParameters->data, withParameters->size);
        parameterSets.CopyTo(target);
        new ReadOnlySpan<byte>(packet->data, packet->size).CopyTo(target[parameterSets.Length..]);

        av_packet_free(ref packet);
        packet = withParameters;
    }

    private static bool IsMp4Family(string outputPath)
        => Path.GetExtension(outputPath).ToUpperInvariant() is ".MP4" or ".MOV" or ".M4V";

    public void WritePacket(
        AVPacket* srcPacket,
        AVRational srcTimeBase)
    {
        lock (stateLock)
        {
            if (outputCtx is null)
            {
                return;
            }

            var isKeyframe = (srcPacket->flags & PktFlags.Key) != PktFlags.None;
            if (isKeyframe && pendingSwitch is { } next && !SwitchAtKeyFrame(next))
            {
                return;
            }

            if (!receivedKeyframe)
            {
                if (!isKeyframe)
                {
                    return;
                }

                receivedKeyframe = true;
            }

            var clonedPkt = av_packet_clone(srcPacket);
            if (clonedPkt is null)
            {
                return;
            }

            try
            {
                // Worked out in microseconds on the source's clock, shifted past any jump in it.
                var dtsUs = clonedPkt->dts != AV_NOPTS_VALUE
                    ? av_rescale_q(clonedPkt->dts, srcTimeBase, Microseconds) + shiftMicroseconds
                    : AV_NOPTS_VALUE;
                var ptsUs = clonedPkt->pts != AV_NOPTS_VALUE
                    ? av_rescale_q(clonedPkt->pts, srcTimeBase, Microseconds) + shiftMicroseconds
                    : dtsUs;
                if (dtsUs != AV_NOPTS_VALUE)
                {
                    var correction = Rebase(dtsUs);
                    dtsUs += correction;
                    if (ptsUs != AV_NOPTS_VALUE)
                    {
                        ptsUs += correction;
                    }
                }

                if (originMicroseconds == AV_NOPTS_VALUE)
                {
                    originMicroseconds = dtsUs != AV_NOPTS_VALUE ? dtsUs : 0;
                }

                var dtsOffset = dtsUs != AV_NOPTS_VALUE ? dtsUs - originMicroseconds : 0;
                var ptsOffset = ptsUs != AV_NOPTS_VALUE ? ptsUs - originMicroseconds : dtsOffset;

                clonedPkt->dts = av_rescale_q(dtsOffset, Microseconds, outputTimeBase);
                clonedPkt->pts = av_rescale_q(ptsOffset, Microseconds, outputTimeBase);
                if (clonedPkt->duration > 0)
                {
                    clonedPkt->duration = av_rescale_q(clonedPkt->duration, srcTimeBase, outputTimeBase);
                }

                clonedPkt->stream_index = 0;
                clonedPkt->pos = -1;

                // Enforce monotonically increasing DTS — required by strict muxers (e.g. Matroska).
                // RTSP streams with B-frames can deliver packets out of DTS order.
                if (lastDts != AV_NOPTS_VALUE && clonedPkt->dts <= lastDts)
                {
                    clonedPkt->dts = lastDts + 1;
                    if (clonedPkt->pts < clonedPkt->dts)
                    {
                        clonedPkt->pts = clonedPkt->dts;
                    }
                }

                lastDts = clonedPkt->dts;

                if (isKeyframe && liveParameterSets is not null)
                {
                    PrependParameterSets(ref clonedPkt, liveParameterSets);
                }

                // Hand the packet to the background writer. The synchronous
                // disk write used to block the demux thread; on a slow
                // drive (cleanup pass on the same disk, network share
                // hiccup) that backed up the RTSP buffer and dropped
                // frames upstream. The writer writes under ioLock only, so
                // CloseLocked can flush before changing outputCtx.
                if (TryEnqueueDropOldest((nint)clonedPkt))
                {
                    // Ownership transferred to the queue — do NOT free here.
                    clonedPkt = null;
                    packetQueued.Set();
                }
            }
            finally
            {
                if (clonedPkt is not null)
                {
                    av_packet_free(ref clonedPkt);
                }
            }
        }
    }

    /// <summary>
    /// Writes a packet of sound. Sound from before the file's first picture
    /// is left out: the file's clock starts with that picture.
    /// </summary>
    public void WriteAudioPacket(
        AVPacket* srcPacket,
        AVRational srcTimeBase)
    {
        lock (stateLock)
        {
            if (outputCtx is null || !hasAudio || !receivedKeyframe || srcPacket->pts == AV_NOPTS_VALUE)
            {
                return;
            }

            // Sound shares the picture's clock, and the shift that keeps it going past a jump.
            var offset = av_rescale_q(srcPacket->pts, srcTimeBase, Microseconds) + shiftMicroseconds - originMicroseconds;
            if (offset < 0)
            {
                return;
            }

            var clonedPkt = av_packet_clone(srcPacket);
            if (clonedPkt is null)
            {
                return;
            }

            clonedPkt->pts = av_rescale_q(offset, Microseconds, audioOutputTimeBase);
            clonedPkt->dts = clonedPkt->pts;
            clonedPkt->duration = av_rescale_q(srcPacket->duration, srcTimeBase, audioOutputTimeBase);
            clonedPkt->stream_index = 1;
            clonedPkt->pos = -1;

            if (lastAudioDts != AV_NOPTS_VALUE && clonedPkt->dts <= lastAudioDts)
            {
                clonedPkt->dts = lastAudioDts + 1;
                clonedPkt->pts = clonedPkt->dts;
            }

            lastAudioDts = clonedPkt->dts;

            if (TryEnqueueDropOldest((nint)clonedPkt))
            {
                packetQueued.Set();
            }
            else
            {
                av_packet_free(ref clonedPkt);
            }
        }
    }

    // Called under stateLock with a packet's decode time (microseconds, already shifted). Returns
    // what to add to it: zero, or after a jump what puts it one picture after the last.
    private long Rebase(long dtsMicroseconds)
    {
        var correction = 0L;
        if (lastSourceDtsMicroseconds != AV_NOPTS_VALUE)
        {
            var step = dtsMicroseconds - lastSourceDtsMicroseconds;
            if (step < -JumpBackMicroseconds || step > JumpForwardMicroseconds)
            {
                correction = lastSourceDtsMicroseconds + lastStepMicroseconds - dtsMicroseconds;
                shiftMicroseconds += correction;
                ClockJumps++;
            }
            else if (step > 0)
            {
                lastStepMicroseconds = step;
            }
        }

        lastSourceDtsMicroseconds = dtsMicroseconds + correction;
        return correction;
    }

    // Called under stateLock with a key frame in hand: the current file is closed and the next one
    // opened, and the key frame is its first picture. False when the next file could not be opened
    // (the recording then reports why, and takes no more packets).
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Runs on the thread that reads the camera; a failure is reported through WriteError.")]
    private bool SwitchAtKeyFrame(PendingSwitch next)
    {
        pendingSwitch = null;
        using (next)
        {
            try
            {
                lock (ioLock)
                {
                    CloseLocked();
                    OpenLocked(next.Path, next.Video, next.TimeBase, next.Rotation, next.Audio);
                }

                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                writeError = ex.Message;
                return false;
            }
        }
    }

    // BlockingCollection has no native drop-oldest mode; emulate it by
    // attempting a non-blocking add and, on failure, draining the oldest
    // queued packet (freeing it) before retrying once.
    private bool TryEnqueueDropOldest(nint ptr)
    {
        if (writeQueue.IsAddingCompleted)
        {
            return false;
        }

        if (writeQueue.TryAdd(ptr))
        {
            return true;
        }

        if (writeQueue.TryTake(out var dropped))
        {
            FreeQueuedPacket(dropped);
        }

        return writeQueue.TryAdd(ptr);
    }

    // Started lazily on first Open and stopped in Dispose. Writes the
    // queued packets to the current outputCtx under ioLock. CloseLocked
    // drains synchronously before closing the muxer, so packets are never
    // written to a stale outputCtx.
    private void EnsureWriterRunning()
    {
        if (writerThread is not null)
        {
            return;
        }

        writerThread = new Thread(WriterLoop)
        {
            IsBackground = true,
            Name = "Remuxer.Writer",
        };
        writerThread.Start();
    }

    private void WriterLoop()
    {
        try
        {
            while (!stopWriter)
            {
                packetQueued.WaitOne();

                // A packet is only taken out of the queue while holding the
                // lock. Taken first and written after waiting for the lock,
                // it could be overtaken by a Close or a SwitchTo: the file
                // it belonged to was closed without it, and the packet was
                // dropped or written into the next file.
                // The lock is taken per packet so WritePacket is never kept
                // waiting for a whole backlog to be written.
                bool written;
                do
                {
                    written = WriteNextQueuedPacket();
                }
                while (written);
            }
        }
        catch (ObjectDisposedException)
        {
            // Race during Dispose; safe to ignore.
        }
    }

    private bool WriteNextQueuedPacket()
    {
        lock (ioLock)
        {
            if (!writeQueue.TryTake(out var ptr))
            {
                return false;
            }

            if (outputCtx is not null)
            {
                WriteLocked((AVPacket*)ptr);
            }

            FreeQueuedPacket(ptr);
            return true;
        }
    }

    // Must be called while holding both locks. Writes every queued packet to
    // the current outputCtx (or just frees them if outputCtx is null).
    private void DrainQueueLocked()
    {
        while (writeQueue.TryTake(out var ptr))
        {
            if (outputCtx is not null)
            {
                WriteLocked((AVPacket*)ptr);
            }

            FreeQueuedPacket(ptr);
        }
    }

    // Must be called while holding ioLock with an open output. After a write that ends the
    // file, the rest is dropped: the recording reports the error instead of claiming to run.
    private void WriteLocked(AVPacket* pkt)
    {
        if (writeError is not null)
        {
            return;
        }

        var ret = av_interleaved_write_frame(outputCtx, pkt);
        var ioError = outputCtx->pb is null ? 0 : outputCtx->pb->error;
        if (WriteErrorClassifier.IsFatal(ret, ioError))
        {
            writeError = new FFmpegException(ret < 0 ? ret : ioError, $"Writing '{outputPath}' failed").Message;
        }
    }

    private static void FreeQueuedPacket(nint ptr)
    {
        var pkt = (AVPacket*)ptr;
        av_packet_free(ref pkt);
    }

    public void Close()
    {
        lock (stateLock)
        {
            pendingSwitch?.Dispose();
            pendingSwitch = null;
            lock (ioLock)
            {
                CloseLocked();
            }
        }
    }

    private void CloseLocked()
    {
        if (outputCtx is null)
        {
            return;
        }

        // Flush every packet still in the queue to the current outputCtx
        // before writing the trailer. The writer task is blocked on the
        // ioLock by us, so this drain is exclusive.
        DrainQueueLocked();

        var hasPictures = receivedKeyframe;
        if (hasPictures)
        {
            av_write_trailer(outputCtx);
        }

        if (outputCtx->pb is not null)
        {
            avio_closep(ref outputCtx->pb);
        }

        avformat_free_context(outputCtx);
        outputCtx = null;
        hasAudio = false;
        liveParameterSets = null;

        // A header and nothing to play is not a recording: the camera
        // stopped delivering right after the start.
        if (!hasPictures && outputPath is not null)
        {
            DeleteFile(outputPath);
        }

        outputPath = null;
        originMicroseconds = AV_NOPTS_VALUE;
        lastAudioDts = AV_NOPTS_VALUE;
        shiftMicroseconds = 0;
        lastSourceDtsMicroseconds = AV_NOPTS_VALUE;
        lastStepMicroseconds = 40_000;
        lastDts = AV_NOPTS_VALUE;
        receivedKeyframe = false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Close();

        // Stop the writer thread cleanly: no more packets, wake it so it
        // sees the stop, then join with a bounded wait.
        writeQueue.CompleteAdding();
        stopWriter = true;
        packetQueued.Set();
        writerThread?.Join(TimeSpan.FromSeconds(2));

        // Free any packets still queued at shutdown so we don't leak
        // native memory.
        while (writeQueue.TryTake(out var ptr))
        {
            FreeQueuedPacket(ptr);
        }

        writeQueue.Dispose();
        packetQueued.Dispose();
        writerThread = null;
    }

    // The next file of a switch that waits for a key frame, with its own copies of the codec
    // parameters: the caller's may be gone by then.
    private sealed class PendingSwitch : IDisposable
    {
        private AVCodecParameters* video;
        private AVCodecParameters* audio;

        public PendingSwitch(
            string path,
            AVCodecParameters* video,
            AVRational timeBase,
            int rotation,
            AVCodecParameters* audio)
        {
            Path = path;
            TimeBase = timeBase;
            Rotation = rotation;
            this.video = Copy(video);
            this.audio = Copy(audio);
        }

        public string Path { get; }

        public AVRational TimeBase { get; }

        public int Rotation { get; }

        public AVCodecParameters* Video => video;

        public AVCodecParameters* Audio => audio;

        public void Dispose()
        {
            if (video is not null)
            {
                avcodec_parameters_free(ref video);
            }

            if (audio is not null)
            {
                avcodec_parameters_free(ref audio);
            }
        }

        private static AVCodecParameters* Copy(AVCodecParameters* source)
        {
            if (source is null)
            {
                return null;
            }

            var copy = avcodec_parameters_alloc();
            if (avcodec_parameters_copy(copy, source) < 0)
            {
                avcodec_parameters_free(ref copy);
                throw new InvalidOperationException("The codec parameters of the next file could not be copied.");
            }

            return copy;
        }
    }
}