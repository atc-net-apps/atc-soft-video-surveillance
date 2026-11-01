namespace Atc.Video.Engine;

/// <summary>
/// A time limit for reading a media file through FFmpeg: a file on a network share that stops
/// answering makes FFmpeg's open or read give up (it returns an error) instead of holding its
/// thread for ever. The limit is started again before each step that may wait for the file.
/// </summary>
internal sealed unsafe class FileReadDeadline : IDisposable
{
    // Kept alive for as long as FFmpeg may call it.
    private static readonly AVIOInterruptCB_callback Callback = Interrupt;

    private readonly long limitTicks;
    private GCHandle handle;
    private long endTimestamp;

    public FileReadDeadline(TimeSpan limit)
    {
        limitTicks = (long)(limit.TotalSeconds * Stopwatch.Frequency);
        Restart();
        handle = GCHandle.Alloc(this);
    }

    /// <summary>
    /// Makes a format context whose reads give up once the limit has passed.
    /// </summary>
    /// <returns>The context, for <c>avformat_open_input</c>.</returns>
    public AVFormatContext* NewContext()
    {
        var context = avformat_alloc_context();
        if (context is null)
        {
            throw new InvalidOperationException("Failed to allocate AVFormatContext.");
        }

        context->interrupt_callback.callback = Callback;
        context->interrupt_callback.opaque = (void*)GCHandle.ToIntPtr(handle);
        return context;
    }

    /// <summary>
    /// Starts the limit again, before a step that may wait for the file.
    /// </summary>
    public void Restart()
        => Volatile.Write(ref endTimestamp, Stopwatch.GetTimestamp() + limitTicks);

    public void Dispose()
    {
        if (handle.IsAllocated)
        {
            handle.Free();
        }
    }

    private static int Interrupt(void* opaque)
    {
        var target = GCHandle.FromIntPtr((nint)opaque).Target;
        return target is FileReadDeadline deadline &&
               Stopwatch.GetTimestamp() > Volatile.Read(ref deadline.endTimestamp)
            ? 1
            : 0;
    }
}