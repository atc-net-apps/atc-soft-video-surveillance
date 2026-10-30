namespace AtcSoft.VideoEngine.Windows.Interop;

/// <summary>
/// One device returned from <see cref="IMfDeviceProbe"/>. Carries
/// the friendly-name + symbolic-link pair plus the capability list
/// (resolution × frame-rate × pixel-format triples) the device
/// advertises through Media Foundation.
/// </summary>
internal sealed record MfDeviceRow(
    string SymbolicLink,
    string FriendlyName,
    IReadOnlyList<MfCapability> Capabilities);