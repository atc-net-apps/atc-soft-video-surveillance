namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Keeps a camera's motion on for the post-motion time after its last moving picture, so the
/// state does not flicker between two pictures. The desktop and the server use the same rule.
/// </summary>
public sealed class MotionHold
{
    private readonly TimeSpan holdFor;
    private DateTimeOffset lastMovingAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionHold"/> class.
    /// </summary>
    /// <param name="holdFor">How long motion stays on after the last moving picture.</param>
    public MotionHold(TimeSpan holdFor)
    {
        this.holdFor = holdFor < TimeSpan.Zero ? TimeSpan.Zero : holdFor;
    }

    /// <summary>
    /// Gets a value indicating whether motion is on.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Takes the result of one analysed picture.
    /// </summary>
    /// <param name="moving">Whether the picture showed movement.</param>
    /// <param name="now">When the picture was analysed.</param>
    /// <returns>What changed.</returns>
    public MotionHoldChange Update(
        bool moving,
        DateTimeOffset now)
    {
        if (moving)
        {
            lastMovingAt = now;
            if (IsActive)
            {
                return MotionHoldChange.Continues;
            }

            IsActive = true;
            return MotionHoldChange.Started;
        }

        if (IsActive && now - lastMovingAt >= holdFor)
        {
            IsActive = false;
            return MotionHoldChange.Ended;
        }

        return MotionHoldChange.None;
    }
}