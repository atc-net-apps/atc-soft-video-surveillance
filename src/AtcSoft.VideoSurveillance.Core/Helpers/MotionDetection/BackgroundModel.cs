namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// What the scene looks like without anything moving: per pixel a running mean and variance.
/// A pixel that keeps changing (a leaf in the wind) gets a large variance and stops counting as
/// foreground; a moving object differs from the background as a whole, not only at its edges.
/// A foreground pixel learns slowly (something that stopped stays foreground for a while), but
/// only once it has matched the background since it was learnt anew: until then the background
/// is the more likely mistake (something that was in the first picture and left), and the pixel
/// learns at the background's speed, so its ghost goes within a second.
/// <para>
/// A covered pixel learns no variance, or something that stopped would be taken in within
/// seconds. A pixel that keeps going back to the background (a branch or a net in the wind) is
/// swaying, not covered: each return adds to its flicker, which fades at the background's speed,
/// and a covered pixel with enough flicker learns its variance from its swings, so wind is taken
/// in. Pixels inside an object being followed (the caller's <c>held</c>) do neither: a walker's
/// legs swing too.
/// </para>
/// </summary>
/// <remarks>
/// The per-pixel step runs on <see cref="Vector{T}"/>, with the plain loop for the last pixels.
/// Both do the same single-precision operations in the same order (no fused multiply-add), so the
/// result is the same to the bit whatever the CPU's vector width.
/// </remarks>
internal sealed class BackgroundModel
{
    private const float StartVariance = 36f;
    private const float MinVariance = 9f;
    private const float MaxVariance = 2500f;
    private const byte StateUnsettled = 0;
    private const byte StateBackground = 1;
    private const byte StateCovered = 2;

    // What each return to the background adds to a pixel's flicker (at most 1), and from how much
    // flicker a covered pixel learns its variance (fully at 1). Two returns within the
    // background's time: a branch swaying, not something that stopped.
    private const float FlickerStep = 0.5f;
    private const float FlickerLearnsFrom = 0.3f;
    private const float FlickerScale = 1 / (1 - FlickerLearnsFrom);

    private float[] mean = [];
    private float[] variance = [];

    // Per pixel: Unsettled (not background once since the background was learnt anew), Background,
    // or Covered (with the background from before it was covered in 'anchor'). A covered pixel stays
    // covered after what covers it is taken in (a parked car, a person close to the lens while
    // most of the picture differs), until the picture is that background again.
    private byte[] settled = [];
    private float[] anchor = [];
    private float[] flicker = [];

    // The watch zones' thresholds spread per pixel, kept while the zones and their values stay.
    private byte[]? zonedFor;
    private float[] zonedSigmas = [];
    private float[] zonedMins = [];
    private float[] pixelSigmas = [];
    private float[] pixelMins = [];

    public int Width { get; private set; }

    public int Height { get; private set; }

    public bool IsEmpty
        => mean.Length == 0;

    /// <summary>
    /// Gets the mean per pixel, read only (for the tests that compare the vector and plain steps).
    /// </summary>
    public ReadOnlySpan<float> Mean
        => mean;

    /// <summary>
    /// Gets the variance per pixel, read only.
    /// </summary>
    public ReadOnlySpan<float> Variance
        => variance;

    /// <summary>
    /// Gets the flicker per pixel, read only.
    /// </summary>
    public ReadOnlySpan<float> Flicker
        => flicker;

    public void Reset(
        byte[] frame,
        int width,
        int height)
    {
        Width = width;
        Height = height;
        if (mean.Length != frame.Length)
        {
            mean = new float[frame.Length];
            variance = new float[frame.Length];
            settled = new byte[frame.Length];
            anchor = new float[frame.Length];
            flicker = new float[frame.Length];
        }
        else
        {
            Array.Clear(settled);
            Array.Clear(flicker);
        }

        for (var i = 0; i < frame.Length; i++)
        {
            mean[i] = frame[i];
            variance[i] = StartVariance;
        }
    }

