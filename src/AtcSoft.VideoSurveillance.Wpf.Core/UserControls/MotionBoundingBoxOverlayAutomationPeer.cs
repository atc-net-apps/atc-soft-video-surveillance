namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

using Rectangle = System.Windows.Shapes.Rectangle;

/// <summary>
/// Shows the overlay's boxes to UI Automation, one element per box on screen, so a UI test can
/// read where the boxes are: each has the automation id <c>box-{trackId}</c> (or
/// <c>box-at-{place}</c> without a track) and, as its help text, the detector's box in video
/// pixels (<see cref="MotionBoundingBoxOverlay.DescribeBox"/>).
/// </summary>
internal sealed class MotionBoundingBoxOverlayAutomationPeer : System.Windows.Automation.Peers.UserControlAutomationPeer
{
    private readonly MotionBoundingBoxOverlay overlay;
    private readonly Dictionary<Rectangle, System.Windows.Automation.Peers.AutomationPeer> boxPeers = [];

    public MotionBoundingBoxOverlayAutomationPeer(
        MotionBoundingBoxOverlay owner)
        : base(owner)
        => overlay = owner;

    protected override string GetClassNameCore()
        => nameof(MotionBoundingBoxOverlay);

    protected override List<System.Windows.Automation.Peers.AutomationPeer>? GetChildrenCore()
    {
        var children = overlay
            .ShownBoxRectangles()
            .Select(PeerFor)
            .ToList();
        return children.Count == 0 ? null : children;
    }

    private System.Windows.Automation.Peers.AutomationPeer PeerFor(
        Rectangle rectangle)
    {
        if (!boxPeers.TryGetValue(rectangle, out var peer))
        {
            peer = new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(rectangle);
            boxPeers[rectangle] = peer;
        }

        return peer;
    }
}