namespace AtcSoft.Video.Surveillance.E2E.Infrastructure;

/// <summary>
/// ONVIF in a camera dialog: search the network, and read the streams of a camera at an address. In
/// the WPF dialogs the search is on the Find camera tab and the read on the Camera tab, next to the
/// address; in the web client both are in one panel of the form. No ONVIF camera is needed: what the
/// search and the read answer on the test machine (none found, a network that cannot be searched,
/// a camera that does not answer ONVIF) is fine, as long as the dialog says one of its own answers.
/// A camera found and picked brings the Camera tab back.
/// </summary>
internal static class OnvifWalk
{
    /// <summary>
    /// The web client's ONVIF panel.
    /// </summary>
    public const string WebHeader = "Find and read ONVIF cameras";

    /// <summary>
    /// The WPF dialogs' tab with the search.
    /// </summary>
    public const string FindTab = "Find camera";

    /// <summary>
    /// The WPF dialogs' tab with the address and the read.
    /// </summary>
    public const string CameraTab = "Camera";

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
    /// Searches on a WPF camera dialog's Find camera tab, then types the address on its Camera tab
    /// and reads from it.
    /// </summary>
    public static void FindAndRead(
        DesktopApp app,
        Window dialog)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(dialog);

        SelectTab(app, dialog, FindTab);
        DesktopApp.Press(app.Find(dialog, "Find cameras", ControlType.Button));
        WaitForAnswer(app, dialog, SearchAnswers, "a search's answer");
        PickAFoundCamera(app, dialog);

        SelectTab(app, dialog, CameraTab);
        TypeAddress(app, dialog);
        var read = app.Find(dialog, "Read from camera", ControlType.Button);
        WaitUntilEnabled(read, "Read from camera is enabled once an address is typed");
        DesktopApp.Press(read);
        WaitForAnswer(app, dialog, ReadAnswers, "a read's answer");
    }

    private static void SelectTab(
        DesktopApp app,
        Window dialog,
        string name)
    {
        DesktopApp.Press(app.Find(dialog, name, ControlType.TabItem));
        DesktopApp.Settle(TimeSpan.FromMilliseconds(500));
    }

    // A camera the search found (CameraWall names its button "name · model · address"): picked, it
    // fills in the address on the Camera tab, which comes back. Nothing to pick on a network without one.
    private static void PickAFoundCamera(
        DesktopApp app,
        Window dialog)
    {
        var found = dialog
            .FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            .FirstOrDefault(button => button.Name?.Contains(" · ", StringComparison.Ordinal) == true);
        if (found is null)
        {
            return;
        }

        DesktopApp.Press(found);
        DesktopApp.Settle(TimeSpan.FromMilliseconds(500));
        app.Find(dialog, CameraTab, ControlType.TabItem).Patterns.SelectionItem.Pattern.IsSelected.Value
            .Should().BeTrue("a camera picked on the Find camera tab brings the Camera tab back");
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