namespace AtcSoft.Video.Surveillance.E2E.Infrastructure;

/// <summary>
/// The ONVIF part of a camera dialog (CameraWall, the WPF client, the web client): open it, search
/// the network, and read the streams of a camera at an address. No ONVIF camera is needed: what the
/// search and the read answer on the test machine (none found, a network that cannot be searched,
/// a camera that does not answer ONVIF) is fine, as long as the part says one of its own answers.
/// </summary>
internal static class OnvifWalk
{
    /// <summary>
    /// The part's header.
    /// </summary>
    public const string Header = "Find and read ONVIF cameras";

    /// <summary>
    /// The address read from: this machine, where no ONVIF camera answers.
    /// </summary>
    public const string Address = "127.0.0.1";

    /// <summary>
    /// What the part says when a search has ended.
    /// </summary>
    public static readonly IReadOnlyList<string> SearchAnswers =
    [
        "No ONVIF cameras answered.",
        "This network cannot be searched from here.",
        "found. Pick one to fill in its address.",
    ];

    /// <summary>
    /// What the part says when a read has ended.
    /// </summary>
    public static readonly IReadOnlyList<string> ReadAnswers =
    [
        "The main stream and the sub-stream are filled in.",
        "The main stream is filled in.",
        "The camera does not answer ONVIF at this address and port.",
        "The camera refused the user and password.",
        "The camera did not answer in time.",
        "The camera offers no RTSP stream.",
    ];

    /// <summary>
    /// How long a search or a read may take: a search listens for three seconds, a read tries
    /// four ports with a timeout each.
    /// </summary>
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Opens the part in a WPF camera dialog, searches, types the address and reads from it.
    /// </summary>
    public static void FindAndRead(
        DesktopApp app,
        Window dialog)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(dialog);

        Expand(app, dialog);

        DesktopApp.Press(app.Find(dialog, "Find cameras", ControlType.Button));
        WaitForAnswer(app, dialog, SearchAnswers, "a search's answer");

        TypeAddress(app, dialog);
        var read = app.Find(dialog, "Read from camera", ControlType.Button);
        WaitUntilEnabled(read, "Read from camera is enabled once an address is typed");
        DesktopApp.Press(read);
        WaitForAnswer(app, dialog, ReadAnswers, "a read's answer");
    }

    // The header's text sits next to its toggle button, which has no name of its own.
    private static void Expand(
        DesktopApp app,
        Window dialog)
    {
        var header = app.Find(dialog, Header, ControlType.Text);
        var toggle = header.Parent?.FindFirstChild(cf => cf.ByAutomationId("ExpanderToggleButton"))
                     ?? throw app.Fail($"No toggle button next to '{Header}'.");
        DesktopApp.Press(toggle);
        app.Find(dialog, "Find cameras", ControlType.Button);
    }

    // The address box is a labelled text box, which shows no name to UI Automation: it is found
    // by its automation id (IpAddress, in both dialogs).
    private static void TypeAddress(
        DesktopApp app,
        Window dialog)
    {
        var box = app.FindWhere(dialog, e => e.AutomationId == "IpAddress", "the IP address box");
        var edit = box.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
                   ?? throw app.Fail("No text box in the IP address box.");
        edit.Patterns.Value.Pattern.SetValue(Address);
        DesktopApp.Settle(TimeSpan.FromMilliseconds(500));
    }

    private static void WaitForAnswer(
        DesktopApp app,
        Window dialog,
        IReadOnlyList<string> answers,
        string what)
        => app.FindWhere(
            dialog,
            e => e.ControlType == ControlType.Text &&
                 e.Name is { Length: > 0 } name &&
                 answers.Any(a => name.Contains(a, StringComparison.Ordinal)),
            what,
            AnswerTimeout);

    private static void WaitUntilEnabled(
        AutomationElement element,
        string because)
    {
        var deadline = DateTime.UtcNow + DesktopApp.ElementTimeout;
        while (!element.IsEnabled && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(200);
        }

        element.IsEnabled.Should().BeTrue(because);
    }
}