    /// <summary>
    /// Shifts the whole background by the picture's overall change in brightness, so a lighting
    /// change (exposure, a cloud, dusk) does not make everything foreground while what is in the
    /// picture stays as it was.
    /// </summary>
    /// <param name="frame">The (blurred) picture after the change.</param>
    public void ShiftTo(byte[] frame)
    {
        double sum = 0;
        for (var i = 0; i < frame.Length; i++)
        {
            sum += frame[i] - mean[i];
        }

        var shift = (float)(sum / frame.Length);
        for (var i = 0; i < mean.Length; i++)
        {
            mean[i] += shift;
            anchor[i] += shift;
        }
    }

    /// <summary>
    /// Finds the foreground of a picture and learns from it.
    /// </summary>
    /// <param name="frame">The (blurred) picture.</param>
    /// <param name="mask">Set to 1 where the picture is foreground.</param>
    /// <param name="sigmas">The threshold in standard deviations.</param>
    /// <param name="minThreshold">The threshold's minimum in grey levels.</param>
    /// <param name="backgroundRate">How much a background pixel learns now (0 to 1).</param>
    /// <param name="foregroundRate">How much a foreground pixel learns now (0 to 1).</param>
    /// <param name="ignored">Pixels never counted as foreground (1), still learnt; <see langword="null"/> for none.</param>
    /// <param name="held">Pixels of objects being followed (1): no flicker, so they are not taken in as wind; <see langword="null"/> for none.</param>
    /// <returns>The share of the pixels looked at that is foreground.</returns>
    public double Apply(
        byte[] frame,
        byte[] mask,
        double sigmas,
        double minThreshold,
        double backgroundRate,
        double foregroundRate,
        byte[]? ignored = null,
        byte[]? held = null)
        => Learn(
            frame,
            mask,
            (float)(sigmas * sigmas),
            (float)(minThreshold * minThreshold),
            pixelSigmas: default,
            pixelMins: default,
            (float)backgroundRate,
            (float)foregroundRate,
            ignored,
            held);

    /// <summary>
    /// As <see cref="Apply"/>, with the thresholds of the watch zone each pixel is in: index 0 of
    /// the thresholds is the camera's, outside every watch zone.
    /// </summary>
    /// <param name="frame">The picture, blurred.</param>
    /// <param name="mask">Where the foreground is written, one byte per pixel.</param>
    /// <param name="sigmas">The standard deviations a pixel must differ by, by zone.</param>
    /// <param name="minThresholds">The grey levels a pixel must differ by, by zone.</param>
    /// <param name="watched">The watch zone of each pixel (see <see cref="ZoneMask.Watched"/>).</param>
    /// <param name="backgroundRate">How much the background learns of a pixel that is not foreground.</param>
    /// <param name="foregroundRate">How much it learns of one that is.</param>
    /// <param name="ignored">The ignored pixels, or <see langword="null"/>.</param>
    /// <param name="held">The pixels of objects being followed, or <see langword="null"/>.</param>
    /// <returns>The share of the pixels looked at that are foreground.</returns>
    public double ApplyZoned(
        byte[] frame,
        byte[] mask,
        IReadOnlyList<double> sigmas,
        IReadOnlyList<double> minThresholds,
        byte[] watched,
        double backgroundRate,
        double foregroundRate,
        byte[]? ignored = null,
        byte[]? held = null)
    {
        ArgumentNullException.ThrowIfNull(sigmas);
        ArgumentNullException.ThrowIfNull(minThresholds);
        ArgumentNullException.ThrowIfNull(watched);

        var s2 = sigmas.Select(s => (float)(s * s)).ToArray();
        var min2 = minThresholds.Select(m => (float)(m * m)).ToArray();
        SpreadZones(watched, s2, min2);
        return Learn(
            frame,
            mask,
            sigmas2: 0,
            mins2: 0,
            pixelSigmas,
            pixelMins,
            (float)backgroundRate,
            (float)foregroundRate,
            ignored,
            held);
    }

