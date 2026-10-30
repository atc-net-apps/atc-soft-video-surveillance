namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Computes clock-aligned segmentation slots and detects whether a slot
/// boundary has already been processed.
/// </summary>
/// <remarks>
/// Slots are identified by a <see cref="DateOnly"/>+<see cref="int"/>
/// pair so that midnight rollovers, NTP-induced clock corrections, and
/// DST transitions never cause a slot to be processed twice nor a slot
/// to be silently skipped due to integer-division wraparound.
/// </remarks>
public static class RecordingSlotCalculator
{
    /// <summary>
    /// Computes the clock-aligned slot containing <paramref name="when"/>
    /// for an interval of <paramref name="intervalMinutes"/> minutes.
    /// </summary>
    /// <param name="when">The instant to bucket.</param>
    /// <param name="intervalMinutes">Slot length in minutes; must be positive.</param>
    /// <returns>
    /// A <c>(Date, Slot)</c> pair where <c>Slot</c> is the zero-based
    /// minute-of-day quotient. Two timestamps in the same slot return the
    /// same pair; the next slot returns a strictly greater pair when
    /// compared via <see cref="CompareSlot"/>.
    /// </returns>
    public static (DateOnly Date, int Slot) ComputeSlot(
        DateTime when,
        int intervalMinutes)
    {
        if (intervalMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMinutes),
                intervalMinutes,
                "intervalMinutes must be positive");
        }

        var date = DateOnly.FromDateTime(when);
        var slot = ((when.Hour * 60) + when.Minute) / intervalMinutes;
        return (date, slot);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="current"/> is strictly
    /// later than <paramref name="lastProcessed"/>. A backward jump (NTP
    /// correction, DST fall-back, manual clock change) returns <c>false</c>
    /// so the same wall-clock slot is never segmented twice.
    /// </summary>
    public static bool IsNewBoundary(
        (DateOnly Date, int Slot) current,
        (DateOnly Date, int Slot) lastProcessed)
        => CompareSlot(current, lastProcessed) > 0;

    /// <summary>
    /// Lexicographic compare on <c>(Date, Slot)</c>. Negative if
    /// <paramref name="a"/> precedes <paramref name="b"/>, zero if equal,
    /// positive if later.
    /// </summary>
    public static int CompareSlot(
        (DateOnly Date, int Slot) a,
        (DateOnly Date, int Slot) b)
    {
        var dc = a.Date.CompareTo(b.Date);
        return dc != 0 ? dc : a.Slot.CompareTo(b.Slot);
    }
}