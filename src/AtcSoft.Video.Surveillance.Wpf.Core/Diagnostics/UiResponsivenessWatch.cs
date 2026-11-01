namespace AtcSoft.Video.Surveillance.Wpf.Core.Diagnostics;

/// <summary>
/// Measures how long the UI thread takes to answer: every second a background thread asks it to run
/// nothing and waits. A slow answer is a frozen window. While the UI thread stays frozen, the watch
/// reports it after 10 seconds, a minute and five minutes; when it answers again, it reports for how
/// long it was frozen. Recording does not run on the UI thread, but a frozen window is what a user sees.
/// </summary>
public sealed class UiResponsivenessWatch : IDisposable
{
    private static readonly TimeSpan[] StillFrozenAfter = [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    private readonly Dispatcher dispatcher;
    private readonly TimeSpan frozenAfter;
    private readonly Action<TimeSpan> frozen;
    private readonly Action<TimeSpan> stillFrozen;
    private readonly CancellationTokenSource stopping = new();
    private Thread? thread;
    private long maxDelayTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiResponsivenessWatch"/> class.
    /// </summary>
    /// <param name="dispatcher">The UI thread's dispatcher.</param>
    /// <param name="frozenAfter">How slow an answer counts as a freeze.</param>
    /// <param name="frozen">Told, on the watch's thread, how long the UI thread was frozen once it answers again.</param>
    /// <param name="stillFrozen">Told, on the watch's thread, while the UI thread is still frozen.</param>
    public UiResponsivenessWatch(
        Dispatcher dispatcher,
        TimeSpan frozenAfter,
        Action<TimeSpan> frozen,
        Action<TimeSpan> stillFrozen)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.frozen = frozen ?? throw new ArgumentNullException(nameof(frozen));
        this.stillFrozen = stillFrozen ?? throw new ArgumentNullException(nameof(stillFrozen));
        this.frozenAfter = frozenAfter;
    }

    /// <summary>
    /// Starts watching.
    /// </summary>
    public void Start()
    {
        if (thread is not null)
        {
            return;
        }

        thread = new Thread(Watch)
        {
            IsBackground = true,
            Name = "UI responsiveness watch",
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
    }

    /// <summary>
    /// Gets the slowest answer since the last call, and starts over.
    /// </summary>
    /// <returns>The slowest answer.</returns>
    public TimeSpan TakeMaxDelay()
        => TimeSpan.FromTicks(Interlocked.Exchange(ref maxDelayTicks, 0));

    /// <inheritdoc />
    public void Dispose()
    {
        if (!stopping.IsCancellationRequested)
        {
            stopping.Cancel();
        }

        thread?.Join(TimeSpan.FromSeconds(2));
        stopping.Dispose();
    }

    private void Watch()
    {
        var token = stopping.Token;
        while (!token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1)))
        {
            if (dispatcher.HasShutdownStarted)
            {
                return;
            }

            var watch = Stopwatch.StartNew();
            var answer = dispatcher.BeginInvoke(DispatcherPriority.Normal, static () => { });
            var reported = 0;
            while (answer.Wait(TimeSpan.FromSeconds(1)) != DispatcherOperationStatus.Completed)
            {
                if (token.IsCancellationRequested || dispatcher.HasShutdownStarted || answer.Status == DispatcherOperationStatus.Aborted)
                {
                    return;
                }

                if (reported < StillFrozenAfter.Length && watch.Elapsed >= StillFrozenAfter[reported])
                {
                    stillFrozen(watch.Elapsed);
                    reported++;
                }
            }

            var delay = watch.Elapsed;
            var ticks = delay.Ticks;
            long seen;
            do
            {
                seen = Interlocked.Read(ref maxDelayTicks);
            }
            while (ticks > seen && Interlocked.CompareExchange(ref maxDelayTicks, ticks, seen) != seen);

            if (delay >= frozenAfter)
            {
                frozen(delay);
            }
        }
    }
}