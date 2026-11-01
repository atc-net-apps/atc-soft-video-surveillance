namespace AtcSoft.Video.Surveillance.Blazor.App.Services;

/// <summary>
/// A read-only stream whose read fails with an <see cref="IOException"/> when
/// no byte arrives for the idle time, or when it is aborted: the generated
/// event reader takes that as a broken connection and opens the stream again.
/// </summary>
internal sealed class IdleTimeoutStream : Stream
{
    private readonly Stream inner;
    private readonly TimeSpan idleTimeout;
    private readonly TimeProvider time;
    private readonly EventStreamWatchdog owner;
    private readonly CancellationTokenSource aborted = new();
    private int disposed;

    public IdleTimeoutStream(
        Stream inner,
        TimeSpan idleTimeout,
        TimeProvider time,
        EventStreamWatchdog owner)
    {
        this.inner = inner;
        this.idleTimeout = idleTimeout;
        this.time = time;
        this.owner = owner;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Fails the read that waits now, and every read after it.
    /// </summary>
    public void Abort()
    {
        try
        {
            aborted.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Closed meanwhile.
        }
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        using var idle = new CancellationTokenSource(idleTimeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idle.Token, aborted.Token);
        try
        {
            return await inner.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException(
                aborted.IsCancellationRequested
                    ? "The event stream was ended to open it again."
                    : $"No data on the event stream for {idleTimeout.TotalSeconds:0} seconds: the connection is taken as lost.",
                ex);
        }
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(
        byte[] buffer,
        int offset,
        int count)
        => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(
        long offset,
        SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(
        byte[] buffer,
        int offset,
        int count)
        => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref disposed, 1) == 0)
        {
            owner.Forget(this);
            aborted.Dispose();
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}