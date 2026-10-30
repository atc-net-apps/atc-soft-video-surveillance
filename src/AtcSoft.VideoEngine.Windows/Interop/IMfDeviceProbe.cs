namespace AtcSoft.VideoEngine.Windows.Interop;

/// <summary>
/// Internal seam for the Media Foundation enumeration call. The real
/// implementation calls <c>MFEnumDeviceSources</c>; tests can plug in
/// a fake to verify the mapping logic without a live webcam.
/// </summary>
internal interface IMfDeviceProbe
{
    /// <summary>
    /// Returns the friendly-name + symbolic-link pairs for every video
    /// capture device the OS reports.
    /// </summary>
    IReadOnlyList<MfDeviceRow> EnumerateVideoCaptureDevices();
}