    // Each pixel's squared thresholds from its zone; spread again only when the zones or their values
    // change. A new layout comes as a new array (ZoneMask.Watched), never as an edit of the old one.
    private void SpreadZones(
        byte[] watched,
        float[] s2,
        float[] min2)
    {
        if (ReferenceEquals(zonedFor, watched) &&
            pixelSigmas.Length == watched.Length &&
            s2.AsSpan().SequenceEqual(zonedSigmas) &&
            min2.AsSpan().SequenceEqual(zonedMins))
        {
            return;
        }

        if (pixelSigmas.Length != watched.Length)
        {
            pixelSigmas = new float[watched.Length];
            pixelMins = new float[watched.Length];
        }

        for (var i = 0; i < watched.Length; i++)
        {
            pixelSigmas[i] = s2[watched[i]];
            pixelMins[i] = min2[watched[i]];
        }

        zonedFor = watched;
        zonedSigmas = s2;
        zonedMins = min2;
    }

    // The per-pixel step: foreground where the picture differs from the mean by more than both
    // thresholds (squared), then the mean and variance learn at the pixel's rate. The thresholds are
    // the same for every pixel, or per pixel when those spans are given.
    private double Learn(
        byte[] frame,
        byte[] mask,
        float sigmas2,
        float mins2,
        ReadOnlySpan<float> pixelSigmas,
        ReadOnlySpan<float> pixelMins,
        float bg,
        float fg,
        byte[]? ignored,
        byte[]? held)
    {
        var perPixel = !pixelSigmas.IsEmpty;

        // Flicker fades at the background's speed; with no time passed nothing is added.
        var flickerKeep = 1 - bg;
        var flickerStep = bg > 0 ? FlickerStep : 0f;
        var count = 0;
        var looked = frame.Length;
        var i = 0;
        var lanes = Vector<float>.Count;
        var step = Vector<byte>.Count;
        if (Vector.IsHardwareAccelerated && frame.Length >= step)
        {
            var thresholds = new LearnStep(sigmas2, mins2, bg, fg, flickerKeep, flickerStep);
            var counted = Vector<int>.Zero;
            var lookedAt = Vector<int>.Zero;
            for (; i + step <= frame.Length; i += step)
            {
                Vector.Widen(new Vector<byte>(frame.AsSpan(i, step)), out var pixelsLow, out var pixelsHigh);
                Vector.Widen(pixelsLow, out var pixels0, out var pixels1);
                Vector.Widen(pixelsHigh, out var pixels2, out var pixels3);
                var keep = ignored is null
                    ? Vector<byte>.AllBitsSet
                    : Vector.Equals(new Vector<byte>(ignored.AsSpan(i, step)), Vector<byte>.Zero);

                // Widened as signed, so all bits set stays all bits set (-1).
                Vector.Widen(Vector.AsVectorSByte(keep), out var keepLow, out var keepHigh);
                Vector.Widen(keepLow, out var keep0, out var keep1);
                Vector.Widen(keepHigh, out var keep2, out var keep3);
                var holds = held is null
                    ? Vector<byte>.Zero
                    : ~Vector.Equals(new Vector<byte>(held.AsSpan(i, step)), Vector<byte>.Zero);
                Vector.Widen(Vector.AsVectorSByte(holds), out var holdLow, out var holdHigh);
                Vector.Widen(holdLow, out var hold0, out var hold1);
                Vector.Widen(holdHigh, out var hold2, out var hold3);
                var states = new Vector<byte>(settled.AsSpan(i, step));
                Vector.Widen(states, out var statesLow, out var statesHigh);
                Vector.Widen(statesLow, out var states0, out var states1);
                Vector.Widen(statesHigh, out var states2, out var states3);

                var foreground0 = LearnLanes(i, pixels0, keep0, hold0, Vector.AsVectorInt32(states0), pixelSigmas, pixelMins, thresholds, out var next0);
                var foreground1 = LearnLanes(i + lanes, pixels1, keep1, hold1, Vector.AsVectorInt32(states1), pixelSigmas, pixelMins, thresholds, out var next1);
                var foreground2 = LearnLanes(i + (2 * lanes), pixels2, keep2, hold2, Vector.AsVectorInt32(states2), pixelSigmas, pixelMins, thresholds, out var next2);
                var foreground3 = LearnLanes(i + (3 * lanes), pixels3, keep3, hold3, Vector.AsVectorInt32(states3), pixelSigmas, pixelMins, thresholds, out var next3);

                // -1 per foreground pixel and per pixel looked at.
                counted += foreground0 + foreground1 + foreground2 + foreground3;
                lookedAt += keep0 + keep1 + keep2 + keep3;
                var foreground = Vector.AsVectorByte(Vector.Narrow(Vector.Narrow(foreground0, foreground1), Vector.Narrow(foreground2, foreground3)));
                (foreground & Vector<byte>.One).CopyTo(mask.AsSpan(i, step));
                Vector.AsVectorByte(Vector.Narrow(Vector.Narrow(next0, next1), Vector.Narrow(next2, next3))).CopyTo(settled.AsSpan(i, step));
            }

            count = -Vector.Sum(counted);
            looked = -Vector.Sum(lookedAt) + (frame.Length - i);
        }

        for (; i < frame.Length; i++)
        {
            var sigmaLimit = perPixel ? pixelSigmas[i] : sigmas2;
            var minLimit = perPixel ? pixelMins[i] : mins2;
            var d = frame[i] - mean[i];
            var d2 = d * d;
            var isForeground = d2 > minLimit && d2 > sigmaLimit * variance[i];
            var state = settled[i];
            var restored = false;
            if (state == StateCovered)
            {
                // Back to the background from before it was covered: what covered it left.
                var back = frame[i] - anchor[i];
                var back2 = back * back;
                if (!(back2 > minLimit && back2 > sigmaLimit * variance[i]))
                {
                    mean[i] = anchor[i];
                    d = back;
                    d2 = back2;
                    isForeground = false;
                    restored = true;
                }
            }

            if (ignored is not null && ignored[i] != 0)
            {
                isForeground = false;
                looked--;
            }

            mask[i] = isForeground ? (byte)1 : (byte)0;
            if (isForeground)
            {
                count++;
            }

            if (isForeground && state == StateBackground)
            {
                anchor[i] = mean[i];
            }

            var stopped = isForeground && state != StateUnsettled;
            var rate = stopped ? fg : bg;
            var inside = held is not null && held[i] != 0;
            var f = flicker[i] * flickerKeep;
            if (restored && !inside)
            {
                f = Math.Min(1f, f + flickerStep);
            }

            flicker[i] = f;
            settled[i] = isForeground
                ? (state == StateUnsettled ? StateUnsettled : StateCovered)
                : (state == StateCovered && !restored ? StateCovered : StateBackground);

            mean[i] += rate * d;

            // The variance learns how much the background varies (swaying leaves raise theirs),
            // not how far something that stopped differs from it, or that would hide it in seconds;
            // a covered pixel that keeps going back (swaying) learns from its swings.
            var swaying = Math.Clamp((f - FlickerLearnsFrom) * FlickerScale, 0f, 1f);
            var varianceRate = stopped ? (inside ? 0f : bg * swaying) : rate;
            variance[i] = Math.Clamp(variance[i] + (varianceRate * (d2 - variance[i])), MinVariance, MaxVariance);
        }

        return looked > 0 ? count / (double)looked : 0;
    }

