namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers;

/// <summary>
/// Waits for an <see cref="IMediaPipeline"/> to reach
/// <see cref="AtcSoft.VideoSurveillance.Enums.ConnectionState.Connected"/>.
/// Used by handlers (StartRecording, CaptureSnapshot) that need a definite
/// connect outcome before acting on the pipeline — pipelineFactory.Create
/// returns immediately while the actual RTSP open runs on a background thread,
/// so calling CaptureFrameAsync / StartRecording without waiting is racy.
/// </summary>
internal static class PipelineConnectionWaiter
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Waits for a pipeline that may be playing already to deliver
    /// pictures. A shared pipeline may have been playing for hours, so its
    /// "connected" event may be long past: it is asked, not listened to.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the pipeline delivers pictures;
    /// <see langword="false"/> on an error, when it was closed, on timeout
    /// or on cancellation.
    /// </returns>
    public static async Task<bool> WaitForPicturesAsync(
        IMediaPipeline pipeline,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var failed = false;

        void OnStateChanged(
            object? sender,
            AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs e)
        {
            if (e.NewState == AtcSoft.VideoSurveillance.Enums.ConnectionState.Error)
            {
                Volatile.Write(ref failed, true);
            }
        }

        pipeline.ConnectionStateChanged += OnStateChanged;
        try
        {
            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                if (pipeline.FramesDecoded > 0)
                {
                    return true;
                }

                if (Volatile.Read(ref failed) || DateTime.UtcNow >= deadline || cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                await Task.Delay(PollInterval, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException)
        {
            // Its host closed it meanwhile.
            return false;
        }
        finally
        {
            pipeline.ConnectionStateChanged -= OnStateChanged;
        }
    }

    /// <summary>
    /// Returns true when the pipeline reaches Connected, false on Error,
    /// timeout, or external cancellation.
    /// </summary>
    public static async Task<bool> WaitForConnectedAsync(
        IMediaPipeline pipeline,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(
            object? sender,
            AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs e)
        {
            if (e.NewState == AtcSoft.VideoSurveillance.Enums.ConnectionState.Connected)
            {
                tcs.TrySetResult(true);
            }
            else if (e.NewState == AtcSoft.VideoSurveillance.Enums.ConnectionState.Error)
            {
                tcs.TrySetResult(false);
            }
        }

        pipeline.ConnectionStateChanged += Handler;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            await using var reg = cts.Token.Register(() => tcs.TrySetResult(false));

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            pipeline.ConnectionStateChanged -= Handler;
        }
    }
}