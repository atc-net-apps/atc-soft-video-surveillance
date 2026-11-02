namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// What became of listing a hub's cameras.
/// </summary>
public enum CameraHubOutcome
{
    /// <summary>
    /// The hub answered with its cameras.
    /// </summary>
    Listed,

    /// <summary>
    /// Nothing answered at the address.
    /// </summary>
    Unreachable,

    /// <summary>
    /// Something answered, but not this kind of hub.
    /// </summary>
    NotThisKind,

    /// <summary>
    /// The hub refused the user name or password.
    /// </summary>
    SignInRefused,
}