    // One vector of pixels from 'at': the same steps as the plain loop. Returns -1 where foreground,
    // and each pixel's next state.
    private Vector<int> LearnLanes(
        int at,
        Vector<uint> pixels,
        Vector<int> keep,
        Vector<int> hold,
        Vector<int> states,
        ReadOnlySpan<float> pixelSigmas,
        ReadOnlySpan<float> pixelMins,
        in LearnStep step,
        out Vector<int> next)
    {
        var lanes = Vector<float>.Count;
        var means = mean.AsSpan(at, lanes);
        var variances = variance.AsSpan(at, lanes);
        var anchors = anchor.AsSpan(at, lanes);
        var m = new Vector<float>(means);
        var v = new Vector<float>(variances);
        var a = new Vector<float>(anchors);
        var value = Vector.ConvertToSingle(Vector.AsVectorInt32(pixels));
        var d = value - m;
        var d2 = d * d;
        var s = pixelSigmas.IsEmpty ? step.Sigmas : new Vector<float>(pixelSigmas.Slice(at, lanes));
        var min = pixelMins.IsEmpty ? step.Mins : new Vector<float>(pixelMins.Slice(at, lanes));
        var isForeground = Vector.GreaterThan(d2, min) & Vector.GreaterThan(d2, s * v);

        // Back to the background from before it was covered: what covered it left.
        var back = value - a;
        var back2 = back * back;
        var restore = Vector.Equals(states, new Vector<int>(StateCovered)) & ~(Vector.GreaterThan(back2, min) & Vector.GreaterThan(back2, s * v));
        m = Vector.ConditionalSelect(restore, a, m);
        d = Vector.ConditionalSelect(restore, back, d);
        d2 = Vector.ConditionalSelect(restore, back2, d2);
        isForeground = Vector.AndNot(isForeground, restore) & keep;

        Vector.ConditionalSelect(isForeground & Vector.Equals(states, new Vector<int>(StateBackground)), m, a).CopyTo(anchors);
        var unsettled = Vector.Equals(states, new Vector<int>(StateUnsettled));
        var covered = Vector.AndNot(Vector.Equals(states, new Vector<int>(StateCovered)), restore);
        next = Vector.ConditionalSelect(
            isForeground,
            Vector.ConditionalSelect(unsettled, new Vector<int>(StateUnsettled), new Vector<int>(StateCovered)),
            Vector.ConditionalSelect(covered, new Vector<int>(StateCovered), new Vector<int>(StateBackground)));
        var stopped = Vector.AndNot(isForeground, unsettled);
        var rate = Vector.ConditionalSelect(stopped, step.Foreground, step.Background);
        var flickers = flicker.AsSpan(at, lanes);
        var f = new Vector<float>(flickers) * step.FlickerKeep;
        f = Vector.ConditionalSelect(Vector.AndNot(restore, hold), Vector.Min(f + step.FlickerAdd, Vector<float>.One), f);
        f.CopyTo(flickers);
        (m + (rate * d)).CopyTo(means);
        var swaying = Vector.Min(Vector.Max((f - step.FlickerFrom) * step.FlickerSpread, Vector<float>.Zero), Vector<float>.One);
        var varianceRate = Vector.ConditionalSelect(
            stopped,
            Vector.ConditionalSelect(hold, Vector<float>.Zero, step.Background * swaying),
            rate);
        Vector.Min(Vector.Max(v + (varianceRate * (d2 - v)), step.Low), step.High).CopyTo(variances);
        return isForeground;
    }

    // The values of one Learn as vectors.
    [StructLayout(LayoutKind.Auto)]
    private readonly struct LearnStep(
        float sigmas2,
        float mins2,
        float background,
        float foreground,
        float flickerKeep,
        float flickerStep)
    {
        public Vector<float> Sigmas { get; } = new(sigmas2);

        public Vector<float> Mins { get; } = new(mins2);

        public Vector<float> Background { get; } = new(background);

        public Vector<float> Foreground { get; } = new(foreground);

        public Vector<float> Low { get; } = new(MinVariance);

        public Vector<float> High { get; } = new(MaxVariance);

        public Vector<float> FlickerKeep { get; } = new(flickerKeep);

        public Vector<float> FlickerAdd { get; } = new(flickerStep);

        public Vector<float> FlickerFrom { get; } = new(FlickerLearnsFrom);

        public Vector<float> FlickerSpread { get; } = new(FlickerScale);
    }
}