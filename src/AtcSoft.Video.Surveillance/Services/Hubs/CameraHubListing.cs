namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// A hub's cameras, or why they could not be listed.
/// </summary>
/// <param name="Outcome">What became of the listing.</param>
/// <param name="Kind">The kind of hub (<see cref="CameraHubKinds"/>).</param>
/// <param name="Address">The hub's address as it was reached (scheme, host and port).</param>
/// <param name="Cameras">Its cameras, when listed.</param>
public sealed record CameraHubListing(
    CameraHubOutcome Outcome,
    string Kind,
    Uri Address,
    IReadOnlyList<HubCamera> Cameras)
{
    /// <summary>
    /// Gets a value indicating whether the hub only knows an older, less secure sign-in (motionEye
    /// before 0.44, as in motionEyeOS): it works, and the user is told to update the hub when they can.
    /// </summary>
    public bool OlderSignIn { get; init; }

    public static CameraHubListing Failed(
        CameraHubOutcome outcome,
        string kind,
        Uri address)
        => new(outcome, kind, address, []);
}